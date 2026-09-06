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

    public long DiscoveredBytes => Actions.Sum(a => a.SizeBytes);
    public int DiscoveredCount => Actions.Count;

    public long EligibleBytes => Actions
        .Where(a => a.Action is IntendedCleanupAction.DeletePermanently or IntendedCleanupAction.MoveToRecycleBin)
        .Sum(a => a.SizeBytes);

    public int EligibleCount => Actions
        .Count(a => a.Action is IntendedCleanupAction.DeletePermanently or IntendedCleanupAction.MoveToRecycleBin);

    public long ProtectedBytes => Actions
        .Where(a => a.SafetyTier == SafetyRiskTier.Protected)
        .Sum(a => a.SizeBytes);

    public int ProtectedCount => Actions
        .Count(a => a.SafetyTier == SafetyRiskTier.Protected);

    public long ReviewRequiredBytes => Actions
        .Where(a => a.SafetyTier == SafetyRiskTier.ReviewRequired)
        .Sum(a => a.SizeBytes);

    public int ReviewRequiredCount => Actions
        .Count(a => a.SafetyTier == SafetyRiskTier.ReviewRequired);

    public long UnknownBytes => Actions
        .Where(a => a.SafetyTier == SafetyRiskTier.Unknown)
        .Sum(a => a.SizeBytes);

    public int UnknownCount => Actions
        .Count(a => a.SafetyTier == SafetyRiskTier.Unknown);

    public long SkippedBytes => Actions
        .Where(a => a.Action is IntendedCleanupAction.SkipProtected or IntendedCleanupAction.SkipReviewRequired or IntendedCleanupAction.SkipRecent or IntendedCleanupAction.SkipError)
        .Sum(a => a.SizeBytes);

    public int SkippedCount => Actions
        .Count(a => a.Action is IntendedCleanupAction.SkipProtected or IntendedCleanupAction.SkipReviewRequired or IntendedCleanupAction.SkipRecent or IntendedCleanupAction.SkipError);
}
