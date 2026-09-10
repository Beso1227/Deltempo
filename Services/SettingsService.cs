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
    private static readonly string SettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Deltempo", "settings.json");

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
            AppSettings? loaded = SettingsFileStore.Load(SettingsFile);

            if (loaded != null)
            {
                Normalize(loaded);

                // Secrets are DPAPI-protected on disk and plaintext only in memory.
                string rawKey = loaded.AiApiKey;
                bool legacyPlaintextKey = !string.IsNullOrEmpty(rawKey) &&
                                          !SettingsSecretProtector.IsProtected(rawKey);
                loaded.AiApiKey = SettingsSecretProtector.Unprotect(rawKey);

                _current = loaded;

                // One-time migration: re-persist legacy plaintext keys in DPAPI form
                // so plaintext stops existing on disk immediately after upgrade.
                if (legacyPlaintextKey)
                {
                    SaveSettings();
                }
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
            Normalize(_current);
            var snapshot = _current;

            // Encrypt secrets at the persistence boundary: plaintext never touches disk.
            var persisted = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(snapshot));
            if (persisted == null) return;

            persisted.AiApiKey = SettingsSecretProtector.Protect(persisted.AiApiKey);

            string json = JsonSerializer.Serialize(persisted, new JsonSerializerOptions { WriteIndented = true });
            SettingsFileStore.SaveAtomic(SettingsFile, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }

    private static void Normalize(AppSettings settings)
    {
        settings.AutoCleanIntervalHours = Math.Clamp(settings.AutoCleanIntervalHours, 1, 168);
        settings.LowDiskAlertThresholdGb = Math.Clamp(settings.LowDiskAlertThresholdGb, 1, 500);
        settings.MemoryAutoOptimizeIntervalHours = Math.Clamp(settings.MemoryAutoOptimizeIntervalHours, 1, 72);
        settings.MemoryAutoOptimizeFreeRamThresholdPercent = Math.Clamp(settings.MemoryAutoOptimizeFreeRamThresholdPercent, 5, 95);
    }
}
