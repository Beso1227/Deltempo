using System.IO;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services.Providers;

public class MockSystemProvider : ISystemProvider
{
    public long MockTotalBytes { get; set; } = 500L * 1024 * 1024 * 1024; // 500 GB
    public long MockFreeBytes { get; set; } = 45L * 1024 * 1024 * 1024;   // 45 GB (9% - Low Space Trigger)
    public long MockTotalMemory { get; set; } = 16L * 1024 * 1024 * 1024;  // 16 GB
    public long MockUsedMemory { get; set; } = 14L * 1024 * 1024 * 1024;   // 14 GB (87.5% - High RAM Trigger)

    public DriveTelemetryInfo GetSystemDriveTelemetry()
    {
        return new DriveTelemetryInfo
        {
            DriveLetter = "C:",
            VolumeLabel = "MockOS",
            TotalBytes = MockTotalBytes,
            FreeBytes = MockFreeBytes
        };
    }

    public (long totalBytes, long freeBytes, double freePercent) GetDriveSpace(string driveLetter)
    {
        double freePercent = MockTotalBytes > 0 ? ((double)MockFreeBytes / MockTotalBytes) * 100.0 : 0;
        return (MockTotalBytes, MockFreeBytes, freePercent);
    }

    public (long totalMem, long freeMem, long usedMem, double usedPercent) GetMemoryMetrics()
    {
        long freeMem = MockTotalMemory - MockUsedMemory;
        double usedPercent = MockTotalMemory > 0 ? ((double)MockUsedMemory / MockTotalMemory) * 100.0 : 0;
        return (MockTotalMemory, freeMem, MockUsedMemory, usedPercent);
    }

    public bool IsProcessProtected(string processName)
    {
        return ProcessOptimizerService.IsProtectedProcess(processName);
    }
}
