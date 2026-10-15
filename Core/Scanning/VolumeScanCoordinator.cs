using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using WinTempCleaner.Models;

namespace WinTempCleaner.Core.Scanning;

/// <summary>
/// Volume-aware concurrency coordinator that partitions filesystem operations by drive root.
/// Constrains parallel scan concurrency per physical/logical drive to prevent I/O bus thrashing,
/// while allowing simultaneous execution across distinct physical drives.
/// </summary>
public static class VolumeScanCoordinator
{
    public const int DefaultMaxParallelismPerVolume = 3;

    /// <summary>
    /// Resolves the partition for a cleanup TARGET rather than a raw path.
    /// </summary>
    /// <remarks>
    /// The scan used to be partitioned on TargetFolderInfo.FolderPath. For the eight scopes
    /// whose FolderPath is a real path that works. For the other seventeen it is a DISPLAY
    /// STRING - "Browser Web Caches", "App Caches Pool", "VSS Shadow Storage (System Volume
    /// Information)" - so Path.GetPathRoot returns nothing, every one of them resolved to the
    /// single "DEFAULT" partition, and all the work ran at maxParallelismPerVolume: 3
    /// together. The coordinator's stated purpose was not achieved for most of the scan.
    ///
    /// This resolves the target's ACTUAL directories and keys on the volume those live on.
    /// Resolving is not free - a device-driver resolver touches DriveInfo - so the result is
    /// only computed once per target for grouping, never per item.
    /// </remarks>
    public static string ResolveVolumeKeyForTarget(
        WinTempCleaner.Models.TargetFolderInfo target,
        Func<WinTempCleaner.Models.TargetFolderInfo, IEnumerable<string>> directoryResolver)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(directoryResolver);

        // A genuine path in FolderPath is both cheaper and more accurate than resolving.
        if (!string.IsNullOrWhiteSpace(target.FolderPath))
        {
            string? root = null;
            try
            {
                root = Path.GetPathRoot(target.FolderPath);
            }
            catch
            {
                // Not a path - fall through to resolution.
            }

            if (!string.IsNullOrEmpty(root))
            {
                return ResolveVolumeKey(target.FolderPath);
            }
        }

        // Shell/virtual targets carry their own identity and must not be merged.
        if (target.IsSpecialShellTarget)
        {
            return "VIRTUAL_SHELL";
        }

        try
        {
            var directories = directoryResolver(target);
            if (directories != null)
            {
                foreach (string directory in directories)
                {
                    if (string.IsNullOrWhiteSpace(directory)) continue;

                    string? root = null;
                    try
                    {
                        root = Path.GetPathRoot(directory);
                    }
                    catch
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(root))
                    {
                        return ResolveVolumeKey(directory);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Volume key resolution failed for '{target.Name}': {ex.Message}");
        }

        return "DEFAULT";
    }

    /// <summary>
    /// Identifies the normalized drive root or volume partition identifier for a given folder path.
    /// Non-standard and shell targets (e.g., RecycleBin, VSS) are assigned to an independent virtual partition.
    /// </summary>
    public static string ResolveVolumeKey(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "DEFAULT";

        try
        {
            if (path.StartsWith("Recycle", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("VSS", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("(System Volume Information)", StringComparison.OrdinalIgnoreCase))
            {
                return "VIRTUAL_SHELL";
            }

            string? root = Path.GetPathRoot(path);
            if (!string.IsNullOrEmpty(root))
            {
                return root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
            }
        }
        catch
        {
            // Path contains invalid characters or is a custom virtual identifier
        }

        return "DEFAULT";
    }

    /// <summary>
    /// Executes scan operations across items grouped by drive volume.
    /// Each volume runs as its own bounded <see cref="Parallel"/> loop, so concurrency per
    /// volume is capped at MaxDegreeOfParallelism without allocating one Task per item.
    /// </summary>
    public static async Task ExecutePartitionedAsync<T>(
        IEnumerable<T> items,
        Func<T, string> pathSelector,
        Func<T, CancellationToken, Task> itemAction,
        int maxParallelismPerVolume = DefaultMaxParallelismPerVolume,
        CancellationToken ct = default)
    {
        if (items == null) return;
        if (maxParallelismPerVolume <= 0) maxParallelismPerVolume = DefaultMaxParallelismPerVolume;

        // 1. Group items by resolved volume partition
        var volumeGroups = items
            .GroupBy(item => ResolveVolumeKey(pathSelector(item)))
            .ToList();

        // 2. Launch each volume partition concurrently, each with its own bounded parallel loop
        var partitionTasks = volumeGroups.Select(group => Parallel.ForEachAsync(
            group,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = maxParallelismPerVolume,
                CancellationToken = ct
            },
            async (item, token) => await itemAction(item, token).ConfigureAwait(false)));

        await Task.WhenAll(partitionTasks).ConfigureAwait(false);
    }
}
