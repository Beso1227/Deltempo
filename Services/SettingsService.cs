using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace WinTempCleaner.Services;

public class AppSettings
{
    public bool MinimizeToTray { get; set; } = true;
    public bool EnableAutoPilot { get; set; } = true;
    public int AutoCleanIntervalHours { get; set; } = 12;
    public bool AutoCleanNotify { get; set; } = true;
    public bool IsDarkMode { get; set; } = true;
    public bool SoundEnabled { get; set; } = true;
    public bool CheckUpdatesOnStartup { get; set; } = true;
    public string Language { get; set; } = "en";
    public bool SendToRecycleBin { get; set; } = false;
    public bool LowDiskAlertEnabled { get; set; } = true;
    public int LowDiskAlertThresholdGb { get; set; } = 10;

    // ─── Memory Optimizer (WinMemoryCleaner integration) ──────────────────
    public bool MemoryAutoOptimizeEnabled { get; set; } = false;
    public int MemoryAutoOptimizeIntervalHours { get; set; } = 4;
    public int MemoryAutoOptimizeFreeRamThresholdPercent { get; set; } = 30;
    public bool MemoryShowInTray { get; set; } = true;
    public bool MemoryAlwaysOnTop { get; set; } = false;
    public bool MemoryCompactMode { get; set; } = false;
    public string MemoryGlobalHotkey { get; set; } = "CTRL+SHIFT+M";
    public bool MemoryCloseToTray { get; set; } = true;
    public bool MemoryShowNotifications { get; set; } = true;

    // ─── Update Tracking & Configuration ──────────────────────────────────
    public string DismissedVersion { get; set; } = string.Empty;
    public string UpdateChannel { get; set; } = "Stable";
    public int UpdateCheckFrequencyDays { get; set; } = 1;
    public bool AutoDownloadUpdates { get; set; } = false;
    public string LastUpdateCheckTimestamp { get; set; } = string.Empty;

    // ─── AI & Online File Intelligence ──────────────────────────────────
    public bool EnableOnlineAiSafety { get; set; } = false;
    public string AiProvider { get; set; } = "BuiltIn"; // "BuiltIn", "Gemini", "OpenAI", "Groq", "Ollama"
    public string AiApiKey { get; set; } = string.Empty;
    public string AiModelName { get; set; } = string.Empty;
    public string AiOllamaEndpoint { get; set; } = "http://localhost:11434";
    public bool AutoQueryAiForLargeFiles { get; set; } = false;
}

public static class SettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Deltempo");

    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    private static AppSettings _current = new();
    public static AppSettings Current => _current;

    static SettingsService()
    {
        LoadSettings();
    }

    public static void Update(Action<AppSettings> apply)
    {
        if (apply == null) return;
        apply(_current);
        SaveSettings();
    }

    public static void LoadSettings()
    {
        try
        {
            AppSettings? loaded = null;
            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);
                loaded = JsonSerializer.Deserialize<AppSettings>(json);
            }

            if (loaded != null)
            {
                loaded.AutoCleanIntervalHours = Math.Clamp(loaded.AutoCleanIntervalHours, 1, 168);
                loaded.LowDiskAlertThresholdGb = Math.Clamp(loaded.LowDiskAlertThresholdGb, 1, 500);
                loaded.MemoryAutoOptimizeIntervalHours = Math.Clamp(loaded.MemoryAutoOptimizeIntervalHours, 1, 72);
                loaded.MemoryAutoOptimizeFreeRamThresholdPercent = Math.Clamp(loaded.MemoryAutoOptimizeFreeRamThresholdPercent, 5, 95);

                _current = loaded;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }

    public static void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var snapshot = _current;
            string json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFile, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }
}
