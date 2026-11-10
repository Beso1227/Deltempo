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

    [Fact]
    public void NvmeSmartLogPage_RealPage_ReportsTheFirmwareCounters()
    {
        // A page shaped like a real controller response: 313 K, 7 % used, 12345 h,
        // 2 media errors. The parser must read the specification's offsets, not
        // the offsets of some other log page.
        var page = new byte[512];
        page[1] = 313 & 0xFF;
        page[2] = 313 >> 8;
        page[5] = 7;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(page.AsSpan(128, 8), 12345);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(page.AsSpan(160, 8), 2);

        Assert.True(StorageSmartReader.TryParseNvmeSmartLog(page, out var health));
        Assert.True(health.HasValue);
        var value = health!.Value;

        Assert.InRange(value.TemperatureCelsius, 39.8, 39.9);
        Assert.Equal(7, value.WearPercent);
        Assert.Equal(12345, value.PowerOnHours);
        Assert.Equal(2, value.MediaErrors);
    }

    [Fact]
    public void NvmeSmartLogPage_AllZeroPage_IsRejectedInsteadOfReportedAsZero()
    {
        var page = new byte[512];

        Assert.False(StorageSmartReader.TryParseNvmeSmartLog(page, out var health));
        Assert.False(health.HasValue);
    }

    [Fact]
    public void NvmeSmartLogPage_ImpossibleTemperature_IsRejected()
    {
        // 0 K is not a temperature a controller reports; it means the read failed.
        var page = new byte[512];
        page[5] = 3;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(page.AsSpan(128, 8), 500);

        Assert.False(StorageSmartReader.TryParseNvmeSmartLog(page, out var health));
        Assert.False(health.HasValue);
    }

    [Fact]
    public void SmartReadOutcome_UnsupportedDriverQuery_IsNotTreatedAsAnElevationProblem()
    {
        // Intel RST VMD answers the documented NVMe property query with
        // ERROR_INVALID_FUNCTION (1) and the pass-through with ERROR_NOT_SUPPORTED
        // (50). Elevation cannot make an unimplemented query work, so the report must
        // say the driver does not expose the log instead of offering a UAC prompt.
        Assert.Equal(SmartReadOutcome.NotSupported, StorageSmartReader.Classify(new[] { 1, 50 }));
        Assert.Equal(SmartReadOutcome.NotSupported, StorageSmartReader.Classify(new[] { 1, 1, 5 }));
    }

    [Fact]
    public void SmartReadOutcome_AccessDenied_StillOffersElevation()
    {
        // Only a refusal is something elevation is documented to fix; any other error
        // must stay a plain failure rather than claiming a permission problem.
        Assert.Equal(SmartReadOutcome.AccessDenied, StorageSmartReader.Classify(new[] { 5, 5, 5 }));
        Assert.Equal(SmartReadOutcome.Failed, StorageSmartReader.Classify(new[] { 87 }));
    }

    [Fact]
    public void SmartAttributeTable_RealShapedPage_ReportsTemperatureWearAndPowerOnHours()
    {
        // Shaped like the table an Intel RST VMD-managed NVMe disk returns through
        // root\wmi: 190 = 39 °C (normalized = 100 - raw), 9 = 3808 h, 231 = 96 %
        // remaining life (so 4 % used). The parser must read these exact fields.
        var page = new byte[362];
        WriteSmartAttribute(page, 0, id: 190, normalized: 61, raw: 39);
        WriteSmartAttribute(page, 1, id: 9, normalized: 100, raw: 3808);
        WriteSmartAttribute(page, 2, id: 231, normalized: 96, raw: 0);

        Assert.True(StorageSmartReader.TryParseSmartAttributes(page, out WmiSmartCounters counters));

        Assert.Equal(39d, counters.TemperatureCelsius);
        Assert.Equal(4, counters.WearPercent);
        Assert.Equal(3808, counters.PowerOnHours);
    }

    [Fact]
    public void SmartAttributeTable_AllZeroPage_IsRejectedInsteadOfReportedAsZero()
    {
        Assert.False(StorageSmartReader.TryParseSmartAttributes(new byte[362], out WmiSmartCounters counters));
        Assert.Null(counters.TemperatureCelsius);
        Assert.Null(counters.WearPercent);
        Assert.Null(counters.PowerOnHours);
    }

    [Fact]
    public void SmartAttributeTable_AttributeFive_IsNotTreatedAsWear()
    {
        // Attribute 5 officially means "Reallocated Sector Count". Some drivers
        // synthesize a value for it on NVMe, but it cannot be proven to be
        // Percentage Used, so a table carrying only attribute 5 has no wear to report.
        var page = new byte[362];
        WriteSmartAttribute(page, 0, id: 5, normalized: 86, raw: 0);

        Assert.False(StorageSmartReader.TryParseSmartAttributes(page, out WmiSmartCounters counters));
        Assert.Null(counters.WearPercent);
    }

    [Fact]
    public void SmartAttributeTable_Attribute194_IsUsedWhen190IsAbsent()
    {
        var page = new byte[362];
        WriteSmartAttribute(page, 0, id: 194, normalized: 55, raw: 45);

        Assert.True(StorageSmartReader.TryParseSmartAttributes(page, out WmiSmartCounters counters));
        Assert.Equal(45d, counters.TemperatureCelsius);
        Assert.Null(counters.WearPercent);
    }

    private static void WriteSmartAttribute(byte[] page, int index, byte id, byte normalized, uint raw)
    {
        int at = 2 + (index * 12);
        page[at] = id;
        page[at + 3] = normalized;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(at + 5, 4), raw);
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
