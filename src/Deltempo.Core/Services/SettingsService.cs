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
    public bool EnableShellContextMenu { get; set; } = false;
    public bool EnableWeeklyScheduledTask { get; set; } = false;

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
    public string AiProvider { get; set; } = "BuiltIn"; // "BuiltIn", "Gemini", "OpenAI", "Groq", "OpenRouter", "Ollama"

    /// <summary>
    /// The key of the currently selected provider, mirrored from <see cref="AiApiKeys"/>
    /// on save. Kept for older builds that only know this single field; new code must
    /// call <see cref="GetAiApiKey"/> so a provider switch cannot reuse another
    /// provider's credential.
    /// </summary>
    public string AiApiKey { get; set; } = string.Empty;

    /// <summary>
    /// One key per provider. A single shared field meant switching Gemini to Groq sent
    /// the Gemini key to Groq, which answered 401 and looked like "AI is broken".
    /// </summary>
    public Dictionary<string, string> AiApiKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string AiModelName { get; set; } = string.Empty;
    public string AiOllamaEndpoint { get; set; } = "http://localhost:11434"; // DevSkim: ignore DS162092
    public bool AutoQueryAiForLargeFiles { get; set; } = false;

    /// <summary>
    /// The key for <paramref name="provider"/> (defaults to the selected provider).
    /// Falls back to the legacy single <see cref="AiApiKey"/> only for the provider it
    /// was actually entered for, so a pre-upgrade settings file keeps working without
    /// handing one provider's secret to another.
    /// </summary>
    public string GetAiApiKey(string? provider = null)
    {
        string target = string.IsNullOrWhiteSpace(provider) ? AiProvider : provider.Trim();

        if (AiApiKeys != null &&
            !string.IsNullOrWhiteSpace(target) &&
            AiApiKeys.TryGetValue(target, out string? stored) &&
            !string.IsNullOrWhiteSpace(stored))
        {
            return stored;
        }

        bool legacyBelongsToTarget = string.IsNullOrWhiteSpace(AiProvider) ||
            string.Equals(AiProvider, target, StringComparison.OrdinalIgnoreCase);

        return legacyBelongsToTarget && !string.IsNullOrWhiteSpace(AiApiKey)
            ? AiApiKey
            : string.Empty;
    }

    /// <summary>
    /// Stores or removes the key for <paramref name="provider"/>. An empty key removes
    /// the entry, which is how the settings surface implements "forget this key".
    /// </summary>
    public void SetAiApiKey(string? provider, string? apiKey)
    {
        string target = string.IsNullOrWhiteSpace(provider) ? "BuiltIn" : provider.Trim();
        AiApiKeys ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            AiApiKeys.Remove(target);
        }
        else
        {
            AiApiKeys[target] = apiKey.Trim();
        }

        // Keep the legacy mirror in step with the selected provider.
        if (string.Equals(target, AiProvider, StringComparison.OrdinalIgnoreCase))
        {
            AiApiKey = string.IsNullOrWhiteSpace(apiKey) ? string.Empty : apiKey.Trim();
        }
    }

    // ─── Custom Exclusions & Advanced Maintenance ──────────────────────────
    public List<string> CustomPathExclusions { get; set; } = new();
    public List<string> CustomExtensionExclusions { get; set; } = new();
    public bool AggressiveEmptyFolderPrune { get; set; } = true;
    public bool AutoCleanOnIdle { get; set; } = false;
    public int AutoCleanIdleMinutes { get; set; } = 15;
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
                loaded.AiApiKeys = UnprotectAiKeys(loaded.AiApiKeys);
                MigrateLegacyAiKey(loaded);

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

            // Keep the legacy single field pointing at the selected provider's key so an
            // older build opening this file still finds a usable credential, and so the
            // field can never hold one provider's secret while another is selected.
            _current.AiApiKey = _current.GetAiApiKey(_current.AiProvider);

            var snapshot = _current;

            // Encrypt secrets at the persistence boundary: plaintext never touches disk.
            var persisted = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(snapshot));
            if (persisted == null) return;

            persisted.AiApiKey = SettingsSecretProtector.Protect(persisted.AiApiKey);
            persisted.AiApiKeys = ProtectAiKeys(persisted.AiApiKeys);

            string json = JsonSerializer.Serialize(persisted, new JsonSerializerOptions { WriteIndented = true });
            SettingsFileStore.SaveAtomic(SettingsFile, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Moves a pre-upgrade single key into the per-provider map so the provider it was
    /// entered for keeps working, and so switching providers no longer sends it
    /// everywhere. Idempotent: a settings file that already has the entry is untouched.
    /// </summary>
    private static void MigrateLegacyAiKey(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.AiApiKey))
        {
            return;
        }

        settings.AiApiKeys ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string provider = string.IsNullOrWhiteSpace(settings.AiProvider) ? "BuiltIn" : settings.AiProvider;

        if (!settings.AiApiKeys.ContainsKey(provider))
        {
            settings.AiApiKeys[provider] = settings.AiApiKey;
        }
    }

    private static Dictionary<string, string> ProtectAiKeys(Dictionary<string, string>? keys)
    {
        var protectedKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (keys == null)
        {
            return protectedKeys;
        }

        foreach (var pair in keys)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            protectedKeys[pair.Key.Trim()] = SettingsSecretProtector.Protect(pair.Value);
        }

        return protectedKeys;
    }

    private static Dictionary<string, string> UnprotectAiKeys(Dictionary<string, string>? keys)
    {
        var plaintextKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (keys == null)
        {
            return plaintextKeys;
        }

        foreach (var pair in keys)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            plaintextKeys[pair.Key.Trim()] = SettingsSecretProtector.Unprotect(pair.Value);
        }

        return plaintextKeys;
    }

    private static void Normalize(AppSettings settings)
    {
        settings.AutoCleanIntervalHours = Math.Clamp(settings.AutoCleanIntervalHours, 1, 168);
        settings.LowDiskAlertThresholdGb = Math.Clamp(settings.LowDiskAlertThresholdGb, 1, 500);
        settings.MemoryAutoOptimizeIntervalHours = Math.Clamp(settings.MemoryAutoOptimizeIntervalHours, 1, 72);
        settings.MemoryAutoOptimizeFreeRamThresholdPercent = Math.Clamp(settings.MemoryAutoOptimizeFreeRamThresholdPercent, 5, 95);
        settings.AutoCleanIdleMinutes = Math.Clamp(settings.AutoCleanIdleMinutes, 1, 120);
        settings.CustomPathExclusions ??= new List<string>();
        settings.CustomExtensionExclusions ??= new List<string>();

        // System.Text.Json rebuilds the dictionary with its default comparer, so the
        // case-insensitive one declared on the property does not survive a round trip.
        // Rebuild here so "gemini" and "Gemini" cannot become two different keys.
        var normalizedKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (settings.AiApiKeys != null)
        {
            foreach (var pair in settings.AiApiKeys)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                {
                    normalizedKeys[pair.Key.Trim()] = pair.Value.Trim();
                }
            }
        }
        settings.AiApiKeys = normalizedKeys;

        WinTempCleaner.Core.Safety.ProtectionPolicy.SetCustomExclusions(settings.CustomPathExclusions, settings.CustomExtensionExclusions);
    }
}
