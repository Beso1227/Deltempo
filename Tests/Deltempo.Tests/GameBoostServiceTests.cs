using System;
using System.Threading;
using System.Threading.Tasks;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

public class GameBoostServiceTests
{
    [Fact]
    public void ExtractPowerSchemeGuid_ValidPowerCfgOutput_ExtractsGuidCorrectly()
    {
        string sampleOutput = "Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)";
        string guid = GameBoostService.ExtractPowerSchemeGuid(sampleOutput);
        Assert.Equal("381b4222-f694-41f0-9685-ff5bb260df2e", guid);
    }

    [Fact]
    public void ExtractPowerSchemeGuid_EmptyOrInvalidOutput_ReturnsNull()
    {
        Assert.Null(GameBoostService.ExtractPowerSchemeGuid(""));
        Assert.Null(GameBoostService.ExtractPowerSchemeGuid("No GUID here"));
        Assert.Null(GameBoostService.ExtractPowerSchemeGuid(null!));
    }

    [Fact]
    public void GetTargetPowerSchemeGuid_ReturnsKnownWindowsPowerGuids()
    {
        string ultimate = GameBoostService.GetTargetPowerSchemeGuid(useUltimate: true);
        string highPerf = GameBoostService.GetTargetPowerSchemeGuid(useUltimate: false);

        Assert.Equal("e9a42b02-d5df-448d-aa00-03f14749eb61", ultimate);
        Assert.Equal("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", highPerf);
    }

    [Fact]
    public void GetThrottlableProcessNames_ContainsExpectedBackgroundApps()
    {
        var names = GameBoostService.GetThrottlableProcessNames();
        Assert.NotNull(names);
        Assert.Contains("OneDrive", names);
        Assert.Contains("MicrosoftEdgeUpdate", names);
    }

    [Fact]
    public async Task ToggleGameBoostAsync_SafelyExecutesAndReturnsResult()
    {
        // Calling toggle off when already off should return gracefully
        var result = await GameBoostService.ToggleGameBoostAsync(enable: false, CancellationToken.None);
        Assert.NotNull(result);
        Assert.False(result.IsActive);
    }
}
