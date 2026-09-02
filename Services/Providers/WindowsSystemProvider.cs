using System.IO;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services.Providers;

public class WindowsSystemProvider : ISystemProvider
{
    public DriveTelemetryInfo GetSystemDriveTelemetry()
    {
        return DriveTelemetryService.GetSystemDriveTelemetry();
    }

    public (long totalBytes, long freeBytes, double freePercent) GetDriveSpace(string driveLetter)
    {
        try
        {
            var drive = new DriveInfo(driveLetter);
            if (drive.IsReady)
            {
                double freePercent = drive.TotalSize > 0 ? ((double)drive.AvailableFreeSpace / drive.TotalSize) * 100.0 : 0;
                return (drive.TotalSize, drive.AvailableFreeSpace, freePercent);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }

        return (0, 0, 0);
    }

    public (long totalMem, long freeMem, long usedMem, double usedPercent) GetMemoryMetrics()
    {
        var mem = MemoryOptimizerService.GetMemoryInfo();
        return (mem.TotalPhysicalBytes, mem.AvailablePhysicalBytes, mem.UsedPhysicalBytes, mem.UsedPercent);
    }

    public bool IsProcessProtected(string processName)
    {
        return ProcessOptimizerService.IsProtectedProcess(processName);
    }
}
