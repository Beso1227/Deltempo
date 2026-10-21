using System.IO;
using WinTempCleaner.Core.Cleaning;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// CleanupPlan's aggregates used to be twelve separate LINQ passes over Actions, recomputed
/// on every property read - and the UI reads them repeatedly while a plan is applied. They
/// are now computed once and memoized, so these tests pin that the memoized values still
/// match a straight recomputation, including after a late mutation via Mutate().
/// </summary>
[Collection("SettingsService")]
public class CleanupPlanTotalsTests
{
    private static PlannedFileAction Make(
        long size,
        SafetyRiskTier tier,
        IntendedCleanupAction action) => new()
        {
            FilePath = $@"C:\temp\{Guid.NewGuid():N}.tmp",
            FileName = "x.tmp",
            SizeBytes = size,
            PlannedSizeBytes = size,
            SafetyTier = tier,
            Action = action
        };

    private static CleanupPlan SamplePlan() => new()
    {
        ScopeId = "Sample",
        ScopeName = "Sample scope",
        Actions = new List<PlannedFileAction>
        {
            Make(100, SafetyRiskTier.Safe, IntendedCleanupAction.DeletePermanently),
            Make(200, SafetyRiskTier.LowRisk, IntendedCleanupAction.MoveToRecycleBin),
            Make(400, SafetyRiskTier.Protected, IntendedCleanupAction.SkipProtected),
            Make(800, SafetyRiskTier.ReviewRequired, IntendedCleanupAction.SkipReviewRequired),
            Make(1600, SafetyRiskTier.Unknown, IntendedCleanupAction.SkipProtected),
            Make(50, SafetyRiskTier.Safe, IntendedCleanupAction.SkipRecent),
        }
    };

    [Fact]
    public void DiscoveredTotals_CountEverything()
    {
        var plan = SamplePlan();

        Assert.Equal(6, plan.DiscoveredCount);
        Assert.Equal(100 + 200 + 400 + 800 + 1600 + 50, plan.DiscoveredBytes);
    }

    [Fact]
    public void EligibleTotals_CountOnlyDestructiveActions()
    {
        var plan = SamplePlan();

        Assert.Equal(2, plan.EligibleCount);
        Assert.Equal(300, plan.EligibleBytes);
    }

    [Fact]
    public void ProtectedAndReviewAndUnknown_TotalsAreIndependentOfAction()
    {
        var plan = SamplePlan();

        Assert.Equal(1, plan.ProtectedCount);
        Assert.Equal(400, plan.ProtectedBytes);

        Assert.Equal(1, plan.ReviewRequiredCount);
        Assert.Equal(800, plan.ReviewRequiredBytes);

        Assert.Equal(1, plan.UnknownCount);
        Assert.Equal(1600, plan.UnknownBytes);
    }

    [Fact]
    public void SkippedTotals_CountEverySkipVariant()
    {
        var plan = SamplePlan();

        // SkipProtected(400) + SkipReviewRequired(800) + SkipProtected-for-Unknown(1600)
        // + SkipRecent(50)
        Assert.Equal(4, plan.SkippedCount);
        Assert.Equal(400 + 800 + 1600 + 50, plan.SkippedBytes);
    }

    /// <summary>
    /// The memoization is only safe if repeated reads return identical values, and the
    /// aggregates must not disagree with a from-scratch recomputation of the same data.
    /// </summary>
    [Fact]
    public void RepeatedReads_AreStableAndMatchAFreshRecomputation()
    {
        var plan = SamplePlan();

        long eligibleFirst = plan.EligibleBytes;
        int eligibleCountFirst = plan.EligibleCount;
        long skippedFirst = plan.SkippedBytes;
        int skippedCountFirst = plan.SkippedCount;

        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(eligibleFirst, plan.EligibleBytes);
            Assert.Equal(eligibleCountFirst, plan.EligibleCount);
            Assert.Equal(skippedFirst, plan.SkippedBytes);
            Assert.Equal(skippedCountFirst, plan.SkippedCount);
        }

        // Independent recomputation over the same list.
        var fresh = SamplePlan();
        Assert.Equal(
            fresh.Actions
                .Where(a => a.Action is IntendedCleanupAction.DeletePermanently or IntendedCleanupAction.MoveToRecycleBin)
                .Sum(a => a.SizeBytes),
            eligibleFirst);
    }

    [Fact]
    public void Mutate_InvalidatesMemoizedTotals()
    {
        var plan = SamplePlan();
        int before = plan.DiscoveredCount;
        long beforeBytes = plan.DiscoveredBytes;

        // Read first so the totals are definitely memoized, then extend.
        _ = plan.SkippedCount;

        plan.Mutate(list => list.Add(
            Make(7, SafetyRiskTier.Safe, IntendedCleanupAction.DeletePermanently)));

        Assert.Equal(before + 1, plan.DiscoveredCount);
        Assert.Equal(beforeBytes + 7, plan.DiscoveredBytes);
        Assert.Equal(3, plan.EligibleCount);
        Assert.Equal(307, plan.EligibleBytes);
    }

    [Fact]
    public void EmptyPlan_ReportsZeroesRatherThanThrowing()
    {
        var plan = new CleanupPlan { ScopeId = "Empty", ScopeName = "Empty" };

        Assert.Equal(0, plan.DiscoveredCount);
        Assert.Equal(0, plan.DiscoveredBytes);
        Assert.Equal(0, plan.EligibleCount);
        Assert.Equal(0, plan.SkippedCount);
        Assert.Equal(0, plan.ProtectedCount);
        Assert.Equal(0, plan.ReviewRequiredCount);
        Assert.Equal(0, plan.UnknownCount);
    }

    [Fact]
    public void ZeroSizedFiles_AreCountedEvenThoughTheyAddNoBytes()
    {
        var plan = new CleanupPlan
        {
            Actions = new List<PlannedFileAction>
            {
                Make(0, SafetyRiskTier.Safe, IntendedCleanupAction.DeletePermanently),
                Make(0, SafetyRiskTier.Safe, IntendedCleanupAction.DeletePermanently),
            }
        };

        Assert.Equal(2, plan.EligibleCount);
        Assert.Equal(0, plan.EligibleBytes);
    }

    /// <summary>
    /// End-to-end shape check: the planner must still produce a plan whose totals agree
    /// with its own action list, through the real scanner and safety engine.
    /// </summary>
    [Fact]
    public void CreatePlan_OverATempDirectory_ProducesSelfConsistentTotals()
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Deltempo_PlanTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            for (int i = 0; i < 12; i++)
            {
                File.WriteAllText(Path.Combine(root, $"old_{i}.tmp"), "scratch");
            }

            var plan = CleanupPlanner.CreatePlan(
                scopeId: "PlanTest",
                scopeName: "Plan test scope",
                directories: new[] { root },
                category: "Test",
                apply24HourShield: false,
                sendToRecycleBin: false,
                ct: CancellationToken.None);

            Assert.NotEmpty(plan.Actions);
            Assert.Equal(plan.Actions.Count, plan.DiscoveredCount);
            Assert.Equal(plan.Actions.Sum(a => a.SizeBytes), plan.DiscoveredBytes);
            Assert.Equal(
                plan.Actions.Count(a => a.Action is IntendedCleanupAction.DeletePermanently or IntendedCleanupAction.MoveToRecycleBin),
                plan.EligibleCount);

            // Every planned path must live under the declared root.
            foreach (var action in plan.Actions)
            {
                Assert.StartsWith(root, action.FilePath, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
            catch { /* best effort */ }
        }
    }
}
