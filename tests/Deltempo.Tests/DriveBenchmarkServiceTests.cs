using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinTempCleaner.Models;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Covers the honest-data contract of the drive report:
///   * every connected volume is listed, and a fact Windows refuses to give is
///     left null instead of being invented;
///   * the speed test reports measured numbers, or an explicit failure - never a
///     fabricated floor;
///   * the temporary test file does not survive, on any path.
/// </summary>
public class DriveBenchmarkServiceTests
{
    [Fact]
    public void GetAllDriveHealth_ReturnsConnectedDrives_WithoutFabricatedCounters()
    {
        var drives = DriveHealthService.GetAllDriveHealth();

        Assert.NotEmpty(drives);
        Assert.All(drives, d =>
        {
            Assert.False(string.IsNullOrWhiteSpace(d.DriveLetter));
            Assert.Matches(@"^[A-Za-z]:$", d.DriveLetter);
            Assert.False(string.IsNullOrWhiteSpace(d.ModelName));
            Assert.Contains(d.HealthStatus, new[] { "Healthy", "Warning", "Unhealthy", "Unknown" });

            if (d.HealthScore.HasValue)
            {
                Assert.InRange(d.HealthScore.Value, 0, 100);
            }

            // The one invariant that matters most: when the provider denied the
            // reliability counters, the row must say so and keep the fields null.
            if (!d.ReliabilityCountersAvailable)
            {
                Assert.Null(d.TemperatureCelsius);
                Assert.Null(d.WearPercent);
                Assert.Null(d.PowerOnHours);
            }
        });

        // The OS drive is always present and is sorted first.
        Assert.Contains(drives, d => d.IsSystemDrive || d.DriveLetter == "C:");
    }

    [Fact]
    public void GetDriveHealthSnapshot_SystemDrive_AgreesWithTheFullReport()
    {
        var all = DriveHealthService.GetAllDriveHealth();
        var system = all.FirstOrDefault(d => d.IsSystemDrive) ?? all[0];

        var snapshot = DriveHealthService.GetDriveHealthSnapshot(system.DriveLetter);

        Assert.Equal(system.DriveLetter, snapshot.DriveLetter);
        Assert.Equal(system.HealthStatus, snapshot.HealthStatus);
        Assert.Equal(system.HealthScore, snapshot.HealthScore);
        Assert.Equal(system.TotalBytes, snapshot.TotalBytes);
    }

    [Fact]
    public void GetWorstDriveHealth_ReturnsTheLowestScoredDrive()
    {
        var all = DriveHealthService.GetAllDriveHealth();
        var worst = DriveHealthService.GetWorstDriveHealth();

        if (all.Any(d => d.HealthScore.HasValue))
        {
            Assert.NotNull(worst);
            Assert.Equal(all.Where(d => d.HealthScore.HasValue).Min(d => d.HealthScore!.Value), worst!.HealthScore);
        }
        else
        {
            Assert.Null(worst);
        }
    }

    [Fact]
    public async Task RunReadWriteBenchmark_MeasuresRealThroughputAndDeletesTheTempFile()
    {
        string root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        string pattern = "DeltempoSpeedTest_*.tmp";
        var before = Directory.GetFiles(root, pattern);

        var result = await DriveHealthService.RunReadWriteBenchmarkAsync(
            "C:", DriveBenchmarkOptions.Quick, CancellationToken.None);

        Assert.Equal("C:", result.DriveLetter);
        Assert.True(result.ElapsedMs >= 0);

        if (result.Success)
        {
            Assert.True(result.ReadSpeedMBs > 0, "a successful run must report a measured read speed");
            Assert.True(result.WriteSpeedMBs > 0, "a successful run must report a measured write speed");
            Assert.True(result.ReadPasses >= 1);
            Assert.Equal(DriveBenchmarkOptions.Quick.FileSizeBytes, result.TestSizeBytes);
        }
        else
        {
            // A refusal is allowed (disk full, locked volume, virtual disk), but it
            // must explain itself rather than report a made-up number.
            Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
            Assert.Equal(0, result.ReadSpeedMBs);
        }

        var after = Directory.GetFiles(root, pattern);
        Assert.Empty(after.Except(before));
    }

    [Fact]
    public async Task RunReadWriteBenchmark_MissingDrive_FailsClosed()
    {
        var result = await DriveHealthService.RunReadWriteBenchmarkAsync(
            "Z:", DriveBenchmarkOptions.Quick, CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.Equal(0, result.ReadSpeedMBs);
        Assert.Equal(0, result.WriteSpeedMBs);
    }

    [Fact]
    public async Task RunReadWriteBenchmark_InvalidOptions_FailsClosed()
    {
        var bad = new DriveBenchmarkOptions(FileSizeBytes: 0, ReadPasses: 0, ChunkSizeBytes: 0);

        var result = await DriveHealthService.RunReadWriteBenchmarkAsync("C:", bad, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Invalid benchmark options.", result.ErrorMessage);
    }

    [Fact]
    public async Task RunBenchmarkForAllDrives_ReportsOneRowPerConnectedDrive()
    {
        var expected = DriveHealthService.GetAllDriveHealth().Count;

        var results = await DriveHealthService.RunBenchmarkForAllDrivesAsync(
            DriveBenchmarkOptions.Quick, ct: CancellationToken.None);

        Assert.Equal(expected, results.Count);
        Assert.All(results, r => Assert.False(string.IsNullOrWhiteSpace(r.DriveName)));
    }
}
