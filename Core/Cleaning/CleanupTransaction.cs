using WinTempCleaner.Models;

namespace WinTempCleaner.Core.Cleaning;

/// <summary>
/// Outcome of the cleanup operation for reporting and auditing.
/// </summary>
public enum CleanupCompletionStatus
{
    Clean,
    CompletedWithWarnings,
    PartiallyCompleted,
    Failed,
    Cancelled
}

/// <summary>
/// Structured category of error or safety abort reason.
/// </summary>
public enum CleanupErrorCategory
{
    None,
    AccessDenied,
    FileLocked,
    InvalidPath,
    ReparsePointRejected,
    ProtectedPath,
    NativeApiFailure,
    SizeDriftDetected,
    TimestampDriftDetected,
    SystemAttributeSet,
    PostVerificationFailed,
    Cancelled,
    Unknown
}

/// <summary>
/// Execution status of an individual file within the cleanup transaction audit log.
/// </summary>
public enum DeletionAuditStatus
{
    Deleted,
    Recycled,
    SkippedPolicy,
    SkippedRevalidation,
    Failed,
    VerificationFailed
}

/// <summary>
/// Fine-grained, immutable audit entry for every evaluated or executed file in the cleanup transaction.
/// </summary>
public record DeletionAuditRecord
{
    public string FilePath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public WinTempCleaner.Core.Safety.SafetyRiskTier RiskTier { get; init; } = WinTempCleaner.Core.Safety.SafetyRiskTier.Unknown;
    public string MatchedRule { get; init; } = string.Empty;
    public IntendedCleanupAction IntendedAction { get; init; }
    public DeletionAuditStatus Status { get; init; }
    public CleanupErrorCategory ErrorCategory { get; init; } = CleanupErrorCategory.None;
    public long SizeBytes { get; init; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public string? ErrorOrSkipReason { get; init; }
}

/// <summary>
/// Structured verification transaction summarizing the precise outcome of a cleanup operation.
/// Enforces the rule: "Do not report '18 GB cleaned' unless the application actually verified what happened."
/// </summary>
public class CleanupTransactionResult
{
    public string ScopeId { get; set; } = string.Empty;
    public string ScopeName { get; set; } = string.Empty;

    public List<DeletionAuditRecord> AuditRecords { get; set; } = new();

    public int DiscoveredCount { get; set; }
    public long DiscoveredBytes { get; set; }

    public int EligibleCount { get; set; }
    public long EligibleBytes { get; set; }

    public int ProtectedCount { get; set; }
    public long ProtectedBytes { get; set; }

    public int ReviewRequiredCount { get; set; }
    public long ReviewRequiredBytes { get; set; }

    public int UnknownCount { get; set; }
    public long UnknownBytes { get; set; }

    public int SkippedCount { get; set; }
    public long SkippedBytes { get; set; }

    public int FailedCount { get; set; }
    public long FailedBytes { get; set; }

    public int DeletedCount { get; set; }
    public long DeletedBytes { get; set; }

    public int RecycledCount { get; set; }
    public long RecycledBytes { get; set; }

    public DateTime StartTimeUtc { get; set; } = DateTime.UtcNow;
    public DateTime EndTimeUtc { get; set; } = DateTime.UtcNow;
    public TimeSpan Duration => EndTimeUtc - StartTimeUtc;

    public List<string> ErrorMessages { get; set; } = new();
    public List<string> SkippedReasons { get; set; } = new();
    public HashSet<string> AffectedParentDirectories { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public long TotalFreedBytes => DeletedBytes + RecycledBytes;
    public int TotalItemsFreed => DeletedCount + RecycledCount;
    public bool WasCancelled { get; set; }

    public string FormattedFreed => TargetFolderInfo.FormatBytes(TotalFreedBytes);
    public string FormattedEligible => TargetFolderInfo.FormatBytes(EligibleBytes);
    public string FormattedDiscovered => TargetFolderInfo.FormatBytes(DiscoveredBytes);

    public CleanupCompletionStatus CompletionStatus
    {
        get
        {
            if (WasCancelled && TotalItemsFreed == 0) return CleanupCompletionStatus.Cancelled;
            if (FailedCount == 0 && ErrorMessages.Count == 0) return CleanupCompletionStatus.Clean;
            if (TotalItemsFreed > 0 && FailedCount > 0) return CleanupCompletionStatus.CompletedWithWarnings;
            if (TotalItemsFreed > 0 && FailedCount == 0) return CleanupCompletionStatus.Clean;
            if (FailedCount > 0 && TotalItemsFreed == 0) return CleanupCompletionStatus.Failed;
            return CleanupCompletionStatus.CompletedWithWarnings;
        }
    }

    public bool Success => CompletionStatus is CleanupCompletionStatus.Clean or CleanupCompletionStatus.CompletedWithWarnings;
}
