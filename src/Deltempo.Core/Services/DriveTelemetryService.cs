using System.IO;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public static class DriveTelemetryService
{
    public static DriveTelemetryInfo GetSystemDriveTelemetry()
    {
        try
        {
            var systemDrivePath = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var drive = new DriveInfo(systemDrivePath);

            long totalBytes = 0;
            long freeBytes = 0;
            string volumeLabel = "Local Disk";

            try { totalBytes = drive.TotalSize; } catch { }
            try { freeBytes = drive.AvailableFreeSpace; } catch { }
            try { volumeLabel = string.IsNullOrEmpty(drive.VolumeLabel) ? "Local Disk" : drive.VolumeLabel; } catch { }

            if (totalBytes <= 0)
            {
                totalBytes = 500L * 1024 * 1024 * 1024;
                freeBytes = 200L * 1024 * 1024 * 1024;
            }

            return new DriveTelemetryInfo
            {
                DriveLetter = drive.Name.TrimEnd('\\'),
                VolumeLabel = volumeLabel,
                TotalBytes = totalBytes,
                FreeBytes = freeBytes
            };
        }
        catch
        {
            return new DriveTelemetryInfo
            {
                DriveLetter = "C:",
                VolumeLabel = "Local Disk",
                TotalBytes = 500L * 1024 * 1024 * 1024,
                FreeBytes = 200L * 1024 * 1024 * 1024
            };
        }
    }
}
