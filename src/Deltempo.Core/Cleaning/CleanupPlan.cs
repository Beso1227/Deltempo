using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;

namespace WinTempCleaner.Core.Cleaning;

public enum IntendedCleanupAction
{
    DeletePermanently,
    MoveToRecycleBin,
    SkipProtected,
    SkipReviewRequired,
    SkipRecent,
    SkipError
}

/// <summary>
/// Individual file decision record produced during the planning / dry-run phase.
/// </summary>
public class PlannedFileAction
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string FormattedSize => TargetFolderInfo.FormatBytes(SizeBytes);
    public string Category { get; set; } = string.Empty;
    public SafetyRiskTier SafetyTier { get; set; } = SafetyRiskTier.Unknown;
    public string Reason { get; set; } = string.Empty;
    public string MatchedRule { get; set; } = string.Empty;
    public IntendedCleanupAction Action { get; set; } = IntendedCleanupAction.SkipProtected;
    public DateTime LastModified { get; set; }
    public long PlannedSizeBytes { get; set; }
}

/// <summary>
/// Immutable deterministic plan for a target scope.
/// Shared identically between Dry-Run preview and live execution to prevent divergence.
/// </summary>
public class CleanupPlan
{
    public string ScopeId { get; set; } = string.Empty;
    public string ScopeName { get; set; } = string.Empty;
    public List<PlannedFileAction> Actions { get; set; } = new();

    // Every aggregate below used to be a separate LINQ pass over Actions, recomputed from
    // scratch on EVERY property read. A plan for a large temp directory holds hundreds of
    // thousands of actions, and the UI reads these totals repeatedly while the plan is being
    // applied - so a single pass became nine, then eighteen, over the whole list.
    //
    // They are now computed once, in a single pass, and memoized. The plan is effectively
    // immutable once CreatePlan returns (Actions is not appended to afterwards), so this is
    // a safe trade; Mutate() is provided for the one legitimate case of adding late actions
    // and invalidates the cache.
    private PlanTotals? _totals;

    private PlanTotals Totals => _totals ??= ComputeTotals();

    private PlanTotals ComputeTotals()
    {
        var totals = new PlanTotals();

        foreach (var a in Actions)
        {
            totals.DiscoveredBytes += a.SizeBytes;
            totals.DiscoveredCount++;

            switch (a.Action)
            {
                case IntendedCleanupAction.DeletePermanently:
                case IntendedCleanupAction.MoveToRecycleBin:
                    totals.EligibleBytes += a.SizeBytes;
                    totals.EligibleCount++;
                    break;

                case IntendedCleanupAction.SkipProtected:
                case IntendedCleanupAction.SkipReviewRequired:
                case IntendedCleanupAction.SkipRecent:
                case IntendedCleanupAction.SkipError:
                    totals.SkippedBytes += a.SizeBytes;
                    totals.SkippedCount++;
                    break;
            }

            switch (a.SafetyTier)
            {
                case SafetyRiskTier.Protected:
                    totals.ProtectedBytes += a.SizeBytes;
                    totals.ProtectedCount++;
                    break;

                case SafetyRiskTier.ReviewRequired:
                    totals.ReviewRequiredBytes += a.SizeBytes;
                    totals.ReviewRequiredCount++;
                    break;

                case SafetyRiskTier.Unknown:
                    totals.UnknownBytes += a.SizeBytes;
                    totals.UnknownCount++;
                    break;
            }
        }

        return totals;
    }

    /// <summary>
    /// Adds actions after construction and invalidates the cached totals. The planner builds
    /// its list before returning, so this is only for callers that genuinely extend a plan.
    /// </summary>
    public void Mutate(Action<List<PlannedFileAction>> mutate)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        mutate(Actions);
        _totals = null;
    }

    private sealed class PlanTotals
    {
        public long DiscoveredBytes;
        public int DiscoveredCount;
        public long EligibleBytes;
        public int EligibleCount;
        public long ProtectedBytes;
        public int ProtectedCount;
        public long ReviewRequiredBytes;
        public int ReviewRequiredCount;
        public long UnknownBytes;
        public int UnknownCount;
        public long SkippedBytes;
        public int SkippedCount;
    }

    public long DiscoveredBytes => Totals.DiscoveredBytes;
    public int DiscoveredCount => Totals.DiscoveredCount;
    public long EligibleBytes => Totals.EligibleBytes;
    public int EligibleCount => Totals.EligibleCount;
    public long ProtectedBytes => Totals.ProtectedBytes;
    public int ProtectedCount => Totals.ProtectedCount;
    public long ReviewRequiredBytes => Totals.ReviewRequiredBytes;
    public int ReviewRequiredCount => Totals.ReviewRequiredCount;
    public long UnknownBytes => Totals.UnknownBytes;
    public int UnknownCount => Totals.UnknownCount;
    public long SkippedBytes => Totals.SkippedBytes;
    public int SkippedCount => Totals.SkippedCount;
}
