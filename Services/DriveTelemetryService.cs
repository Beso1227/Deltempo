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

            return new DriveTelemetryInfo
            {
                DriveLetter = drive.Name.TrimEnd('\\'),
                VolumeLabel = string.IsNullOrEmpty(drive.VolumeLabel) ? "Windows OS" : drive.VolumeLabel,
                TotalBytes = drive.TotalSize,
                FreeBytes = drive.AvailableFreeSpace
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
