using System.IO;
using System.IO.Compression;
using System.Text.Json;
using WinTempCleaner.Models;

namespace WinTempCleaner.Core.Safety;

public class QuarantineFileEntry
{
    public string OriginalPath { get; set; } = string.Empty;
    public string ArchiveEntryName { get; set; } = string.Empty;
    public long OriginalSizeBytes { get; set; }
    public DateTime LastWriteTimeUtc { get; set; }
    public FileAttributes Attributes { get; set; }
}

public class QuarantineSessionManifest
{
    public string SessionId { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public string ScopeId { get; set; } = string.Empty;
    public string ScopeName { get; set; } = string.Empty;
    public long TotalOriginalSizeBytes { get; set; }
    public long CompressedSizeBytes { get; set; }
    public int FileCount { get; set; }
    public List<QuarantineFileEntry> Entries { get; set; } = new();
}

/// <summary>
/// Dedicated compressed quarantine vault and 1-click atomic restore engine.
/// Archives cleaned files into an LZ-compressed snapshot before deletion,
/// allowing instant reversal of accidental cache wipes or disrupted application sessions.
/// </summary>
public static class QuarantineManager
{
    private static string GetDefaultVaultDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(localAppData, "Deltempo", "Quarantine");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    }

    public static string VaultDirectory => GetDefaultVaultDirectory();

    /// <summary>
    /// Archives a list of existing files into a new timestamped quarantine session ZIP archive.
    /// </summary>
    public static async Task<QuarantineSessionManifest?> CreateQuarantineSnapshotAsync(
        string scopeId,
        string scopeName,
        IEnumerable<string> filePaths,
        CancellationToken ct = default)
    {
        var filesList = filePaths.Where(File.Exists).ToList();
        if (filesList.Count == 0) return null;

        string sessionId = $"Q_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}";
        string zipPath = Path.Combine(VaultDirectory, $"{sessionId}.dtq"); // Deltempo Quarantine archive

        var manifest = new QuarantineSessionManifest
        {
            SessionId = sessionId,
            CreatedUtc = DateTime.UtcNow,
            ScopeId = scopeId,
            ScopeName = scopeName,
            FileCount = filesList.Count
        };

        long totalOrigBytes = 0;

        await Task.Run(() =>
        {
            using (var zipStream = new FileStream(zipPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                int counter = 0;
                foreach (var file in filesList)
                {
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        var fi = new FileInfo(file);
                        if (!fi.Exists) continue;

                        string entryName = $"f_{counter++:D6}_{Path.GetFileName(file)}";
                        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
                        entry.LastWriteTime = fi.LastWriteTimeUtc;

                        using (var src = fi.OpenRead())
                        using (var dest = entry.Open())
                        {
                            src.CopyTo(dest);
                        }

                        totalOrigBytes += fi.Length;
                        manifest.Entries.Add(new QuarantineFileEntry
                        {
                            OriginalPath = fi.FullName,
                            ArchiveEntryName = entryName,
                            OriginalSizeBytes = fi.Length,
                            LastWriteTimeUtc = fi.LastWriteTimeUtc,
                            Attributes = fi.Attributes
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"[Quarantine] File archiving skipped '{file}': {ex.Message}");
                    }
                }

                // Add manifest inside archive
                manifest.TotalOriginalSizeBytes = totalOrigBytes;
                var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Fastest);
                using (var manifestStream = manifestEntry.Open())
                {
                    JsonSerializer.Serialize(manifestStream, manifest, new JsonSerializerOptions { WriteIndented = true });
                }
            }

            if (File.Exists(zipPath))
            {
                manifest.CompressedSizeBytes = new FileInfo(zipPath).Length;
            }
        }, ct);

        return manifest;
    }

    /// <summary>
    /// Restores all files from a quarantine session back to their exact original paths.
    /// </summary>
    public static async Task<(bool Success, int RestoredCount, long RestoredBytes, string ErrorMessage)> RestoreQuarantineSessionAsync(
        string sessionId,
        Action<string, LogLevel>? logAction = null,
        CancellationToken ct = default)
    {
        string zipPath = Path.Combine(VaultDirectory, $"{sessionId}.dtq");
        if (!File.Exists(zipPath))
        {
            return (false, 0, 0, "Quarantine archive file does not exist.");
        }

        int restoredCount = 0;
        long restoredBytes = 0;

        return await Task.Run(() =>
        {
            try
            {
                using var zipStream = File.OpenRead(zipPath);
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

                var manifestEntry = archive.GetEntry("manifest.json");
                if (manifestEntry == null)
                {
                    return (false, 0, 0, "Corrupted quarantine session: missing manifest.");
                }

                QuarantineSessionManifest? manifest;
                using (var mStream = manifestEntry.Open())
                {
                    manifest = JsonSerializer.Deserialize<QuarantineSessionManifest>(mStream);
                }

                if (manifest == null || manifest.Entries.Count == 0)
                {
                    return (false, 0, 0, "Quarantine session is empty or invalid.");
                }

                foreach (var item in manifest.Entries)
                {
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        // Validate path safety: Prevent zip-slip or arbitrary disk traversal
                        string canonicalTarget = PathSecurity.NormalizeCanonicalPath(item.OriginalPath);
                        if (string.IsNullOrEmpty(canonicalTarget)) continue;

                        if (ProtectionPolicy.IsProtected(canonicalTarget, out string protectedReason))
                        {
                            logAction?.Invoke($"Skipped restore of protected path '{canonicalTarget}': {protectedReason}", LogLevel.Warning);
                            continue;
                        }

                        var entry = archive.GetEntry(item.ArchiveEntryName);
                        if (entry == null) continue;

                        string? targetDir = Path.GetDirectoryName(canonicalTarget);
                        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }

                        using (var entryStream = entry.Open())
                        using (var destStream = new FileStream(canonicalTarget, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            entryStream.CopyTo(destStream);
                        }

                        File.SetLastWriteTimeUtc(canonicalTarget, item.LastWriteTimeUtc);
                        try { File.SetAttributes(canonicalTarget, item.Attributes); } catch { }

                        restoredCount++;
                        restoredBytes += item.OriginalSizeBytes;
                    }
                    catch (Exception ex)
                    {
                        logAction?.Invoke($"Could not restore '{item.OriginalPath}': {ex.Message}", LogLevel.Warning);
                    }
                }

                logAction?.Invoke($"Restored {restoredCount:N0} files ({TargetFolderInfo.FormatBytes(restoredBytes)}) from Quarantine session {sessionId}", LogLevel.Success);
                return (true, restoredCount, restoredBytes, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, restoredCount, restoredBytes, ex.Message);
            }
        }, ct);
    }

    /// <summary>
    /// Enumerates all stored quarantine sessions.
    /// </summary>
    public static List<QuarantineSessionManifest> GetQuarantineSessions()
    {
        var sessions = new List<QuarantineSessionManifest>();
        if (!Directory.Exists(VaultDirectory)) return sessions;

        foreach (var file in Directory.EnumerateFiles(VaultDirectory, "*.dtq"))
        {
            try
            {
                using var zipStream = File.OpenRead(file);
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
                var manifestEntry = archive.GetEntry("manifest.json");
                if (manifestEntry != null)
                {
                    using var mStream = manifestEntry.Open();
                    var manifest = JsonSerializer.Deserialize<QuarantineSessionManifest>(mStream);
                    if (manifest != null)
                    {
                        manifest.CompressedSizeBytes = new FileInfo(file).Length;
                        sessions.Add(manifest);
                    }
                }
            }
            catch { }
        }

        return sessions.OrderByDescending(s => s.CreatedUtc).ToList();
    }

    /// <summary>
    /// Automatically prunes quarantine archives older than retention days or exceeding max vault quota.
    /// </summary>
    public static int PruneExpiredQuarantine(TimeSpan maxAge, long maxTotalVaultBytes = 2L * 1024 * 1024 * 1024)
    {
        if (!Directory.Exists(VaultDirectory)) return 0;

        int prunedCount = 0;
        var cutoff = DateTime.UtcNow - maxAge;

        try
        {
            var files = new DirectoryInfo(VaultDirectory)
                .GetFiles("*.dtq")
                .OrderBy(f => f.CreationTimeUtc)
                .ToList();

            long totalBytes = files.Sum(f => f.Length);

            foreach (var f in files)
            {
                bool isExpired = f.CreationTimeUtc < cutoff;
                bool isOverQuota = totalBytes > maxTotalVaultBytes;

                if (isExpired || isOverQuota)
                {
                    long len = f.Length;
                    f.Delete();
                    totalBytes -= len;
                    prunedCount++;
                }
            }
        }
        catch { }

        return prunedCount;
    }
}
