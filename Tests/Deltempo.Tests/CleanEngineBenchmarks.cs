using System.Diagnostics;
using System.IO;
using WinTempCleaner.Models;
using WinTempCleaner.Services;
using Xunit;
using Xunit.Abstractions;

namespace Deltempo.Tests;

/// <summary>
/// Reproducible performance benchmarks for the parallel cleaning engine.
/// Excluded from CI by default — run explicitly with:
///   dotnet test --filter "Category=Benchmark" -c Release
/// or via scripts/benchmark.ps1.
/// Assertions verify correctness only — never timing — so accidental CI
/// inclusion cannot produce flaky failures.
/// </summary>
public class CleanEngineBenchmarks
{
    private readonly ITestOutputHelper _output;

    public CleanEngineBenchmarks(ITestOutputHelper output) => _output = output;

    [Fact]
    [Trait("Category", "Benchmark")]
    public async Task CleanFolderAsync_ParallelDeletion_Throughput()
    {
        const int fileCount = 1500;
        const int fileSizeBytes = 8 * 1024;

        string sandbox = Path.Combine(Path.GetTempPath(), "Deltempo_Bench_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);
        try
        {
            // Arrange: stale files (48h old) across 50 subfolders to exercise
            // directory traversal + the bounded parallel deletion pool.
            var payload = new byte[fileSizeBytes];
            for (int i = 0; i < fileCount; i++)
            {
                string dir = Path.Combine(sandbox, "sub" + (i % 50));
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "bench_" + i + ".tmp");
                File.WriteAllBytes(path, payload);
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow - TimeSpan.FromHours(48));
            }

            var target = new TargetFolderInfo
            {
                Id = "BenchmarkSandbox",
                Name = "Benchmark Sandbox",
                Category = "Benchmark",
                FolderPath = sandbox
            };

            // Act
            var cleaner = new CleanerService();
            var sw = Stopwatch.StartNew();
            var (freed, filesDeleted, foldersDeleted, filesSkipped) = await cleaner.CleanFolderAsync(
                target,
                safeMode24Hours: false,
                logAction: (_, _) => { },
                progressReport: _ => { },
                ct: CancellationToken.None);
            sw.Stop();

            double seconds = Math.Max(sw.Elapsed.TotalSeconds, 0.000001);
            double filesPerSec = filesDeleted / seconds;
            double mbPerSec = freed / (1024.0 * 1024.0) / seconds;

            _output.WriteLine($"Cleaned {filesDeleted:N0} files ({TargetFolderInfo.FormatBytes(freed)}, {foldersDeleted} folders) in {sw.Elapsed.TotalSeconds:F2}s");
            _output.WriteLine($"Throughput: {filesPerSec:N0} files/s | {mbPerSec:F1} MB/s");

            // Assert: correctness only (never timing)
            Assert.Equal(fileCount, filesDeleted + filesSkipped);
            Assert.True(freed > 0, "Expected nonzero bytes reclaimed.");
            Assert.Empty(Directory.EnumerateFiles(sandbox, "*", SearchOption.AllDirectories));
        }
        finally
        {
            try { Directory.Delete(sandbox, true); } catch { }
        }
    }
}
