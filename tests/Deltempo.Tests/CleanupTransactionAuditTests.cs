using WinTempCleaner.Core.Cleaning;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// The four audit aggregates on CleanupTransactionResult are the ONLY per-file skip
/// reporting the UI has - MainWindow renders them directly - and no test asserted any of
/// them. They were also four O(n) passes over a list that grows to one entry per file
/// across a 16-way parallel loop; they are now one memoized pass.
///
/// These cases pin the counting LOGIC, which matters more than the caching: a shielded
/// file must be counted as shielded and NOT double-counted as policy-protected, and an
/// in-use failure must not be counted as a generic execution failure.
/// </summary>
public class CleanupTransactionAuditTests
{
    private static DeletionAuditRecord Record(
        DeletionAuditStatus status,
        CleanupErrorCategory category,
        string matchedRule) => new()
        {
            FilePath = @"C:\temp\x.tmp",
            FileName = "x.tmp",
            Status = status,
            ErrorCategory = category,
            MatchedRule = matchedRule
        };

    private static CleanupTransactionResult With(params DeletionAuditRecord[] records)
    {
        var result = new CleanupTransactionResult();
        foreach (var r in records) result.AuditRecords.Add(r);
        result.InvalidateAuditCounts();
        return result;
    }

    [Fact]
    public void InUseLockedCount_CountsOnlyFileLockedFailures()
    {
        var result = With(
            Record(DeletionAuditStatus.Failed, CleanupErrorCategory.FileLocked, "TemporaryScratchRule"),
            Record(DeletionAuditStatus.Failed, CleanupErrorCategory.AccessDenied, "TemporaryScratchRule"));

        Assert.Equal(1, result.InUseLockedCount);
    }

    [Fact]
    public void RecentShieldedCount_CountsTheShieldRule()
    {
        var result = With(
            Record(DeletionAuditStatus.SkippedPolicy, CleanupErrorCategory.None, "RecentModificationShield"),
            Record(DeletionAuditStatus.SkippedPolicy, CleanupErrorCategory.None, "ProtectionPolicy"));

        Assert.Equal(1, result.RecentShieldedCount);
    }

    /// <summary>
    /// A 24h-shielded file is also a policy skip. Counting it in both would double-count
    /// it in any total that adds the two, and the UI branches on exactly this pair.
    /// </summary>
    [Fact]
    public void PolicyProtectedCount_ExcludesRecentShieldedRecords()
    {
        var result = With(
            Record(DeletionAuditStatus.SkippedPolicy, CleanupErrorCategory.None, "RecentModificationShield"),
            Record(DeletionAuditStatus.SkippedPolicy, CleanupErrorCategory.None, "ProtectionPolicy"));

        Assert.Equal(1, result.PolicyProtectedCount);
    }

    [Fact]
    public void FailedExecutionCount_ExcludesFileLockedFailures()
    {
        var result = With(
            Record(DeletionAuditStatus.Failed, CleanupErrorCategory.FileLocked, "TemporaryScratchRule"),
            Record(DeletionAuditStatus.Failed, CleanupErrorCategory.AccessDenied, "TemporaryScratchRule"));

        Assert.Equal(1, result.FailedExecutionCount);
    }

    [Fact]
    public void AllFourCounts_AreIndependentAndAdditiveOverAMixedBatch()
    {
        var result = With(
            Record(DeletionAuditStatus.Failed, CleanupErrorCategory.FileLocked, "TemporaryScratchRule"),
            Record(DeletionAuditStatus.Failed, CleanupErrorCategory.AccessDenied, "TemporaryScratchRule"),
            Record(DeletionAuditStatus.SkippedPolicy, CleanupErrorCategory.None, "RecentModificationShield"),
            Record(DeletionAuditStatus.SkippedPolicy, CleanupErrorCategory.None, "ProtectionPolicy"),
            Record(DeletionAuditStatus.Deleted, CleanupErrorCategory.None, "DesignatedCacheFileRule"));

        Assert.Equal(1, result.InUseLockedCount);
        Assert.Equal(1, result.FailedExecutionCount);
        Assert.Equal(1, result.RecentShieldedCount);
        Assert.Equal(1, result.PolicyProtectedCount);
        Assert.Equal(5, result.AuditRecords.Count);
    }

    [Fact]
    public void RepeatedReads_AreStable()
    {
        var result = With(
            Record(DeletionAuditStatus.Failed, CleanupErrorCategory.FileLocked, "TemporaryScratchRule"));

        int first = result.InUseLockedCount;
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(first, result.InUseLockedCount);
        }
    }

    [Fact]
    public void InvalidateAuditCounts_RecomputesAfterLateAdditions()
    {
        var result = With(
            Record(DeletionAuditStatus.Failed, CleanupErrorCategory.FileLocked, "TemporaryScratchRule"));

        int before = result.InUseLockedCount;
        Assert.Equal(1, before);

        result.AuditRecords.Add(
            Record(DeletionAuditStatus.Failed, CleanupErrorCategory.FileLocked, "TemporaryScratchRule"));

        // Without invalidation the memoized value would still be 1.
        result.InvalidateAuditCounts();
        Assert.Equal(2, result.InUseLockedCount);
    }

    [Fact]
    public void EmptyResult_ReportsZeroCounts()
    {
        var result = new CleanupTransactionResult();

        Assert.Equal(0, result.InUseLockedCount);
        Assert.Equal(0, result.RecentShieldedCount);
        Assert.Equal(0, result.PolicyProtectedCount);
        Assert.Equal(0, result.FailedExecutionCount);
    }
}
