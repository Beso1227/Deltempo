namespace WinTempCleaner.Models;

public class JunkFileItem
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime LastModified { get; set; }

    public string FormattedSize => TargetFolderInfo.FormatBytes(SizeBytes);
    public string FormattedDate => LastModified.ToString("yyyy-MM-dd HH:mm");
}
