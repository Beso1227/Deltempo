namespace WinTempCleaner.Models;

public class CleanSummary
{
    public long TotalFreedBytes { get; set; }
    public int TotalFilesDeleted { get; set; }
    public int TotalFoldersDeleted { get; set; }
    public int TotalFilesSkipped { get; set; }
    public TimeSpan ElapsedTime { get; set; }

    public string FormattedFreedSize => TargetFolderInfo.FormatBytes(TotalFreedBytes);
}
