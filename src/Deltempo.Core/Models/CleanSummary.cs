namespace WinTempCleaner.Models;

public class CleanSummary
{
    public long TotalFreedBytes { get; set; }
    public int TotalFilesDeleted { get; set; }
    public int TotalFoldersDeleted { get; set; }
    public int TotalFilesSkipped { get; set; }
    public int TotalFilesInUse { get; set; }
    public int TotalFilesRecentShielded { get; set; }
    public int TotalFilesPolicyProtected { get; set; }
    public int TotalFilesFailed { get; set; }
    public TimeSpan ElapsedTime { get; set; }

    /// <summary>
    /// Set by the 1-Click Deep Clean, which also flushes standby memory. The
    /// selected-category clean leaves both at their defaults.
    /// </summary>
    public bool IsDeepClean { get; set; }

    public long RamFreedBytes { get; set; }

    public string FormattedFreedSize => TargetFolderInfo.FormatBytes(TotalFreedBytes);

    public string FormattedRamFreed => TargetFolderInfo.FormatBytes(RamFreedBytes);
}
