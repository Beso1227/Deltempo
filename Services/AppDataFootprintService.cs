using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Services.Providers.CacheResolvers;

namespace WinTempCleaner.Services;

/// <summary>
/// One top-level folder inside a user AppData tree, with its measured size and whether
/// Deltempo already has a scope that can clean it.
/// </summary>
/// <param name="Path">Full path to the folder.</param>
/// <param name="Name">Folder display name (the AppData root itself uses its own name).</param>
/// <param name="RootLabel">Which AppData tree this folder belongs to.</param>
/// <param name="SizeBytes">Measured recursive size. 0 when the folder is empty or unreadable.</param>
/// <param name="LastWriteTimeUtc">Last write time, used to judge staleness.</param>
/// <param name="IsProtectedState">
/// True when the folder holds durable application state (OneDrive sync DB, Office licences,
/// credential vaults, key stores). Such folders are REPORTED but never offered for cleaning.
/// </param>
/// <param name="CoveredByExistingScope">
/// True when an existing Deltempo scope already targets something inside this folder, so the
/// user has a cleanup path today.
/// </param>
public sealed record AppDataFootprintEntry(
    string Path,
    string Name,
    string RootLabel,
    long SizeBytes,
    DateTime LastWriteTimeUtc,
    bool IsProtectedState,
    bool CoveredByExistingScope);

/// <summary>
/// READ-ONLY report of where a user's AppData space actually goes.
///
/// This type deliberately has NO delete capability of any kind: there is no Remove, no
/// Cleanup, no target registration, and it returns immutable records rather than the mutable
/// <see cref="Models.TargetFolderInfo"/> the cleaning pipeline consumes. A report that can
/// delete is not a report.
///
/// The question it answers is "why is my AppData N GB?", which the existing per-app resolvers
/// cannot: they clean known caches silently and never show the remaining mass. Nothing here is
/// ever auto-selected or offered as a deletion target.
/// </summary>
public static class AppDataFootprintService
{
    /// <summary>Roots scanned, in report order, with human labels.</summary>
    private static readonly (string Path, string Label)[] AppDataRoots =
    [
        (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Roaming"),
        (Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Local"),
        (Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData",
            "LocalLow"), "LocalLow"),
    ];

    /// <summary>
    /// Measures every top-level folder in the three AppData trees, largest first.
    ///
    /// This is a full recursive walk, so it is comparatively slow on a large profile. That is
    /// acceptable for an explicitly invoked report and is why it is never run during a scan.
    /// Unreadable folders report size 0 rather than aborting the whole report.
    /// </summary>
    public static List<AppDataFootprintEntry> Analyze(CancellationToken? cancellationToken = null)
    {
        var entries = new List<AppDataFootprintEntry>();
        var ct = cancellationToken ?? CancellationToken.None;

        // Existing cleanup targets, resolved once. Used only to LABEL rows as already
        // coverable - never to add anything to a deletion path.
        var knownScopes = CollectKnownScopePaths();

        foreach (var (rootPath, label) in AppDataRoots)
        {
            if (ct.IsCancellationRequested) break;
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath)) continue;

            IEnumerable<string> children;
            try
            {
                children = Directory.EnumerateDirectories(rootPath);
            }
            catch
            {
                continue; // unreadable root: report what we can
            }

            // Only DEPTH-1 folders are returned. Including the root itself as a row would
            // make every file count twice in any total (once under the root, once under its
            // own folder), so per-root totals are derived by grouping the returned entries
            // instead. See GroupBy(e => e.RootLabel).
            foreach (string child in children)
            {
                if (ct.IsCancellationRequested) break;

                // SKIP reparse points. AppData is full of legacy compatibility junctions -
                // "Application Data" and "Local Settings" are junctions that point straight back
                // at "Local". Measuring one re-walks the entire tree through the link and
                // double counts every byte beneath it (observed: a 59.5 GB total against a real
                // ~32 GB). A junction is also not this folder's own content, so it has no place
                // in a per-folder size breakdown.
                if (IsLink(child)) continue;

                entries.Add(MeasureFolder(child, Path.GetFileName(child), label, knownScopes));
            }
        }

        return entries
            .OrderByDescending(e => e.SizeBytes)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// True when the directory is a junction, symlink or other reparse point. Reported
    /// separately from <see cref="PathSecurity.IsReparsePointOrLink"/> so the AppData walk can
    /// skip links without triggering the protection reason string on a read-only scan.
    /// </summary>
    private static bool IsLink(string path)
    {
        try
        {
            return (new DirectoryInfo(path).Attributes & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            // Cannot stat it, so it cannot be measured either.
            return true;
        }
    }

    private static AppDataFootprintEntry MeasureFolder(
        string folderPath,
        string name,
        string label,
        IReadOnlyCollection<string> knownScopes)
    {
        long size = RootLeftoverScannerService.CalculateDirectorySizeSafe(folderPath);

        DateTime lastWrite;
        try
        {
            lastWrite = Directory.GetLastWriteTimeUtc(folderPath);
        }
        catch
        {
            lastWrite = DateTime.MinValue;
        }

        return new AppDataFootprintEntry(
            folderPath,
            name,
            label,
            size,
            lastWrite,
            IsProtectedState: ProtectionPolicy.IsAppDataApplicationState(folderPath),
            CoveredByExistingScope: IsCoveredByScope(folderPath, knownScopes));
    }

    /// <summary>
    /// True when an existing scope targets this folder or something beneath it, meaning the
    /// user already has a cleanup path for part of it.
    /// </summary>
    private static bool IsCoveredByScope(string folderPath, IReadOnlyCollection<string> knownScopes)
    {
        string canonical = PathSecurity.NormalizeCanonicalPath(folderPath);
        if (string.IsNullOrEmpty(canonical)) return false;

        foreach (string scope in knownScopes)
        {
            if (string.IsNullOrWhiteSpace(scope)) continue;
            if (PathSecurity.IsSubpathOf(canonical, scope) || PathSecurity.IsSubpathOf(scope, canonical))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Every directory Deltempo can already clean, across all resolvers. Unresolvable or
    /// machine-specific failures degrade to an empty set: the report is still correct, it just
    /// cannot label rows as already-coverable.
    /// </summary>
    private static IReadOnlyCollection<string> CollectKnownScopePaths()
    {
        var paths = new List<string>();

        void Add(Func<List<string>> resolver)
        {
            try
            {
                var resolved = resolver();
                if (resolved is null) return;
                paths.AddRange(resolved.Where(p => !string.IsNullOrWhiteSpace(p)));
            }
            catch
            {
                // A resolver that cannot enumerate on this machine simply contributes nothing.
            }
        }

        Add(() => CleanerService.GetAppCacheDirectories());
        Add(() => CleanerService.GetComponentCacheDirectories());
        Add(() => CleanerService.GetUpgradeLeftoverDirectories());
        Add(BrowserCacheResolver.Resolve);
        Add(DevPackageCacheResolver.Resolve);
        Add(MessagingAppCacheResolver.Resolve);
        Add(StoreAppCacheResolver.Resolve);

        return paths;
    }
}