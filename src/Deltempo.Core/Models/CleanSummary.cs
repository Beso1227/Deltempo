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

    public string FormattedFreedSize => TargetFolderInfo.FormatBytes(TotalFreedBytes);
}
