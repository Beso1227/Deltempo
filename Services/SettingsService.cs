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
}

public static class SettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Deltempo");

    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");
    private static readonly ReaderWriterLockSlim SettingsLock = new(LockRecursionPolicy.NoRecursion);

    public static AppSettings Current { get; set; } = new();

    static SettingsService()
    {
        LoadSettings();
    }

    public static void LoadSettings()
    {
        try
        {
            SettingsLock.EnterReadLock();
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string json = File.ReadAllText(SettingsFile);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                    if (loaded != null)
                    {
                        Current = loaded;
                    }
                }
            }
            finally
            {
                SettingsLock.ExitReadLock();
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
            string json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            SettingsLock.EnterWriteLock();
            try
            {
                File.WriteAllText(SettingsFile, json);
            }
            finally
            {
                SettingsLock.ExitWriteLock();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }
}
