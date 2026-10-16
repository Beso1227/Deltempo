using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Enforces the rule that Deltempo never presents memory trimming as a performance
/// improvement.
///
/// The available-RAM figure already counts the standby cache, so discarding the standby list
/// raises that number while removing the very pages Windows uses to serve new allocations
/// without touching disk. "More free RAM" therefore moves opposite to real performance here.
/// Copy is the enforcement surface: a future copywriter reintroducing "frees RAM", "boosts
/// performance", or "speed boost" fails this test instead of shipping a false promise.
/// </summary>
public class MemoryClaimLanguageTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "deltempo.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir.FullName;
    }

    private static string[] ClaimFiles() => new[]
    {
        "README.md",
        "SECURITY.md",
        "docs/llms.txt",
        "docs/llms-full.txt",
        "docs/THREAT_MODEL.md",
    };

    /// <summary>
    /// Phrasings that assert a performance benefit from memory trimming. Each is a claim the
    /// implementation cannot support.
    /// </summary>
    private static readonly string[] ForbiddenPhrases =
    {
        "free up ram",
        "frees ram",
        "frees up ram",
        "free more ram",
        "boost performance",
        "ram boost",
        "boost ram",
        "speed boost",
        "faster boot",
        "more ram means",
    };

    [Theory]
    [InlineData("README.md")]
    [InlineData("SECURITY.md")]
    [InlineData("docs/llms.txt")]
    [InlineData("docs/llms-full.txt")]
    [InlineData("docs/THREAT_MODEL.md")]
    public void Docs_DoNotClaimMemoryTrimmingImprovesPerformance(string relativePath)
    {
        string path = Path.Combine(RepoRoot(), relativePath);
        Assert.True(File.Exists(path), $"Expected documentation file: {relativePath}");

        string text = File.ReadAllText(path);
        string lower = text.ToLowerInvariant();

        foreach (var phrase in ForbiddenPhrases)
        {
            Assert.False(lower.Contains(phrase, StringComparison.Ordinal),
                $"{relativePath} contains the unsupported claim '{phrase}'. Memory trimming evicts cached pages; it is not a performance improvement.");
        }
    }

    [Fact]
    public void Readme_MemoryFaq_ExplainsTheReReadTradeoff()
    {
        // The honest version of the answer must stay present: explaining that freed memory is
        // re-read. Losing this would leave the prohibition above satisfied by silence.
        string readme = File.ReadAllText(Path.Combine(RepoRoot(), "README.md"));

        Assert.Contains("re-read", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a speed button", readme, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The user-visible surface must not label a memory target as unconditionally safe.
    /// Guards the badge table in MemoryOptimizerService against regression.
    /// </summary>
    [Fact]
    public void MemoryTargetMetadata_NeverClaimsUnconditionalSafety()
    {
        string[] unconditional = { "100% safe", "safe to purge", "guaranteed safe", "risk-free" };

        var areas = WinTempCleaner.Services.MemoryOptimizerService.GetMemoryAreaSnapshots();
        Assert.NotEmpty(areas);

        foreach (var area in areas)
        {
            foreach (var claim in unconditional)
            {
                Assert.False(area.SafetyBadge.Contains(claim, StringComparison.OrdinalIgnoreCase),
                    $"Target {area.Target} claims '{claim}' in badge '{area.SafetyBadge}'.");
            }
        }
    }

    /// <summary>
    /// The result object must not expose a "bytes freed" figure. The only honestly measurable
    /// quantity is the change in available physical RAM across the whole run.
    /// </summary>
    [Fact]
    public void ResultTypes_ExposeOnlyTheHonestlyMeasurableQuantity()
    {
        var resultType = typeof(WinTempCleaner.Services.MemoryOptimizationResult);
        var areaType = typeof(WinTempCleaner.Services.MemoryAreaResult);

        var resultProps = resultType.GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("MeasuredBytesFreed", resultProps);
        Assert.DoesNotContain("FormattedReclaimed", resultProps);

        var areaProps = areaType.GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("MeasuredBytesFreed", areaProps);
        Assert.DoesNotContain("FormattedFreed", areaProps);

        Assert.Contains("MeasuredAvailableDelta", resultProps);
        Assert.Contains("RequestedTargetCount", resultProps);
        Assert.Contains("SucceededTargetCount", resultProps);
        Assert.Contains("FailedTargetCount", resultProps);
    }
}
