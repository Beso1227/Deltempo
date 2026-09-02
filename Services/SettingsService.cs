using System.IO;
using System.Text.Json;

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
}

public static class SettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Deltempo");

    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    public static AppSettings Current { get; set; } = new();

    static SettingsService()
    {
        LoadSettings();
    }

    public static void LoadSettings()
    {
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
            File.WriteAllText(SettingsFile, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }
}
