using System;
using System.Threading;
using System.Threading.Tasks;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

public class SystemRepairServiceTests
{
    [Theory]
    [InlineData("[==========================100.0%==========================]", 1.0)]
    [InlineData("[========== 40.0% ]", 0.40)]
    [InlineData("[= 5.5% ]", 0.055)]
    [InlineData("Verification 45% complete.", 0.45)]
    [InlineData("Verification 100% complete.", 1.0)]
    [InlineData("85% complete", 0.85)]
    public void ParseProgressFromLine_ValidFormats_ExtractsNormalizedFraction(string line, double expectedFraction)
    {
        double? result = SystemRepairService.ParseProgressFromLine(line);

        Assert.NotNull(result);
        Assert.Equal(expectedFraction, result.Value, 2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Deployment Image Servicing and Management tool")]
    [InlineData("Version: 10.0.26100.1150")]
    [InlineData("Windows Resource Protection did not find any integrity violations.")]
    public void ParseProgressFromLine_NonProgressLines_ReturnsNull(string line)
    {
        double? result = SystemRepairService.ParseProgressFromLine(line);

        Assert.Null(result);
    }

    [Fact]
    public async Task RunChkdskScanAsync_CancelledToken_AbortsCleanly()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Immediately cancelled

        var res = await SystemRepairService.RunChkdskScanAsync("C:", null, null, cts.Token);

        Assert.False(res.Success);
        Assert.Contains("cancelled", res.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunSfcScannowAsync_CancelledToken_ReturnsCancelledResult()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var res = await SystemRepairService.RunSfcScannowAsync(null, null, cts.Token);

        Assert.False(res.Success);
        Assert.Equal(-1, res.ExitCode);
        Assert.Contains("cancelled", res.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunDismScanHealthAsync_CancelledToken_ReturnsCancelledResult()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var res = await SystemRepairService.RunDismScanHealthAsync(null, null, cts.Token);

        Assert.False(res.Success);
        Assert.Equal(-1, res.ExitCode);
    }

    [Fact]
    public void ParseProgressFromLine_PercentWithBrackets_NormalizesCorrectly()
    {
        Assert.Equal(0.5, SystemRepairService.ParseProgressFromLine("[ 50.0% ]"));
        Assert.Equal(1.0, SystemRepairService.ParseProgressFromLine("[100%]"));
        Assert.Equal(0.0, SystemRepairService.ParseProgressFromLine("[0%]"));
    }

    [Fact]
    public async Task RunDismRestoreHealthAsync_CancelledToken_ReturnsCancelledResult()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var res = await SystemRepairService.RunDismRestoreHealthAsync(null, null, cts.Token);
        Assert.False(res.Success);
        Assert.Equal(-1, res.ExitCode);
    }

    [Fact]
    public async Task RunDismComponentCleanupAsync_CancelledToken_ReturnsCancelledResult()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var res = await SystemRepairService.RunDismComponentCleanupAsync(null, null, cts.Token);
        Assert.False(res.Success);
        Assert.Equal(-1, res.ExitCode);
    }

    [Fact]
    public async Task ResetWindowsUpdateStackAsync_CancelledToken_CompletesSafely()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var res = await SystemRepairService.ResetWindowsUpdateStackAsync(null, null, cts.Token);
        Assert.NotNull(res);
    }

    [Fact]
    public async Task ResetNetworkStackAsync_CancelledToken_CompletesSafely()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var res = await SystemRepairService.ResetNetworkStackAsync(null, null, cts.Token);
        Assert.NotNull(res);
    }

    [Fact]
    public async Task RunAutonomousHealthCheckAndRepairAsync_CancelledToken_CompletesSafely()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var res = await SystemRepairService.RunAutonomousHealthCheckAndRepairAsync(null, null, cts.Token);
        Assert.NotNull(res);
    }

    [Fact]
    public void RepairExecutionResult_MessageProperty_ReturnsAppropriateDescriptions()
    {
        var successResult = new RepairExecutionResult
        {
            Success = true,
            Tool = RepairToolType.SfcScan
        };
        Assert.Contains("completed successfully", successResult.Message);

        var failResult = new RepairExecutionResult
        {
            Success = false,
            ExitCode = 87,
            Tool = RepairToolType.DismScanHealth
        };
        Assert.Contains("completed with exit code 87", failResult.Message);

        var errorResult = new RepairExecutionResult
        {
            Success = false,
            ErrorMessage = "Custom error"
        };
        Assert.Equal("Custom error", errorResult.Message);
    }
}
