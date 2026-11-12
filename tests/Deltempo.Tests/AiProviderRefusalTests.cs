using System;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// A provider that refuses at ACCOUNT level refuses every file the same way. The
/// batch used to keep asking anyway, one file at a time, paying the provider's own
/// timeout per file - measured at 11-19 seconds per file against a rate-limited
/// endpoint, which turned a 500-file batch into minutes of waiting.
/// </summary>
public class AiProviderRefusalTests
{
    [Theory]
    [InlineData(AiFailureKind.RateLimited)]
    [InlineData(AiFailureKind.PaymentRequired)]
    [InlineData(AiFailureKind.ProviderUnreachable)]
    [InlineData(AiFailureKind.Authentication)]
    [InlineData(AiFailureKind.ModelUnavailable)]
    public void AccountLevelRefusals_StopTheBatch(AiFailureKind kind)
    {
        Assert.True(OnlineFileIntelligenceService.IsAccountLevelRefusal(kind),
            $"{kind} applies to the account, so a batch must stop instead of retrying per file.");
    }

    [Theory]
    [InlineData(AiFailureKind.Timeout)]
    [InlineData(AiFailureKind.Unknown)]
    public void PerFileFailures_DoNotStopTheBatch(AiFailureKind kind)
    {
        // A timeout can be one cold local model or one enormous file; the next file
        // may well succeed, so the batch must continue.
        Assert.False(OnlineFileIntelligenceService.IsAccountLevelRefusal(kind),
            $"{kind} can be specific to one file, so the batch must keep going.");
    }

    [Fact]
    public void RefusalReport_CarriesTheReasonSoTheRowCanExplainIt()
    {
        var report = new OnlineSafetyReport
        {
            ProviderUsed = "Web Knowledge (Fallback - RateLimited)",
            ProviderRefused = true,
            ProviderFailureKind = AiFailureKind.RateLimited,
        };

        Assert.True(report.ProviderRefused);
        Assert.Equal(AiFailureKind.RateLimited, report.ProviderFailureKind);
        Assert.Contains("RateLimited", report.ProviderUsed, StringComparison.Ordinal);
    }
}
