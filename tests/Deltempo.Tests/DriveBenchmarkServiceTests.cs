using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinTempCleaner.Models;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Covers the honest-data contract of the drive report:
///   * every physical storage device is listed once, with its volume letters, and
///     a fact Windows refuses to give is left null instead of being invented;
///   * the speed test reports measured numbers, or an explicit failure - never a
///     fabricated floor - and tests each device once, not once per volume;
///   * the temporary test file does not survive, on any path.
/// </summary>
public class DriveBenchmarkServiceTests
{
    [Fact]
    public void GetAllDriveHealth_ReturnsOneRowPerPhysicalDevice_WithoutFabricatedCounters()
    {
        var drives = DriveHealthService.GetAllDriveHealth();

        Assert.NotEmpty(drives);
        Assert.All(drives, d =>
        {
            Assert.False(string.IsNullOrWhiteSpace(d.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(d.ModelName));
            Assert.False(string.IsNullOrWhiteSpace(d.VolumesLabel));
            Assert.Contains(d.HealthStatus, new[] { "Healthy", "Warning", "Unhealthy", "Unknown" });
            Assert.False(string.IsNullOrWhiteSpace(d.HealthScoreBasis));

            // Elevation only "helps" when the provider actually denied access.
            if (d.ReliabilityCountersNeedElevation)
            {
                Assert.False(d.ReliabilityCountersAvailable);
            }

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

            if (d.HasMountedVolume)
            {
                Assert.NotEmpty(d.VolumeLetters);
                Assert.All(d.VolumeLetters, letter => Assert.Matches(@"^[A-Za-z]:$", letter));
                Assert.Contains(d.DriveLetter, d.VolumeLetters);
            }
            else
            {
                Assert.Empty(d.VolumeLetters);
                Assert.Equal(string.Empty, d.DriveLetter);
                Assert.Equal("No mounted volume", d.FormattedFreeSpace);
            }
        });

        // The OS device is always present and is sorted first.
        Assert.Contains(drives, d => d.IsSystemDrive);
        Assert.True(drives[0].IsSystemDrive);

        // A disk number never appears twice: two volumes on one SSD are one row.
        var diskNumbers = drives
            .Where(d => d.DiskNumber.HasValue)
            .Select(d => d.DiskNumber!.Value)
            .ToList();
        Assert.Equal(diskNumbers.Count, diskNumbers.Distinct().Count());

        // Every ready fixed/removable volume belongs to exactly one device row.
        foreach (string letter in ReadyVolumeLetters())
        {
            Assert.Single(drives, d =>
                d.VolumeLetters.Contains(letter, StringComparer.OrdinalIgnoreCase));
        }

        // The 0-100 score never pretends to include wear it did not receive.
        var verdictOnly = new DriveHealthInfo { HealthStatus = "Healthy", HealthScore = 100 };
        Assert.Contains("verdict", verdictOnly.HealthScoreBasis, StringComparison.Ordinal);
        Assert.Contains("wear is not reported", verdictOnly.HealthScoreBasis, StringComparison.Ordinal);
        verdictOnly.WearPercent = 4;
        verdictOnly.HealthScore = 96;
        Assert.Contains("reported wear", verdictOnly.HealthScoreBasis, StringComparison.Ordinal);
    }

    [Fact]
    public void GetDriveHealthSnapshot_VolumeOnADisk_ReturnsThatDevicesRow()
    {
        string letter = ReadyVolumeLetters().First();
        var all = DriveHealthService.GetAllDriveHealth();
        var owner = all.Single(d =>
            d.VolumeLetters.Contains(letter, StringComparer.OrdinalIgnoreCase));

        var snapshot = DriveHealthService.GetDriveHealthSnapshot(letter);

        Assert.Equal(owner.DiskNumber, snapshot.DiskNumber);
        Assert.Equal(owner.DisplayName, snapshot.DisplayName);
        Assert.Equal(owner.HealthStatus, snapshot.HealthStatus);
        Assert.Equal(owner.HealthScore, snapshot.HealthScore);
        Assert.Equal(owner.TotalBytes, snapshot.TotalBytes);
        Assert.Contains(letter, snapshot.VolumeLetters);
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

        // Two read passes, the shipping default: the second pass must rewind the
        // unbuffered handle instead of reading zero bytes at EOF.
        var options = new DriveBenchmarkOptions(FileSizeBytes: 16L * 1024 * 1024, ReadPasses: 2);

        var result = await DriveHealthService.RunReadWriteBenchmarkAsync(
            "C:", options, CancellationToken.None);

        Assert.Equal("C:", result.DriveLetter);
        Assert.True(result.ElapsedMs >= 0);

        if (result.Success)
        {
            Assert.True(result.ReadSpeedMBs > 0, "a successful run must report a measured read speed");
            Assert.True(result.WriteSpeedMBs > 0, "a successful run must report a measured write speed");
            Assert.Equal(2, result.ReadPasses);
            Assert.Equal(options.FileSizeBytes, result.TestSizeBytes);
        }
        else
        {
            // A refusal is allowed (disk full, locked volume, virtual disk), but it
            // must explain itself rather than report a made-up number - and it must
            // never be the truncated-file false positive the rewind bug produced.
            Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
            Assert.DoesNotContain("fewer bytes", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
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
    public async Task RunBenchmarkForAllDrives_ReportsOneRowPerPhysicalDevice()
    {
        var devices = DriveHealthService.GetAllDriveHealth();

        var results = await DriveHealthService.RunBenchmarkForAllDrivesAsync(
            DriveBenchmarkOptions.Quick, ct: CancellationToken.None);

        Assert.Equal(devices.Count, results.Count);
        Assert.Equal(
            devices.Select(d => d.DiskNumber).ToList(),
            results.Select(r => r.DiskNumber).ToList());
        Assert.All(results, r => Assert.False(string.IsNullOrWhiteSpace(r.DriveName)));

        foreach (var (device, result) in devices.Zip(results))
        {
            if (device.HasMountedVolume)
            {
                // Tested once, through the volume the report chose for that device.
                Assert.Equal(device.DriveLetter, result.DriveLetter);
                Assert.Equal(device.DisplayName, result.DriveName);
            }
            else
            {
                Assert.False(result.Success);
                Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
            }
        }
    }

    private static List<string> ReadyVolumeLetters()
    {
        var letters = new List<string>();
        foreach (var volume in DriveInfo.GetDrives())
        {
            try
            {
                if (!volume.IsReady) continue;
                if (volume.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;
                letters.Add(volume.Name.TrimEnd('\\'));
            }
            catch (Exception)
            {
            }
        }

        return letters;
    }
}
