using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Blake3;
using WinTempCleaner.Core.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Phase 1 hot-path benchmarks: end-to-end duplicate scanning and the staged
/// content-hash path (size -> partial -> full), which moved from SHA-256 to BLAKE3-256.
/// Excluded from CI by default — run explicitly with:
///   dotnet test --filter "Category=Benchmark" -c Release
/// Assertions verify correctness only — never timing — so accidental CI inclusion
/// cannot produce flaky failures.
/// </summary>
public class DuplicateScanBenchmarks
{
    private const int ChunkSize = 81920;

    private readonly ITestOutputHelper _output;

    public DuplicateScanBenchmarks(ITestOutputHelper output) => _output = output;

    [Fact]
    [Trait("Category", "Benchmark")]
    public async Task DuplicateScan_SyntheticCorpus_ReportsThroughput()
    {
        const int uniqueCount = 300;
        const int duplicatePairs = 50;
        const int payloadSize = 64 * 1024;

        string sandbox = Path.Combine(Path.GetTempPath(), "Deltempo_DupBench_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);
        try
        {
            var random = new Random(20261008); // DevSkim: ignore DS148264

            // Same-size unique payloads force every candidate through the full-hash stage,
            // which is exactly the path Phase 1 retargeted to BLAKE3.
            for (int i = 0; i < uniqueCount; i++)
            {
                var unique = new byte[payloadSize];
                random.NextBytes(unique);
                File.WriteAllBytes(Path.Combine(sandbox, $"unique_{i}.dat"), unique);
            }

            for (int i = 0; i < duplicatePairs; i++)
            {
                var payload = new byte[payloadSize];
                random.NextBytes(payload);
                string original = Path.Combine(sandbox, $"dup_{i}_original.dat");
                string copy = Path.Combine(sandbox, $"dup_{i}_copy.dat");
                File.WriteAllBytes(original, payload);
                File.WriteAllBytes(copy, payload);
            }

            var stopwatch = Stopwatch.StartNew();
            var result = await DuplicateFileService.FindDuplicatesAsync([sandbox], minFileSizeBytes: 1);
            stopwatch.Stop();

            double seconds = Math.Max(stopwatch.Elapsed.TotalSeconds, 0.000001);
            _output.WriteLine(
                $"Scanned {result.TotalScannedFiles:N0} files in {stopwatch.Elapsed.TotalSeconds:F2}s " +
                $"({result.TotalScannedFiles / seconds:N0} files/s)");
            _output.WriteLine($"Duplicate groups: {result.Groups.Count:N0} | Wasted: {result.TotalWastedBytes:N0} bytes");

            // Correctness only (never timing)
            Assert.Equal(duplicatePairs, result.Groups.Count);
            Assert.Equal(duplicatePairs, result.TotalDuplicateFilesCount);
            Assert.All(result.Groups, g => Assert.Equal(2, g.Files.Count));
            Assert.All(result.Groups, g => Assert.False(string.IsNullOrEmpty(g.Sha256Hash)));
        }
        finally
        {
            try { Directory.Delete(sandbox, true); } catch { /* best effort cleanup */ }
        }
    }

    [Fact]
    [Trait("Category", "Benchmark")]
    public void ContentHash_Blake3ComparedToSha256_ReportsThroughput()
    {
        const int payloadSize = 64 * 1024 * 1024;
        const int iterations = 5;
        var payload = new byte[payloadSize];
        new Random(20261008).NextBytes(payload); // DevSkim: ignore DS148264

        // Warm up JIT (including runtime SIMD path selection) so first-call compilation
        // cost does not dominate the measurement.
        WarmUpSha256(payload);
        WarmUpBlake3(payload);

        var sha256Times = new double[iterations]; // DevSkim: ignore DS197836
        var blake3Times = new double[iterations];
        string sha256Hex = string.Empty;
        string blake3Hex = string.Empty;

        for (int i = 0; i < iterations; i++)
        {
            var shaWatch = Stopwatch.StartNew();
            using (var stream = new MemoryStream(payload, writable: false))
            using (var sha256 = SHA256.Create())
            {
                sha256Hex = Convert.ToHexString(sha256.ComputeHash(stream));
            }
            shaWatch.Stop();
            sha256Times[i] = shaWatch.Elapsed.TotalMilliseconds; // DevSkim: ignore DS197836

            var blakeWatch = Stopwatch.StartNew();
            using (var stream = new MemoryStream(payload, writable: false))
            using (var hasher = Hasher.New())
            {
                var buffer = new byte[ChunkSize];
                int bytesRead;
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    hasher.Update(buffer.AsSpan(0, bytesRead));
                }
                blake3Hex = Convert.ToHexString(hasher.Finalize().AsSpan());
            }
            blakeWatch.Stop();
            blake3Times[i] = blakeWatch.Elapsed.TotalMilliseconds;
        }

        double sha256Ms = Median(sha256Times); // DevSkim: ignore DS197836
        double blake3Ms = Median(blake3Times);
        double megabytes = payloadSize / (1024.0 * 1024.0);

        _output.WriteLine($"SHA-256 : {megabytes / Math.Max(sha256Ms / 1000.0, 0.000001):F1} MB/s ({sha256Ms:F1} ms, median of {iterations})");
        _output.WriteLine($"BLAKE3  : {megabytes / Math.Max(blake3Ms / 1000.0, 0.000001):F1} MB/s ({blake3Ms:F1} ms, median of {iterations})");
        _output.WriteLine($"Speedup : {sha256Ms / Math.Max(blake3Ms, 0.0001):F2}x (>1.0x means BLAKE3 is faster)");

        // The partial (header) stage hashes many small buffers, so per-call cost dominates
        // there. Measure the 4 KB case separately — that is the highest-call-count path.
        const int headerSize = 4096;
        const int headerIterations = 2000;
        var header = new byte[headerSize];
        new Random(20261008).NextBytes(header); // DevSkim: ignore DS148264
        _ = Convert.ToHexString(SHA256.HashData(header));
        using (var warmup = Hasher.New()) { warmup.Update(header); _ = warmup.Finalize(); }

        double sha256HeaderMs;
        var shaHeaderWatch = Stopwatch.StartNew();
        for (int i = 0; i < headerIterations; i++)
        {
            _ = Convert.ToHexString(SHA256.HashData(header));
        }
        shaHeaderWatch.Stop();
        sha256HeaderMs = shaHeaderWatch.Elapsed.TotalMilliseconds;

        double blake3HeaderMs;
        var blakeHeaderWatch = Stopwatch.StartNew();
        for (int i = 0; i < headerIterations; i++)
        {
            using var hasher = Hasher.New();
            hasher.Update(header);
            _ = Convert.ToHexString(hasher.Finalize().AsSpan());
        }
        blakeHeaderWatch.Stop();
        blake3HeaderMs = blakeHeaderWatch.Elapsed.TotalMilliseconds;

        _output.WriteLine(
            $"4KB header stage ({headerIterations:N0} ops) — SHA-256: {sha256HeaderMs:F1} ms | " +
            $"BLAKE3: {blake3HeaderMs:F1} ms | Speedup: {sha256HeaderMs / Math.Max(blake3HeaderMs, 0.0001):F2}x");

        // Correctness only (never timing)
        Assert.Equal(64, sha256Hex.Length);
        Assert.Equal(64, blake3Hex.Length);
        Assert.NotEqual(sha256Hex, blake3Hex);

        // Both algorithms must be deterministic for identical input.
        Assert.Equal(sha256Hex, Convert.ToHexString(SHA256.HashData(payload)));
        using (var hasher = Hasher.New())
        {
            hasher.Update(payload);
            Assert.Equal(blake3Hex, Convert.ToHexString(hasher.Finalize().AsSpan()));
        }
    }

    private static void WarmUpSha256(byte[] payload)
    {
        using var sha256 = SHA256.Create();
        _ = sha256.ComputeHash(payload, 0, 1024 * 1024);
    }

    private static void WarmUpBlake3(byte[] payload)
    {
        using var hasher = Hasher.New();
        hasher.Update(payload.AsSpan(0, 1024 * 1024));
        _ = hasher.Finalize();
    }

    private static double Median(double[] values)
    {
        var sorted = (double[])values.Clone();
        Array.Sort(sorted);
        return sorted[sorted.Length / 2];
    }
}
