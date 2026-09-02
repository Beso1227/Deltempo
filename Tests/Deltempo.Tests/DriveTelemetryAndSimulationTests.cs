using WinTempCleaner.Models;
using WinTempCleaner.Services.Providers;
using Xunit;

namespace Deltempo.Tests;

public class DriveTelemetryAndSimulationTests
{
    [Fact]
    public void MockSystemProvider_SimulatesLowDiskSpaceTrigger()
    {
        // Arrange
        var mockProvider = new MockSystemProvider
        {
            MockTotalBytes = 500L * 1024 * 1024 * 1024,
            MockFreeBytes = 35L * 1024 * 1024 * 1024 // 7% free (Low Space < 15%)
        };

        // Act
        var telemetry = mockProvider.GetSystemDriveTelemetry();

        // Assert
        Assert.NotNull(telemetry);
        Assert.True(telemetry.IsLowSpace, "Should trigger low space flag when free percentage is 7%");
        Assert.Equal(7.0, telemetry.FreePercentage, 1);
    }

    [Fact]
    public void MockSystemProvider_SimulatesHighMemoryUsage()
    {
        // Arrange
        var mockProvider = new MockSystemProvider
        {
            MockTotalMemory = 32L * 1024 * 1024 * 1024,
            MockUsedMemory = 28L * 1024 * 1024 * 1024 // 87.5% used
        };

        // Act
        var (total, free, used, usedPercent) = mockProvider.GetMemoryMetrics();

        // Assert
        Assert.True(usedPercent > 80.0, "Used RAM should exceed 80% threshold");
        Assert.Equal(87.5, usedPercent, 1);
    }

    [Theory]
    [InlineData(500, "500.0 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(1048576, "1.0 MB")]
    [InlineData(1073741824, "1.0 GB")]
    public void FormatBytes_FormatsAccurately(long bytes, string expected)
    {
        // Act
        string formatted = TargetFolderInfo.FormatBytes(bytes);

        // Assert
        Assert.Equal(expected, formatted);
    }
}
