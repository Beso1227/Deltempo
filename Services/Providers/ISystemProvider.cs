using WinTempCleaner.Models;

namespace WinTempCleaner.Services.Providers;

public interface ISystemProvider
{
    DriveTelemetryInfo GetSystemDriveTelemetry();
    (long totalBytes, long freeBytes, double freePercent) GetDriveSpace(string driveLetter);
    (long totalMem, long freeMem, long usedMem, double usedPercent) GetMemoryMetrics();
    bool IsProcessProtected(string processName);
}
