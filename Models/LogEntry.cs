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
    public string Message { get; set; } = string.Empty;
    public LogLevel Level { get; set; } = LogLevel.Info;
    public string FormattedTime => Timestamp.ToString("HH:mm:ss");

    public string BadgeColor => Level switch
    {
        LogLevel.Success => "#10B981",
        LogLevel.Warning => "#F59E0B",
        LogLevel.Error => "#F43F5E",
        _ => WinTempCleaner.Services.ThemeService.IsDarkMode ? "#F8FAFC" : "#0F172A"
    };

    public string LevelGlyph => Level switch
    {
        LogLevel.Success => "✓",
        LogLevel.Warning => "⚠",
        LogLevel.Error => "✕",
        _ => "›"
    };
}

