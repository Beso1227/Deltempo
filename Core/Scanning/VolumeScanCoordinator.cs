using System.Collections.Concurrent;
using System.IO;

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
    /// Each volume enforces its own MaxDegreeOfParallelism via a dedicated SemaphoreSlim.
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

        // 2. Launch each volume partition concurrently
        var partitionTasks = volumeGroups.Select(async group =>
        {
            using var throttle = new SemaphoreSlim(maxParallelismPerVolume, maxParallelismPerVolume);
            var itemTasks = new List<Task>();

            foreach (var item in group)
            {
                if (ct.IsCancellationRequested) break;

                await throttle.WaitAsync(ct).ConfigureAwait(false);
                itemTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await itemAction(item, ct).ConfigureAwait(false);
                    }
                    finally
                    {
                        throttle.Release();
                    }
                }, ct));
            }

            await Task.WhenAll(itemTasks).ConfigureAwait(false);
        });

        await Task.WhenAll(partitionTasks).ConfigureAwait(false);
    }
}
