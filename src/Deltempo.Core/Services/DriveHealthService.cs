using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;

namespace WinTempCleaner.Models;

/// <summary>Physical media class reported by the Windows Storage provider.</summary>
public enum DriveMediaKind
{
    Unknown = 0,
    Hdd = 3,
    Ssd = 4,
    Scm = 5
}

/// <summary>Read-only drive report mode shared by both shells.</summary>
public enum DriveReportMode
{
    Health,
    SpeedTest
}

/// <summary>
/// One connected volume and the health facts Windows can prove about the physical
/// disk behind it.
///
/// Data sources, in order of trust:
/// 1. <c>MSFT_Disk</c> / <c>MSFT_PhysicalDisk</c> (root\Microsoft\Windows\Storage):
///    model, serial, bus, media type and <c>HealthStatus</c>. Health status is
///    available to a standard user.
/// 2. <c>MSFT_StorageReliabilityCounter</c>: temperature, wear, power-on hours and
///    uncorrected error counters. The provider denies this class to a standard user,
///    so <see cref="ReliabilityCountersAvailable"/> is false there and the affinity
///    fields stay null rather than being invented.
/// 3. <see cref="DriveInfo"/>: capacity and free space, always.
///
/// When the disk cannot be matched to the volume (Storage Spaces, some virtual
/// disks) the volume is still listed with <see cref="HealthStatus"/> "Unknown"
/// instead of a fabricated 100 %.
/// </summary>
public sealed class DriveHealthInfo
{
    public string DriveLetter { get; set; } = "C:";
    public string DisplayName { get; set; } = "C:";
    public string ModelName { get; set; } = "Unknown drive";
    public string Manufacturer { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string FirmwareVersion { get; set; } = string.Empty;
    public DriveMediaKind MediaKind { get; set; }
    public string BusType { get; set; } = "Unknown";
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public bool IsSystemDrive { get; set; }
    public bool IsRemovable { get; set; }

    /// <summary>0-100 wear-adjusted score, or null when Windows reports no verdict.</summary>
    public int? HealthScore { get; set; }

    /// <summary>"Healthy", "Warning", "Unhealthy" or "Unknown" - the provider's verdict.</summary>
    public string HealthStatus { get; set; } = "Unknown";

    public string HealthColor => HealthScore switch
    {
        null => "#94A3B8",
        >= 90 => "#10B981",
        >= 70 => "#F59E0B",
        _ => "#EF4444"
    };

    public double? TemperatureCelsius { get; set; }
    public int? WearPercent { get; set; }
    public long? PowerOnHours { get; set; }
    public long? ReadErrorsUncorrected { get; set; }
    public long? WriteErrorsUncorrected { get; set; }
    public bool ReliabilityCountersAvailable { get; set; }

    public string MediaType => MediaKind switch
    {
        DriveMediaKind.Hdd => "HDD",
        DriveMediaKind.Ssd => "SSD",
        DriveMediaKind.Scm => "SCM",
        _ => IsRemovable ? "Removable" : "Unknown media"
    };

    public string FormattedCapacity => TotalBytes > 0 ? TargetFolderInfo.FormatBytes(TotalBytes) : "Unavailable";

    public string FormattedFreeSpace => TotalBytes > 0
        ? $"{TargetFolderInfo.FormatBytes(FreeBytes)} free of {TargetFolderInfo.FormatBytes(TotalBytes)}"
        : "Unavailable";

    public string FormattedTemperature => TemperatureCelsius.HasValue
        ? $"{TemperatureCelsius.Value:F0} °C"
        : "Not reported";

    public string FormattedWear => WearPercent.HasValue
        ? $"{WearPercent.Value}% used"
        : "Not reported";

    public string FormattedPowerOnHours => PowerOnHours.HasValue
        ? $"{PowerOnHours.Value:N0} h"
        : "Not reported";

    public string Recommendation => HealthStatus switch
    {
        "Healthy" when WearPercent is >= 80 =>
            "Healthy but heavily worn. Keep backups current and plan a replacement.",
        "Healthy" =>
            "Windows reports the drive healthy. No action needed.",
        "Warning" =>
            "Windows reports a warning. Back up important data and check the drive soon.",
        "Unhealthy" =>
            "Windows reports the drive unhealthy. Back up immediately and replace it.",
        _ =>
            "Windows did not report health for this drive. It may be a virtual, network or removable device without SMART passthrough."
    };

    /// <summary>Human summary for a badge/tooltip: worst status across the machine.</summary>
    public static DriveHealthInfo? Worst(IReadOnlyList<DriveHealthInfo> drives) =>
        drives
            .Where(d => d.HealthScore.HasValue)
            .OrderBy(d => d.HealthScore!.Value)
            .FirstOrDefault();
}

/// <summary>One drive's measured sequential throughput. No estimates, no floors.</summary>
public sealed class DriveBenchmarkResult
{
    public string DriveLetter { get; set; } = "C:";
    public string DriveName { get; set; } = "C:";
    public string MediaType { get; set; } = "Unknown";
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public long TestSizeBytes { get; set; }
    public double WriteSpeedMBs { get; set; }
    public double ReadSpeedMBs { get; set; }
    public int ReadPasses { get; set; }
    public long ElapsedMs { get; set; }

    public string FormattedReadSpeed => Success && ReadSpeedMBs > 0 ? $"{ReadSpeedMBs:F0} MB/s" : "Unavailable";
    public string FormattedWriteSpeed => Success && WriteSpeedMBs > 0 ? $"{WriteSpeedMBs:F0} MB/s" : "Unavailable";
    public string FormattedTestSize => TestSizeBytes > 0 ? TargetFolderInfo.FormatBytes(TestSizeBytes) : "-";
}

public static partial class DriveHealthService
{
    private const string StorageNamespace = @"root\Microsoft\Windows\Storage";

    /// <summary>
    /// Enumerates every ready fixed and removable volume and reports the health
    /// facts Windows can prove about the physical disk behind each one.
    /// Never throws: an inaccessible provider degrades to DriveInfo-only rows.
    /// </summary>
    public static IReadOnlyList<DriveHealthInfo> GetAllDriveHealth()
    {
        Dictionary<string, uint> volumeToDisk = QueryVolumeDiskMap();
        Dictionary<uint, DiskRecord> disks = QueryDisks();

        var results = new List<DriveHealthInfo>();

        DriveInfo[] volumes;
        try
        {
            volumes = DriveInfo.GetDrives();
        }
        catch (Exception)
        {
            return results;
        }

        foreach (var volume in volumes)
        {
            try
            {
                if (!volume.IsReady) continue;
                if (volume.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;

                string letter = volume.Name.TrimEnd('\\');
                var info = new DriveHealthInfo
                {
                    DriveLetter = letter,
                    IsRemovable = volume.DriveType == DriveType.Removable,
                    TotalBytes = volume.TotalSize,
                    FreeBytes = volume.AvailableFreeSpace
                };

                if (volumeToDisk.TryGetValue(letter, out uint diskNumber) &&
                    disks.TryGetValue(diskNumber, out DiskRecord? disk))
                {
                    ApplyDisk(info, disk);
                }

                info.DisplayName = $"{letter} — {info.ModelName}";
                results.Add(info);
            }
            catch (Exception)
            {
                // A volume can disappear between enumeration and query (USB unplug).
                // Skip it rather than failing the whole report.
            }
        }

        return results
            .OrderByDescending(d => d.IsSystemDrive)
            .ThenBy(d => d.DriveLetter, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Compatibility accessor for one drive. Returns a DriveInfo-only row when the
    /// storage provider cannot map the volume, with health "Unknown".
    /// </summary>
    public static DriveHealthInfo GetDriveHealthSnapshot(string driveLetter = "C:")
    {
        ArgumentNullException.ThrowIfNull(driveLetter);

        string letter = NormalizeDriveLetter(driveLetter);
        var all = GetAllDriveHealth();
        var match = all.FirstOrDefault(d =>
            string.Equals(d.DriveLetter, letter, StringComparison.OrdinalIgnoreCase));
        if (match is not null) return match;

        var fallback = new DriveHealthInfo { DriveLetter = letter };
        try
        {
            var drive = new DriveInfo(letter + "\\");
            if (drive.IsReady)
            {
                fallback.TotalBytes = drive.TotalSize;
                fallback.FreeBytes = drive.AvailableFreeSpace;
                fallback.IsRemovable = drive.DriveType == DriveType.Removable;
                fallback.ModelName = drive.DriveType == DriveType.Removable
                    ? "Removable drive"
                    : "Storage device";
            }
        }
        catch (Exception)
        {
        }

        fallback.DisplayName = $"{letter} — {fallback.ModelName}";
        return fallback;
    }

    /// <summary>Worst health across all connected drives, for the hero button colour.</summary>
    public static DriveHealthInfo? GetWorstDriveHealth() => DriveHealthInfo.Worst(GetAllDriveHealth());

    private static string NormalizeDriveLetter(string driveLetter)
    {
        string letter = driveLetter.TrimEnd('\\');
        return letter.EndsWith(':') ? letter : letter + ":";
    }

    private sealed class DiskRecord
    {
        public uint Number { get; init; }
        public string Model { get; init; } = "Unknown drive";
        public string Manufacturer { get; init; } = string.Empty;
        public string SerialNumber { get; init; } = string.Empty;
        public string FirmwareVersion { get; init; } = string.Empty;
        public DriveMediaKind MediaKind { get; init; }
        public uint BusType { get; init; }
        public ushort HealthStatus { get; init; } = 5;
        public bool IsSystem { get; init; }
        public int? WearPercent { get; init; }
        public ushort? TemperatureCelsius { get; init; }
        public long? PowerOnHours { get; init; }
        public long? ReadErrorsUncorrected { get; init; }
        public long? WriteErrorsUncorrected { get; init; }
        public bool ReliabilityAvailable { get; init; }
    }

    private static Dictionary<string, uint> QueryVolumeDiskMap()
    {
        var map = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher(
                StorageNamespace,
                "SELECT DiskNumber, DriveLetter FROM MSFT_Partition");
            using ManagementObjectCollection partitions = searcher.Get();
            foreach (ManagementObject partition in partitions)
            {
                using (partition)
                {
                    object? letterValue = partition["DriveLetter"];
                    if (letterValue is null) continue;
                    string? letter = letterValue.ToString();
                    if (string.IsNullOrWhiteSpace(letter)) continue;

                    uint diskNumber = Convert.ToUInt32(partition["DiskNumber"], System.Globalization.CultureInfo.InvariantCulture);
                    map[$"{letter.Trim()}:"] = diskNumber;
                }
            }
        }
        catch (Exception)
        {
            // Provider unavailable: volumes still render from DriveInfo.
        }

        return map;
    }

    private static Dictionary<uint, DiskRecord> QueryDisks()
    {
        var mediaByDisk = QueryPhysicalMedia();
        var reliabilityByDisk = QueryReliabilityCounters();
        var disks = new Dictionary<uint, DiskRecord>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                StorageNamespace,
                "SELECT Number, FriendlyName, Model, Manufacturer, SerialNumber, FirmwareVersion, " +
                "HealthStatus, BusType, IsSystem FROM MSFT_Disk");
            using ManagementObjectCollection rows = searcher.Get();
            foreach (ManagementObject row in rows)
            {
                using (row)
                {
                    uint number = Convert.ToUInt32(row["Number"], System.Globalization.CultureInfo.InvariantCulture);
                    mediaByDisk.TryGetValue(number, out DriveMediaKind media);
                    reliabilityByDisk.TryGetValue(number, out ReliabilityRecord? reliability);

                    disks[number] = new DiskRecord
                    {
                        Number = number,
                        Model = FirstNonEmpty(row["Model"], row["FriendlyName"], "Unknown drive"),
                        Manufacturer = FirstNonEmpty(row["Manufacturer"], string.Empty),
                        SerialNumber = FirstNonEmpty(row["SerialNumber"], string.Empty).Trim(),
                        FirmwareVersion = FirstNonEmpty(row["FirmwareVersion"], string.Empty),
                        MediaKind = media,
                        BusType = ToUInt32(row["BusType"]),
                        HealthStatus = (ushort)ToUInt32(row["HealthStatus"], 5),
                        IsSystem = ToUInt32(row["IsSystem"]) != 0,
                        WearPercent = reliability?.WearPercent,
                        TemperatureCelsius = reliability?.TemperatureCelsius,
                        PowerOnHours = reliability?.PowerOnHours,
                        ReadErrorsUncorrected = reliability?.ReadErrorsUncorrected,
                        WriteErrorsUncorrected = reliability?.WriteErrorsUncorrected,
                        ReliabilityAvailable = reliability is not null
                    };
                }
            }
        }
        catch (Exception)
        {
        }

        return disks;
    }

    private static Dictionary<uint, DriveMediaKind> QueryPhysicalMedia()
    {
        var media = new Dictionary<uint, DriveMediaKind>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                StorageNamespace,
                "SELECT DeviceId, MediaType FROM MSFT_PhysicalDisk");
            using ManagementObjectCollection rows = searcher.Get();
            foreach (ManagementObject row in rows)
            {
                using (row)
                {
                    string? id = row["DeviceId"]?.ToString();
                    if (!uint.TryParse(id, out uint number)) continue;

                    uint mediaType = ToUInt32(row["MediaType"]);
                    if (Enum.IsDefined(typeof(DriveMediaKind), (int)mediaType))
                    {
                        media[number] = (DriveMediaKind)mediaType;
                    }
                }
            }
        }
        catch (Exception)
        {
        }

        return media;
    }

    private sealed class ReliabilityRecord
    {
        public int? WearPercent { get; init; }
        public ushort? TemperatureCelsius { get; init; }
        public long? PowerOnHours { get; init; }
        public long? ReadErrorsUncorrected { get; init; }
        public long? WriteErrorsUncorrected { get; init; }
    }

    private static Dictionary<uint, ReliabilityRecord> QueryReliabilityCounters()
    {
        var counters = new Dictionary<uint, ReliabilityRecord>();
        try
        {
            // Standard users get "Access to a CIM resource was not available" here;
            // the catch below keeps the row honest (fields stay null) instead of
            // guessing temperature or wear.
            using var searcher = new ManagementObjectSearcher(
                StorageNamespace,
                "SELECT DeviceId, Temperature, Wear, PowerOnHours, " +
                "ReadErrorsUncorrected, WriteErrorsUncorrected FROM MSFT_StorageReliabilityCounter");
            using ManagementObjectCollection rows = searcher.Get();
            foreach (ManagementObject row in rows)
            {
                using (row)
                {
                    string? id = row["DeviceId"]?.ToString();
                    if (!uint.TryParse(id, out uint number)) continue;

                    counters[number] = new ReliabilityRecord
                    {
                        TemperatureCelsius = ToNullableUShort(row["Temperature"]),
                        WearPercent = ToNullableInt(row["Wear"]),
                        PowerOnHours = ToNullableLong(row["PowerOnHours"]),
                        ReadErrorsUncorrected = ToNullableLong(row["ReadErrorsUncorrected"]),
                        WriteErrorsUncorrected = ToNullableLong(row["WriteErrorsUncorrected"])
                    };
                }
            }
        }
        catch (Exception)
        {
        }

        return counters;
    }

    private static void ApplyDisk(DriveHealthInfo info, DiskRecord disk)
    {
        info.ModelName = disk.Model;
        info.Manufacturer = disk.Manufacturer;
        info.SerialNumber = disk.SerialNumber;
        info.FirmwareVersion = disk.FirmwareVersion;
        info.MediaKind = disk.MediaKind;
        info.BusType = BusName(disk.BusType);
        info.IsSystemDrive = disk.IsSystem;
        info.ReliabilityCountersAvailable = disk.ReliabilityAvailable;
        info.TemperatureCelsius = disk.TemperatureCelsius;
        info.WearPercent = disk.WearPercent;
        info.PowerOnHours = disk.PowerOnHours;
        info.ReadErrorsUncorrected = disk.ReadErrorsUncorrected;
        info.WriteErrorsUncorrected = disk.WriteErrorsUncorrected;

        info.HealthStatus = disk.HealthStatus switch
        {
            0 => "Healthy",
            1 => "Warning",
            2 => "Unhealthy",
            _ => "Unknown"
        };

        int? statusScore = disk.HealthStatus switch
        {
            0 => 100,
            1 => 70,
            2 => 25,
            _ => null
        };

        if (statusScore.HasValue && disk.WearPercent.HasValue)
        {
            statusScore = Math.Min(statusScore.Value, Math.Clamp(100 - disk.WearPercent.Value, 0, 100));
        }

        info.HealthScore = statusScore;
    }

    private static string BusName(uint busType) => busType switch
    {
        1 => "SCSI",
        2 => "ATAPI",
        3 => "ATA",
        4 => "IEEE 1394",
        6 => "Fibre Channel",
        7 => "USB",
        8 => "RAID",
        9 => "iSCSI",
        10 => "SAS",
        11 => "SATA",
        12 => "SD",
        13 => "MMC",
        14 => "Virtual",
        15 => "File-backed virtual",
        16 => "Storage Spaces",
        17 => "NVMe",
        18 => "SCM",
        19 => "UFS",
        _ => "Unknown"
    };

    private static string FirstNonEmpty(object? primary, object? fallback, string emptyValue = "")
    {
        string? value = primary?.ToString();
        if (!string.IsNullOrWhiteSpace(value)) return value.Trim();

        value = fallback?.ToString();
        return string.IsNullOrWhiteSpace(value) ? emptyValue : value.Trim();
    }

    private static uint ToUInt32(object? value, uint fallback = 0)
    {
        if (value is null) return fallback;
        try
        {
            return Convert.ToUInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    private static int? ToNullableInt(object? value)
    {
        if (value is null) return null;
        try
        {
            return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static ushort? ToNullableUShort(object? value)
    {
        if (value is null) return null;
        try
        {
            return Convert.ToUInt16(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static long? ToNullableLong(object? value)
    {
        if (value is null) return null;
        try
        {
            return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
