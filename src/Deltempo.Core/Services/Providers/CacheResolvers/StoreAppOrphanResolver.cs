using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services.Providers.CacheResolvers;

/// <summary>
/// Detects ORPHANED Microsoft Store / MSIX application data under
/// %LOCALAPPDATA%\Packages.
///
/// Why this exists: <see cref="OrphanedAppService"/> matches orphaned app folders by EXECUTABLE
/// NAME. A Store package's data folder is named "Publisher.App_hash", which never matches an
/// .exe name, so real orphaned Store data was invisible to the existing orphan scan. Removing a
/// Store app leaves its whole data folder behind, and several of these are gigabytes.
///
/// The evidence here is BINARY, not heuristic: a package is either present in the AppModel
/// package repository or it is not. That is what makes this safe where name matching is not.
///
/// Matching is not a plain string comparison. The data folder is "Name_PublisherId" while the
/// repository key is "Name_Version_Arch_ResourceId_PublisherId", so both sides are split at the
/// final underscore and matched on publisher id plus a name prefix:
///   folder  aimgr_8wekyb3d8bbwe
///   repo    aimgr_0.20.62.0_x64__8wekyb3d8bbwe   -> prefix "aimgr_0.20.62.0_x64__"
/// A naive equality test would mark every Store app as an orphan.
/// </summary>
public static class StoreAppOrphanResolver
{
    /// <summary>
    /// A package removed minutes ago may be mid-reinstall, and an in-flight install rewrites
    /// its data folder. Mirrors the Safety Shield's 24h window.
    /// </summary>
    public static TimeSpan StalenessShield { get; set; } = TimeSpan.FromHours(24);

    private const int MinPublisherIdLength = 13;
    private const int MaxPublisherIdLength = 14;

    /// <summary>Per-user package repository.</summary>
    internal const string CurrentUserRepositoryPath =
        @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";

    /// <summary>Machine-wide package repository (inbox/system packages).</summary>
    internal const string MachineRepositoryPath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";

    /// <summary>
    /// Builds targets for every orphaned Store app data folder found.
    /// Returns an EMPTY list - never a guess - when the package repository cannot be read,
    /// because without it "not found" cannot be distinguished from "cannot tell".
    /// </summary>
    public static List<TargetFolderInfo> Resolve()
    {
        var targets = new List<TargetFolderInfo>();

        var registered = ReadRegisteredPackages(out bool repositoryReadable);
        if (!repositoryReadable || registered.Count == 0)
        {
            return targets; // fail closed
        }

        string packagesRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");

        if (!Directory.Exists(packagesRoot))
        {
            return targets;
        }

        foreach (string folder in Directory.EnumerateDirectories(packagesRoot))
        {
            var target = Evaluate(folder, registered, packagesRoot);
            if (target is not null)
            {
                targets.Add(target);
            }
        }

        return targets;
    }

    /// <summary>
    /// Evaluates one Packages subfolder. Returns null for anything not proposed for deletion.
    /// </summary>
    internal static TargetFolderInfo? Evaluate(
        string folder,
        IReadOnlyDictionary<string, List<string>> registered,
        string packagesRoot)
    {
        string folderName;
        try
        {
            folderName = Path.GetFileName(folder.TrimEnd('\\', '/'));
        }
        catch
        {
            return null;
        }

        // Not an MSIX data folder (legacy folders such as "ActiveSync" or the IE
        // "windows_ie_ac_001" lock folder carry no publisher id). Never propose these.
        if (!TrySplitPackageFolderName(folderName, out string packageName, out string publisherId))
        {
            return null;
        }

        // Still registered => a live app, not an orphan.
        if (IsRegistered(publisherId, packageName, registered))
        {
            return null;
        }

        // Single source of truth for refusals: this already covers communication packages
        // (IsCommunicationAppState refuses anything under \packages\ whose name is a known
        // comm app), browser credential stores, and sensitive extensions. Deliberately not a
        // duplicated app-name list, which would drift from the real policy.
        if (ProtectionPolicy.IsProtected(folder, out _))
        {
            return null;
        }

        // Recent activity => possibly mid-install or mid-uninstall.
        DateTime lastWrite;
        try
        {
            lastWrite = Directory.GetLastWriteTimeUtc(folder);
        }
        catch
        {
            return null;
        }

        if (DateTime.UtcNow - lastWrite < StalenessShield)
        {
            return null;
        }

        if (!InstalledAppService.IsSafeToDeleteResidual(folder))
        {
            return null;
        }

        // Never delete through a link: the folder could redirect outside Packages entirely.
        if (PathSecurity.IsReparsePointOrLink(folder))
        {
            return null;
        }

        long size = RootLeftoverScannerService.CalculateDirectorySizeSafe(folder);
        if (size <= 0)
        {
            return null; // nothing to reclaim; do not clutter the UI with empty rows
        }

        return new TargetFolderInfo
        {
            Id = $"StoreOrphan_{SanitizeIdentifier(folder)}",
            Name = $"{packageName} (Uninstalled Store App)",
            Category = "Store App Data",
            CategoryColor = "#8B5CF6",
            SafetyBadge = "Verified Orphan (App Uninstalled)",
            SafetyBadgeColor = "#10B981",
            Description =
                $"The Microsoft Store package '{packageName}' is no longer registered on this " +
                "machine, so this folder is unreachable app data. Review the top files before " +
                "removing it.",
            FolderPath = folder,
            IconGlyph = "",
            SizeBytes = size,
            IsOrphanedAppFolder = true,

            // LowRisk, never Safe: excluded from "Select 100% safe", from smart/one-click
            // cleans, and from the 24h per-file shield. Requires explicit user consent.
            IsSafeModeEligible = false,
            IsSelected = false,
            HasAccess = true
        };
    }

    /// <summary>
    /// Reads every registered package from both hives into publisherId -> name-prefix list.
    /// Sets <paramref name="readable"/> false when NEITHER hive could be opened.
    /// </summary>
    internal static Dictionary<string, List<string>> ReadRegisteredPackages(out bool readable)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        bool openedAny = false;

        foreach (var (hive, subKey) in new[]
                 {
                     (Registry.CurrentUser, CurrentUserRepositoryPath),
                     (Registry.LocalMachine, MachineRepositoryPath),
                 })
        {
            try
            {
                using var key = hive.OpenSubKey(subKey);
                if (key is null)
                {
                    continue; // hive readable but this location absent
                }

                openedAny = true;
                foreach (string name in key.GetSubKeyNames())
                {
                    if (!TrySplitRepositoryKeyName(name, out string packageName, out string publisherId))
                    {
                        continue;
                    }

                    if (!map.TryGetValue(publisherId, out var prefixes))
                    {
                        prefixes = new List<string>();
                        map[publisherId] = prefixes;
                    }
                    prefixes.Add(packageName);
                }
            }
            catch
            {
                // A denied or missing hive is not evidence that any package is unregistered.
            }
        }

        readable = openedAny;
        return map;
    }

    /// <summary>
    /// True when a package with this publisher id is registered under this name. The
    /// repository key carries extra version/arch segments, so the registered name must be a
    /// prefix of the folder name rather than equal to it.
    /// </summary>
    internal static bool IsRegistered(
        string publisherId,
        string packageName,
        IReadOnlyDictionary<string, List<string>> registered)
    {
        if (!registered.TryGetValue(publisherId, out var prefixes))
        {
            return false;
        }

        foreach (string prefix in prefixes)
        {
            // The repository key carries the version/arch tail the folder name omits, so the
            // registered prefix normally EXTENDS the folder name:
            //   folder name "aimgr"      vs prefix "aimgr_0.20.62.0_x64__"
            // Equality is kept for the case where a key genuinely has no tail segments.
            // The trailing underscore is what stops "aim" matching "aimgr_...".
            if (prefix.StartsWith(packageName + "_", StringComparison.OrdinalIgnoreCase) ||
                packageName.Equals(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Splits "Name_PublisherId" as used by Packages subfolder names.
    /// </summary>
    internal static bool TrySplitPackageFolderName(string folderName, out string packageName, out string publisherId)
    {
        packageName = string.Empty;
        publisherId = string.Empty;

        if (string.IsNullOrWhiteSpace(folderName))
        {
            return false;
        }

        int split = folderName.LastIndexOf('_');
        if (split <= 0 || split == folderName.Length - 1)
        {
            return false;
        }

        string candidateName = folderName[..split];
        string candidatePublisher = folderName[(split + 1)..];

        if (!IsValidPublisherId(candidatePublisher))
        {
            return false;
        }

        packageName = candidateName;
        publisherId = candidatePublisher;
        return true;
    }

    /// <summary>
    /// Splits "Name_Version_Arch_ResourceId_PublisherId" as used by repository keys. Same
    /// final-underscore split; the leading portion is a package NAME PREFIX here.
    /// </summary>
    internal static bool TrySplitRepositoryKeyName(string keyName, out string packageNamePrefix, out string publisherId)
    {
        packageNamePrefix = string.Empty;
        publisherId = string.Empty;

        if (string.IsNullOrWhiteSpace(keyName))
        {
            return false;
        }

        int split = keyName.LastIndexOf('_');
        if (split <= 0 || split == keyName.Length - 1)
        {
            return false;
        }

        string candidatePublisher = keyName[(split + 1)..];
        if (!IsValidPublisherId(candidatePublisher))
        {
            return false;
        }

        packageNamePrefix = keyName[..split];
        publisherId = candidatePublisher;
        return true;
    }

    /// <summary>
    /// Publisher ids are fixed-width alphanumeric tokens. The length check is what keeps
    /// legacy folders like "windows_ie_ac_001" out: its "001" is not a publisher id, so the
    /// folder is never treated as a package folder and therefore never proposed.
    /// </summary>
    internal static bool IsValidPublisherId(string publisherId)
    {
        if (string.IsNullOrEmpty(publisherId)) return false;
        if (publisherId.Length < MinPublisherIdLength || publisherId.Length > MaxPublisherIdLength) return false;

        foreach (char c in publisherId)
        {
            if (!char.IsLetterOrDigit(c)) return false;
        }

        return true;
    }

    private static string SanitizeIdentifier(string path)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = path.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }
}
