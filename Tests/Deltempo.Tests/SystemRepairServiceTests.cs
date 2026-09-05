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
}
