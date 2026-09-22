using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using WinTempCleaner.Core.Safety;

namespace WinTempCleaner.Core.Services;

public enum DuplicateSelectionStrategy
{
    KeepOldest,
    KeepNewest,
    KeepShortestPath,
    KeepLongestPath
}

public class DuplicateFileItem
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime LastModified { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;
    public string DirectoryPath => Path.GetDirectoryName(FilePath) ?? string.Empty;
    public bool IsSelectedForDeletion { get; set; }
    public bool IsProtected { get; set; }
}

public class DuplicateGroup
{
    public long SizeBytes { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;
    public List<DuplicateFileItem> Files { get; set; } = new();
    public long TotalWastedBytes => Math.Max(0, SizeBytes * (Files.Count - 1));
}

public class DuplicateScanResult
{
    public List<DuplicateGroup> Groups { get; set; } = new();
    public int TotalDuplicateFilesCount => Groups.Sum(g => Math.Max(0, g.Files.Count - 1));
    public long TotalWastedBytes => Groups.Sum(g => g.TotalWastedBytes);
    public int TotalScannedFiles { get; set; }
    public TimeSpan Elapsed { get; set; }
}

public class DuplicateFileService
{
    private const int HeaderBufferSize = 4096;

    public static async Task<DuplicateScanResult> FindDuplicatesAsync(
        IEnumerable<string> targetPaths,
        long minFileSizeBytes = 1024,
        IProgress<(int scanned, int candidates)>? progress = null,
        CancellationToken ct = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var filesBySize = new ConcurrentDictionary<long, ConcurrentBag<string>>();
        int scannedCount = 0;

        await Task.Run(() =>
        {
            foreach (var targetPath in targetPaths)
            {
                if (ct.IsCancellationRequested) break;
                if (!Directory.Exists(targetPath)) continue;

                try
                {
                    var enumOptions = new EnumerationOptions
                    {
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = true,
                        AttributesToSkip = FileAttributes.ReparsePoint
                    };

                    var dirInfo = new DirectoryInfo(targetPath);
                    foreach (var fileInfo in dirInfo.EnumerateFiles("*", enumOptions))
                    {
                        if (ct.IsCancellationRequested) break;
                        Interlocked.Increment(ref scannedCount);

                        if (scannedCount % 500 == 0)
                        {
                            progress?.Report((scannedCount, 0));
                        }

                        if (fileInfo.Length < minFileSizeBytes) continue;

                        filesBySize.GetOrAdd(fileInfo.Length, _ => new ConcurrentBag<string>()).Add(fileInfo.FullName);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[DuplicateFileService] Error scanning directory {targetPath}: {ex.Message}");
                }
            }
        }, ct);

        // Phase 1 Filter: only keep sizes with 2+ files
        var potentialDuplicateSizes = filesBySize
            .Where(kvp => kvp.Value.Count > 1)
            .ToList();

        int totalCandidates = potentialDuplicateSizes.Sum(s => s.Value.Count);
        progress?.Report((scannedCount, totalCandidates));

        if (potentialDuplicateSizes.Count == 0 || ct.IsCancellationRequested)
        {
            return new DuplicateScanResult
            {
                TotalScannedFiles = scannedCount,
                Elapsed = stopwatch.Elapsed
            };
        }

        // Phase 2: First 4KB Header Hash
        var filesByHeader = new ConcurrentDictionary<string, ConcurrentBag<string>>();

        await Task.Run(() =>
        {
            Parallel.ForEach(potentialDuplicateSizes, new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Environment.ProcessorCount }, sizeGroup =>
            {
                foreach (var filePath in sizeGroup.Value)
                {
                    if (ct.IsCancellationRequested) break;
                    string headerHash = ComputeHeaderHash(filePath);
                    if (string.IsNullOrEmpty(headerHash)) continue;

                    string key = $"{sizeGroup.Key}:{headerHash}";
                    filesByHeader.GetOrAdd(key, _ => new ConcurrentBag<string>()).Add(filePath);
                }
            });
        }, ct);

        // Phase 2 Filter: only keep header matches with 2+ files
        var potentialFullMatches = filesByHeader
            .Where(kvp => kvp.Value.Count > 1)
            .ToList();

        if (potentialFullMatches.Count == 0 || ct.IsCancellationRequested)
        {
            return new DuplicateScanResult
            {
                TotalScannedFiles = scannedCount,
                Elapsed = stopwatch.Elapsed
            };
        }

        // Phase 3: Full SHA-256 Hash
        var filesByFullHash = new ConcurrentDictionary<string, ConcurrentBag<string>>();

        await Task.Run(() =>
        {
            Parallel.ForEach(potentialFullMatches, new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount / 2) }, headerGroup =>
            {
                foreach (var filePath in headerGroup.Value)
                {
                    if (ct.IsCancellationRequested) break;
                    string fullHash = ComputeFullSha256(filePath);
                    if (string.IsNullOrEmpty(fullHash)) continue;

                    filesByFullHash.GetOrAdd(fullHash, _ => new ConcurrentBag<string>()).Add(filePath);
                }
            });
        }, ct);

        var groups = new List<DuplicateGroup>();
        foreach (var kvp in filesByFullHash.Where(g => g.Value.Count > 1))
        {
            var fileList = new List<DuplicateFileItem>();
            long groupSize = 0;

            foreach (var filePath in kvp.Value)
            {
                try
                {
                    var fi = new FileInfo(filePath);
                    if (!fi.Exists) continue;

                    groupSize = fi.Length;
                    bool isProtected = ProtectionPolicy.IsProtected(filePath, out _);

                    fileList.Add(new DuplicateFileItem
                    {
                        FilePath = filePath,
                        FileName = fi.Name,
                        SizeBytes = fi.Length,
                        LastModified = fi.LastWriteTime,
                        Sha256Hash = kvp.Key,
                        IsProtected = isProtected
                    });
                }
                catch { }
            }

            if (fileList.Count > 1)
            {
                groups.Add(new DuplicateGroup
                {
                    SizeBytes = groupSize,
                    Sha256Hash = kvp.Key,
                    Files = fileList
                });
            }
        }

        stopwatch.Stop();

        return new DuplicateScanResult
        {
            Groups = groups.OrderByDescending(g => g.TotalWastedBytes).ToList(),
            TotalScannedFiles = scannedCount,
            Elapsed = stopwatch.Elapsed
        };
    }

    public static void ApplySelectionStrategy(List<DuplicateGroup> groups, DuplicateSelectionStrategy strategy)
    {
        foreach (var group in groups)
        {
            if (group.Files.Count <= 1) continue;

            DuplicateFileItem? keeper = null;

            switch (strategy)
            {
                case DuplicateSelectionStrategy.KeepOldest:
                    keeper = group.Files.OrderBy(f => f.LastModified).FirstOrDefault();
                    break;
                case DuplicateSelectionStrategy.KeepNewest:
                    keeper = group.Files.OrderByDescending(f => f.LastModified).FirstOrDefault();
                    break;
                case DuplicateSelectionStrategy.KeepShortestPath:
                    keeper = group.Files.OrderBy(f => f.FilePath.Length).FirstOrDefault();
                    break;
                case DuplicateSelectionStrategy.KeepLongestPath:
                    keeper = group.Files.OrderByDescending(f => f.FilePath.Length).FirstOrDefault();
                    break;
            }

            foreach (var file in group.Files)
            {
                // Never auto-select the keeper, and never auto-select protected files
                file.IsSelectedForDeletion = (file != keeper && !file.IsProtected);
            }
        }
    }

    private static string ComputeHeaderHash(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, HeaderBufferSize);
            byte[] buffer = new byte[HeaderBufferSize];
            int bytesRead = stream.Read(buffer, 0, HeaderBufferSize);
            if (bytesRead <= 0) return string.Empty;

            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(buffer, 0, bytesRead);
            return Convert.ToHexString(hash);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ComputeFullSha256(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536);
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(stream);
            return Convert.ToHexString(hash);
        }
        catch
        {
            return string.Empty;
        }
    }
}
