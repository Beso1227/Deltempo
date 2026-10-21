using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WinTempCleaner.Models;
using Xunit;

namespace Deltempo.Tests;

public class DriveBenchmarkServiceTests
{
    [Fact]
    public void GetDriveHealthSnapshot_ValidSystemDrive_ReturnsHealthObject()
    {
        var health = DriveHealthService.GetDriveHealthSnapshot("C:");
        Assert.NotNull(health);
        Assert.False(string.IsNullOrWhiteSpace(health.DriveLetter));
        Assert.False(string.IsNullOrWhiteSpace(health.HealthStatus));
        Assert.InRange(health.HealthScore, 0, 100);
    }

    [Fact]
    public async Task RunQuickReadBenchmarkAsync_ExecutesSafelyAndReturnsSpeed()
    {
        var result = await DriveHealthService.RunQuickReadBenchmarkAsync("C:", null, CancellationToken.None);
        Assert.NotNull(result);
        Assert.True(result.ReadSpeedMBs >= 0);
        Assert.True(result.ExecutionTimeMs >= 0);
    }
}
