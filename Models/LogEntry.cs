namespace WinTempCleaner.Models;

public enum LogLevel
{
    Info,
    Success,
    Warning,
    Error
}

public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LogLevel Level { get; set; } = LogLevel.Info;
    public string Message { get; set; } = string.Empty;

    public string FormattedTime => Timestamp.ToString("HH:mm:ss");

    public string BadgeColor => Level switch
    {
        LogLevel.Success => "#10B981",
        LogLevel.Warning => "#F59E0B",
        LogLevel.Error => "#EF4444",
        _ => "#60A5FA"
    };
}
