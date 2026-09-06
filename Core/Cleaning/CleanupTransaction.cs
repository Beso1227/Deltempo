using WinTempCleaner.Models;

namespace WinTempCleaner.Core.Cleaning;

/// <summary>
/// Structured verification transaction summarizing the precise outcome of a cleanup operation.
/// Enforces the rule: "Do not report '18 GB cleaned' unless the application actually verified what happened."
/// </summary>
public class CleanupTransactionResult
{
    public string ScopeId { get; set; } = string.Empty;
    public string ScopeName { get; set; } = string.Empty;

    public int DiscoveredCount { get; set; }
    public long DiscoveredBytes { get; set; }

    public int EligibleCount { get; set; }
    public long EligibleBytes { get; set; }

    public int ProtectedCount { get; set; }
    public long ProtectedBytes { get; set; }

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

    public long TotalFreedBytes => DeletedBytes + RecycledBytes;
    public int TotalItemsFreed => DeletedCount + RecycledCount;

    public string FormattedFreed => TargetFolderInfo.FormatBytes(TotalFreedBytes);
    public string FormattedEligible => TargetFolderInfo.FormatBytes(EligibleBytes);
    public string FormattedDiscovered => TargetFolderInfo.FormatBytes(DiscoveredBytes);

    public bool Success => FailedCount == 0 && ErrorMessages.Count == 0;
}
