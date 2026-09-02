namespace WinTempCleaner.Models;

public class DriveTelemetryInfo
{
    public string DriveLetter { get; set; } = "C:";
    public string VolumeLabel { get; set; } = "OS";
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);

    public double FreePercentage => TotalBytes > 0 ? (double)FreeBytes / TotalBytes * 100 : 0;
    public double UsedPercentage => TotalBytes > 0 ? (double)UsedBytes / TotalBytes * 100 : 0;
    public bool IsLowSpace => FreePercentage < 15.0;

    public string FormattedTotal => TargetFolderInfo.FormatBytes(TotalBytes);
    public string FormattedFree => TargetFolderInfo.FormatBytes(FreeBytes);
    public string FormattedUsed => TargetFolderInfo.FormatBytes(UsedBytes);

    public string DisplaySummary => $"{DriveLetter} ({VolumeLabel}) — {FormattedFree} free of {FormattedTotal} ({FreePercentage:F1}% Free)";
}
