using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace Deltempo.App.WinUI.ViewModels;

/// <summary>
/// WinUI 3 port of the WPF settings surface (MainWindow.Settings.cs plus the
/// settings modal in MainWindow.xaml).
///
/// Every value here is a shared setting: it is read from and written back to Core's
/// SettingsService, so the WPF app and this shell always agree. Nothing is stored in
/// the UI layer, and no setting is re-implemented here - the clamps live in
/// SettingsService.Normalize, exactly as they did in the WPF app.
///
/// The theme choice is the one exception to "write it straight back": the shell owns
/// the actual theme, so the panel asks for a change and the shell applies it. The
/// always-on-top switch is the second: only the shell can talk to its own window.
///
/// WPF put these on four tabs behind Save and Cancel. A tab is still a tab here - the
/// same four categories, the same controls in the same order - but there is no Save:
/// each control writes its setting as it changes, which is the pattern the rest of
/// this shell already uses, and it removes the way a user could close the modal
/// believing a change had been applied.
/// </summary>
public sealed partial class SettingsWorkspaceViewModel : ObservableObject
{
    /// <summary>The four categories WPF's settings modal tabbed between.</summary>
    public const string GeneralCategory = "GENERAL";
    public const string UpdatesCategory = "UPDATES";
    public const string MemoryCategory = "MEMORY";
    public const string SafetyCategory = "SAFETY";

    /// <summary>The languages the WPF app ships translations for.</summary>
    public static readonly (string Code, string Name)[] Languages =
    {
        ("en", "English"),
        ("ar", "العربية"),
        ("es", "Español"),
        ("fr", "Français"),
        ("de", "Deutsch"),
    };

    /// <summary>The AI providers WPF offered, in its order, with its labels.</summary>
    public static readonly (string Tag, string Label)[] AiProviders =
    {
        ("BuiltIn", "Built-in Web Knowledge (Free, No Key)"),
        ("OpenRouter", "OpenRouter (Universal / Multi-Model)"),
        ("Gemini", "Google Gemini (Free API Key)"),
        ("Groq", "Groq (Ultra-Fast Free Tier)"),
        ("OpenAI", "OpenAI (GPT-4o-mini)"),
        ("Ollama", "Ollama (Local & Private)"),
    };

    /// <summary>The update channels WPF offered.</summary>
    public static readonly (string Tag, string Label)[] UpdateChannels =
    {
        ("Stable", "Stable (Recommended)"),
        ("PreRelease", "Beta / Pre-Release"),
    };

    /// <summary>WPF's update frequency choices, including "manual only" (0).</summary>
    public static readonly (int Days, string Label)[] UpdateFrequencies =
    {
        (1, "Every 24 Hours (Daily)"),
        (3, "Every 3 Days"),
        (7, "Every 7 Days (Weekly)"),
        (0, "Manual Only"),
    };

    /// <summary>WPF's auto-clean intervals.</summary>
    public static readonly (int Hours, string Label)[] AutoCleanIntervals =
    {
        (6, "Every 6 Hours"),
        (12, "Every 12 Hours"),
        (24, "Every 24 Hours"),
    };

    /// <summary>WPF's idle waits.</summary>
    public static readonly (int Minutes, string Label)[] IdleWaits =
    {
        (5, "After 5 Minutes"),
        (15, "After 15 Minutes"),
        (30, "After 30 Minutes"),
        (60, "After 60 Minutes"),
    };

    /// <summary>WPF's memory auto-optimization intervals.</summary>
    public static readonly (int Hours, string Label)[] MemoryIntervals =
    {
        (1, "Every 1 Hour"),
        (2, "Every 2 Hours"),
        (4, "Every 4 Hours"),
        (8, "Every 8 Hours"),
        (12, "Every 12 Hours"),
        (24, "Every 24 Hours"),
        (0, "Disabled"),
    };

    /// <summary>WPF's free-RAM trigger thresholds.</summary>
    public static readonly (int Percent, string Label)[] MemoryThresholds =
    {
        (10, "Below 10%"),
        (20, "Below 20%"),
        (30, "Below 30%"),
        (40, "Below 40%"),
        (50, "Below 50%"),
        (0, "Disabled"),
    };

    /// <summary>WPF's low-disk thresholds.</summary>
    public static readonly (int Gigabytes, string Label)[] DiskThresholds =
    {
        (5, "Below 5 GB"),
        (10, "Below 10 GB"),
        (15, "Below 15 GB"),
        (20, "Below 20 GB"),
        (30, "Below 30 GB"),
        (50, "Below 50 GB"),
    };

    public SettingsWorkspaceViewModel()
    {
        OpenReleasesPageCommand = new RelayCommand(OpenReleasesPage);
        ResetToDefaultsCommand = new RelayCommand(ResetToDefaults);
        AddExcludedPathCommand = new RelayCommand(AddExcludedPath);
        RemoveExcludedPathCommand = new RelayCommand(RemoveExcludedPath);
        AddExcludedExtensionCommand = new RelayCommand(AddExcludedExtension);
        RemoveExcludedExtensionCommand = new RelayCommand(RemoveExcludedExtension);
        TestAiConnectionCommand = new AsyncRelayCommand(TestAiConnectionAsync);
        ClearAiCacheCommand = new RelayCommand(ClearAiCache);

        LoadFromSettings();
    }

    // ---------------------------------------------------------------------
    // Category (WPF's four tabs)
    // ---------------------------------------------------------------------

    /// <summary>Which of the four cards is showing. WPF's SettingsTab_Checked.</summary>
    [ObservableProperty]
    public partial string Category { get; set; } = GeneralCategory;

    public bool IsGeneralCategory
    {
        get => Category == GeneralCategory;
        set { if (value) Category = GeneralCategory; }
    }

    public bool IsUpdatesCategory
    {
        get => Category == UpdatesCategory;
        set { if (value) Category = UpdatesCategory; }
    }

    public bool IsMemoryCategory
    {
        get => Category == MemoryCategory;
        set { if (value) Category = MemoryCategory; }
    }

    public bool IsSafetyCategory
    {
        get => Category == SafetyCategory;
        set { if (value) Category = SafetyCategory; }
    }

    public bool ShowGeneral => Category == GeneralCategory;
    public bool ShowUpdates => Category == UpdatesCategory;
    public bool ShowMemory => Category == MemoryCategory;
    public bool ShowSafety => Category == SafetyCategory;

    partial void OnCategoryChanged(string value)
    {
        OnPropertyChanged(nameof(IsGeneralCategory));
        OnPropertyChanged(nameof(IsUpdatesCategory));
        OnPropertyChanged(nameof(IsMemoryCategory));
        OnPropertyChanged(nameof(IsSafetyCategory));
        OnPropertyChanged(nameof(ShowGeneral));
        OnPropertyChanged(nameof(ShowUpdates));
        OnPropertyChanged(nameof(ShowMemory));
        OnPropertyChanged(nameof(ShowSafety));

        // WPF played the click cue from its tab handler.
        SoundService.PlayClickSound();
    }

    // ---------------------------------------------------------------------
    // Appearance (WPF had these on the header, not in the modal)
    // ---------------------------------------------------------------------

    [ObservableProperty]
    public partial bool IsDarkTheme { get; set; }

    [ObservableProperty]
    public partial bool SoundEnabled { get; set; }

    [ObservableProperty]
    public partial string Language { get; set; }

    // ---------------------------------------------------------------------
    // General: Auto-Pilot guardian
    // ---------------------------------------------------------------------

    [ObservableProperty]
    public partial bool EnableAutoPilot { get; set; }

    [ObservableProperty]
    public partial int AutoCleanIntervalHours { get; set; }

    [ObservableProperty]
    public partial bool AutoCleanOnIdle { get; set; }

    [ObservableProperty]
    public partial int AutoCleanIdleMinutes { get; set; }

    // ---------------------------------------------------------------------
    // General: tray behaviour
    // ---------------------------------------------------------------------

    [ObservableProperty]
    public partial bool MinimizeToTray { get; set; }

    [ObservableProperty]
    public partial bool AutoCleanNotify { get; set; }

    [ObservableProperty]
    public partial bool EnableShellContextMenu { get; set; }

    [ObservableProperty]
    public partial bool EnableWeeklyScheduledTask { get; set; }

    // ---------------------------------------------------------------------
    // Updates
    // ---------------------------------------------------------------------

    [ObservableProperty]
    public partial bool CheckUpdatesOnStartup { get; set; }

    [ObservableProperty]
    public partial int UpdateCheckFrequencyDays { get; set; }

    [ObservableProperty]
    public partial string UpdateChannel { get; set; }

    [ObservableProperty]
    public partial bool AutoDownloadUpdates { get; set; }

    [ObservableProperty]
    public partial bool IsCheckingUpdates { get; set; }

    [ObservableProperty]
    public partial string UpdateStatusText { get; set; } = "Update status has not been checked yet.";

    /// <summary>WPF showed the last successful check under the version badge.</summary>
    public string LastCheckedText
    {
        get
        {
            string stamp = SettingsService.Current.LastUpdateCheckTimestamp;
            if (string.IsNullOrWhiteSpace(stamp))
            {
                return "Last checked: Never";
            }

            return DateTime.TryParse(stamp, out DateTime parsed)
                ? $"Last checked: {parsed:g}"
                : "Last checked: Never";
        }
    }

    /// <summary>The version this build reports, straight from the assembly.</summary>
    public string CurrentVersionText { get; } =
        $"v{typeof(SettingsWorkspaceViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"}";

    // ---------------------------------------------------------------------
    // Memory optimizer
    // ---------------------------------------------------------------------

    [ObservableProperty]
    public partial bool MemoryAutoOptimizeEnabled { get; set; }

    [ObservableProperty]
    public partial int MemoryAutoOptimizeIntervalHours { get; set; }

    [ObservableProperty]
    public partial int MemoryAutoOptimizeFreeRamThresholdPercent { get; set; }

    [ObservableProperty]
    public partial bool MemoryShowInTray { get; set; }

    [ObservableProperty]
    public partial bool MemoryAlwaysOnTop { get; set; }

    [ObservableProperty]
    public partial bool MemoryCompactMode { get; set; }

    [ObservableProperty]
    public partial bool MemoryCloseToTray { get; set; }

    [ObservableProperty]
    public partial bool MemoryShowNotifications { get; set; }

    // ---------------------------------------------------------------------
    // Safety: deletion, low disk, pruning, exclusions, AI
    // ---------------------------------------------------------------------

    [ObservableProperty]
    public partial bool SendToRecycleBin { get; set; }

    [ObservableProperty]
    public partial bool LowDiskAlertEnabled { get; set; }

    [ObservableProperty]
    public partial int LowDiskThresholdGb { get; set; }

    [ObservableProperty]
    public partial bool AggressiveEmptyFolderPrune { get; set; }

    [ObservableProperty]
    public partial string NewExcludedPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewExcludedExtension { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? SelectedExcludedPath { get; set; }

    [ObservableProperty]
    public partial string? SelectedExcludedExtension { get; set; }

    /// <summary>The protected folders, bound to the list WPF showed.</summary>
    public ObservableCollection<string> ExcludedPaths { get; } = [];

    /// <summary>The protected extensions, bound to the list WPF showed.</summary>
    public ObservableCollection<string> ExcludedExtensions { get; } = [];

    // ---------------------------------------------------------------------
    // Safety: AI & online file intelligence
    // ---------------------------------------------------------------------

    [ObservableProperty]
    public partial bool EnableOnlineAiSafety { get; set; }

    [ObservableProperty]
    public partial string AiProvider { get; set; } = "BuiltIn";

    [ObservableProperty]
    public partial string AiApiKey { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AiModelName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AiOllamaEndpoint { get; set; } = "http://localhost:11434";

    [ObservableProperty]
    public partial bool AutoQueryAiForLargeFiles { get; set; }

    [ObservableProperty]
    public partial string AiTestStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsTestingAi { get; set; }

    /// <summary>
    /// WPF's UpdateAiSettingsUiVisibility: a key is only asked for from providers that
    /// need one, the endpoint only from Ollama, and neither shows a model field.
    /// </summary>
    public bool ShowAiApiKeyRow => AiProvider is "Gemini" or "Groq" or "OpenAI" or "OpenRouter";

    public bool ShowAiOllamaRow => AiProvider == "Ollama";

    public bool ShowAiModelRow => ShowAiApiKeyRow || ShowAiOllamaRow;

    /// <summary>
    /// "Ask AI don't work" was often really "which model is actually being hit?".
    /// This surfaces the catalog default next to the custom-model box so the
    /// effective model is visible without reading the code. A typed name still
    /// wins - the catalog suggestion is never a restriction.
    /// </summary>
    public string AiDefaultModelText
    {
        get
        {
            string model = AiModelCatalog.ResolveModelId(AiProvider, userModelName: null);
            return string.IsNullOrEmpty(model)
                ? string.Empty
                : $"Leave blank to use the provider default: {model}";
        }
    }

    // ---------------------------------------------------------------------
    // Commands
    // ---------------------------------------------------------------------

    public IRelayCommand OpenReleasesPageCommand { get; }

    public IRelayCommand ResetToDefaultsCommand { get; }

    public IRelayCommand AddExcludedPathCommand { get; }

    public IRelayCommand RemoveExcludedPathCommand { get; }

    public IRelayCommand AddExcludedExtensionCommand { get; }

    public IRelayCommand RemoveExcludedExtensionCommand { get; }

    public IAsyncRelayCommand TestAiConnectionCommand { get; }

    public IRelayCommand ClearAiCacheCommand { get; }

    /// <summary>Raised when the user picks the other theme. The shell applies it.</summary>
    public event Action<bool>? ThemeChangeRequested;

    /// <summary>Raised when the always-on-top switch moves. Only the shell has a window.</summary>
    public event Action<bool>? AlwaysOnTopRequested;

    /// <summary>Raised when this panel wants a line in the shell's activity stream.</summary>
    public event Action<string, LogLevel>? LogRequested;

    /// <summary>Reports a line into the shell's activity stream.</summary>
    public void Log(string message, LogLevel level = LogLevel.Info)
        => LogRequested?.Invoke(message, level);

    /// <summary>
    /// NumberBox works in doubles; the settings are whole hours/minutes/gigabytes.
    /// These proxies are what the XAML binds to, and they round once and hand the
    /// integer to the setting.
    /// </summary>
    public double AutoCleanIdleMinutesValue
    {
        get => AutoCleanIdleMinutes;
        set
        {
            int minutes = (int)Math.Round(value);
            if (minutes != AutoCleanIdleMinutes)
            {
                AutoCleanIdleMinutes = minutes;
            }
        }
    }

    public double LowDiskThresholdValue
    {
        get => LowDiskThresholdGb;
        set
        {
            int gigabytes = (int)Math.Round(value);
            if (gigabytes != LowDiskThresholdGb)
            {
                LowDiskThresholdGb = gigabytes;
            }
        }
    }

    // ---------------------------------------------------------------------
    // Load / reset
    // ---------------------------------------------------------------------

    /// <summary>
    /// Reads every setting into the panel. WPF's LoadSettingsIntoUI ran on open and after
    /// a reset; here it also runs at construction.
    ///
    /// Loading assigns every property, which fires every write-through handler. Those
    /// handlers are what register registry verbs, create a scheduled task and start the
    /// auto-pilot, so a flag suppresses the ones with an effect outside this process:
    /// opening the settings screen must never re-register anything, and the self-test
    /// must never mutate the machine.
    /// </summary>
    public void LoadFromSettings()
    {
        _isLoading = true;
        try
        {
            LoadFromSettingsCore();
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>True while <see cref="LoadFromSettings"/> is assigning values.</summary>
    private bool _isLoading;

    private void LoadFromSettingsCore()
    {
        AppSettings s = SettingsService.Current;

        IsDarkTheme = s.IsDarkMode;
        SoundEnabled = s.SoundEnabled;
        Language = string.IsNullOrWhiteSpace(s.Language) ? "en" : s.Language;

        EnableAutoPilot = s.EnableAutoPilot;
        AutoCleanIntervalHours = s.AutoCleanIntervalHours;
        AutoCleanOnIdle = s.AutoCleanOnIdle;
        AutoCleanIdleMinutes = s.AutoCleanIdleMinutes;

        MinimizeToTray = s.MinimizeToTray;
        AutoCleanNotify = s.AutoCleanNotify;
        EnableShellContextMenu = s.EnableShellContextMenu;
        EnableWeeklyScheduledTask = s.EnableWeeklyScheduledTask;

        CheckUpdatesOnStartup = s.CheckUpdatesOnStartup;
        UpdateCheckFrequencyDays = s.UpdateCheckFrequencyDays;
        UpdateChannel = string.IsNullOrWhiteSpace(s.UpdateChannel) ? "Stable" : s.UpdateChannel;
        AutoDownloadUpdates = s.AutoDownloadUpdates;

        MemoryAutoOptimizeEnabled = s.MemoryAutoOptimizeEnabled;
        MemoryAutoOptimizeIntervalHours = s.MemoryAutoOptimizeIntervalHours;
        MemoryAutoOptimizeFreeRamThresholdPercent = s.MemoryAutoOptimizeFreeRamThresholdPercent;
        MemoryShowInTray = s.MemoryShowInTray;
        MemoryAlwaysOnTop = s.MemoryAlwaysOnTop;
        MemoryCompactMode = s.MemoryCompactMode;
        MemoryCloseToTray = s.MemoryCloseToTray;
        MemoryShowNotifications = s.MemoryShowNotifications;

        SendToRecycleBin = s.SendToRecycleBin;
        LowDiskAlertEnabled = s.LowDiskAlertEnabled;
        LowDiskThresholdGb = s.LowDiskAlertThresholdGb;
        AggressiveEmptyFolderPrune = s.AggressiveEmptyFolderPrune;

        EnableOnlineAiSafety = s.EnableOnlineAiSafety;
        AiProvider = string.IsNullOrWhiteSpace(s.AiProvider) ? "BuiltIn" : s.AiProvider;
        AiApiKey = s.AiApiKey;
        AiModelName = s.AiModelName;
        AiOllamaEndpoint = string.IsNullOrWhiteSpace(s.AiOllamaEndpoint) ? "http://localhost:11434" : s.AiOllamaEndpoint;
        AutoQueryAiForLargeFiles = s.AutoQueryAiForLargeFiles;

        ExcludedPaths.Clear();
        foreach (string path in s.CustomPathExclusions)
        {
            ExcludedPaths.Add(path);
        }

        ExcludedExtensions.Clear();
        foreach (string extension in s.CustomExtensionExclusions)
        {
            ExcludedExtensions.Add(extension);
        }

        OnPropertyChanged(nameof(AutoCleanIdleMinutesValue));
        OnPropertyChanged(nameof(LowDiskThresholdValue));
        OnPropertyChanged(nameof(LastCheckedText));
        OnPropertyChanged(nameof(ShowAiApiKeyRow));
        OnPropertyChanged(nameof(ShowAiOllamaRow));
        OnPropertyChanged(nameof(ShowAiModelRow));
        OnPropertyChanged(nameof(AiDefaultModelText));
        OnPropertyChanged(nameof(CachedAiReportsText));
    }

    /// <summary>
    /// WPF's ResetSettings_Click: every settings value back to a fresh AppSettings, then
    /// reload the controls. Shared state only - no files, no registry keys.
    /// </summary>
    private void ResetToDefaults()
    {
        Persist(s =>
        {
            var defaults = new AppSettings();
            s.EnableAutoPilot = defaults.EnableAutoPilot;
            s.MinimizeToTray = defaults.MinimizeToTray;
            s.AutoCleanNotify = defaults.AutoCleanNotify;
            s.SoundEnabled = defaults.SoundEnabled;
            s.SendToRecycleBin = defaults.SendToRecycleBin;
            s.LowDiskAlertEnabled = defaults.LowDiskAlertEnabled;
            s.LowDiskAlertThresholdGb = defaults.LowDiskAlertThresholdGb;
            s.EnableShellContextMenu = defaults.EnableShellContextMenu;
            s.EnableWeeklyScheduledTask = defaults.EnableWeeklyScheduledTask;
            s.CheckUpdatesOnStartup = defaults.CheckUpdatesOnStartup;
            s.AutoDownloadUpdates = defaults.AutoDownloadUpdates;
            s.UpdateChannel = defaults.UpdateChannel;
            s.UpdateCheckFrequencyDays = defaults.UpdateCheckFrequencyDays;
            s.AutoCleanIntervalHours = defaults.AutoCleanIntervalHours;
            s.MemoryAutoOptimizeEnabled = defaults.MemoryAutoOptimizeEnabled;
            s.MemoryShowInTray = defaults.MemoryShowInTray;
            s.MemoryAlwaysOnTop = defaults.MemoryAlwaysOnTop;
            s.MemoryCompactMode = defaults.MemoryCompactMode;
            s.MemoryCloseToTray = defaults.MemoryCloseToTray;
            s.MemoryShowNotifications = defaults.MemoryShowNotifications;
            s.MemoryAutoOptimizeIntervalHours = defaults.MemoryAutoOptimizeIntervalHours;
            s.MemoryAutoOptimizeFreeRamThresholdPercent = defaults.MemoryAutoOptimizeFreeRamThresholdPercent;
            s.EnableOnlineAiSafety = defaults.EnableOnlineAiSafety;
            s.AiProvider = defaults.AiProvider;
            s.AiApiKey = defaults.AiApiKey;
            s.AiModelName = defaults.AiModelName;
            s.AiOllamaEndpoint = defaults.AiOllamaEndpoint;
            s.AutoCleanOnIdle = defaults.AutoCleanOnIdle;
            s.AutoCleanIdleMinutes = defaults.AutoCleanIdleMinutes;
            s.AggressiveEmptyFolderPrune = defaults.AggressiveEmptyFolderPrune;
            s.CustomPathExclusions = new List<string>(defaults.CustomPathExclusions);
            s.CustomExtensionExclusions = new List<string>(defaults.CustomExtensionExclusions);
        });

        LoadFromSettings();
        SoundService.IsSoundEnabled = SettingsService.Current.SoundEnabled;
        AutoCleanService.Start();

        // The two registry-backed switches follow the values they were just reset to,
        // which is what WPF's Save button did on the same pass.
        ApplyShellContextMenu(SettingsService.Current.EnableShellContextMenu);
        ApplyWeeklyTask(SettingsService.Current.EnableWeeklyScheduledTask);

        LogRequested?.Invoke("Settings restored to factory defaults.", LogLevel.Info);
    }

    // ---------------------------------------------------------------------
    // Update check
    // ---------------------------------------------------------------------

    /// <summary>
    /// Marks the check as running. The check itself belongs to the shell, because a
    /// newer release opens a window-level offer - so this panel only shows the state
    /// the shell reports back.
    /// </summary>
    public void BeginUpdateCheck()
    {
        IsCheckingUpdates = true;
        UpdateStatusText = "Checking for updates...";
    }

    /// <summary>Shows what the shell's check found.</summary>
    public void ReportUpdateCheck(string status, bool isError = false)
    {
        IsCheckingUpdates = false;
        UpdateStatusText = status;
        OnPropertyChanged(nameof(LastCheckedText));
        LogRequested?.Invoke(status, isError ? LogLevel.Error : LogLevel.Info);
    }

    private void OpenReleasesPage()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/Beso1227/Deltempo/releases") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LogRequested?.Invoke($"Could not open the releases page: {ex.Message}", LogLevel.Error);
        }
    }

    // ---------------------------------------------------------------------
    // Exclusions
    // ---------------------------------------------------------------------

    /// <summary>Hands the folder the view's picker returned to the new-path box.</summary>
    public void SetBrowsedExcludedFolder(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            NewExcludedPath = path;
        }
    }

    private void AddExcludedPath()
    {
        string path = (NewExcludedPath ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (!ExcludedPaths.Any(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)))
        {
            ExcludedPaths.Add(path);
            NewExcludedPath = string.Empty;
            SaveExclusions();
            SoundService.PlayClickSound();
        }
    }

    private void RemoveExcludedPath()
    {
        if (SelectedExcludedPath is not null && ExcludedPaths.Remove(SelectedExcludedPath))
        {
            SelectedExcludedPath = null;
            SaveExclusions();
            SoundService.PlayClickSound();
        }
    }

    private void AddExcludedExtension()
    {
        string extension = (NewExcludedExtension ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(extension))
        {
            return;
        }

        if (!extension.StartsWith('.'))
        {
            extension = "." + extension;
        }

        if (!ExcludedExtensions.Any(e => string.Equals(e, extension, StringComparison.OrdinalIgnoreCase)))
        {
            ExcludedExtensions.Add(extension);
            NewExcludedExtension = string.Empty;
            SaveExclusions();
            SoundService.PlayClickSound();
        }
    }

    private void RemoveExcludedExtension()
    {
        if (SelectedExcludedExtension is not null && ExcludedExtensions.Remove(SelectedExcludedExtension))
        {
            SelectedExcludedExtension = null;
            SaveExclusions();
            SoundService.PlayClickSound();
        }
    }

    /// <summary>
    /// Persists both lists. SettingsService.Normalize pushes them into ProtectionPolicy
    /// on every save, so the very next clean sees the new whitelist - the same path WPF
    /// used from its Save button.
    /// </summary>
    private void SaveExclusions()
    {
        List<string> paths = ExcludedPaths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        List<string> extensions = ExcludedExtensions
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Persist(s =>
        {
            s.CustomPathExclusions = paths;
            s.CustomExtensionExclusions = extensions;
        });
    }

    // ---------------------------------------------------------------------
    // AI
    // ---------------------------------------------------------------------

    private async Task TestAiConnectionAsync()
    {
        if (IsTestingAi)
        {
            return;
        }

        IsTestingAi = true;
        AiTestStatusText = "Connecting & verifying provider...";

        try
        {
            var (ok, message) = await OnlineFileIntelligenceService.TestConnectionAsync(
                AiProvider, AiApiKey, AiOllamaEndpoint, AiModelName);

            AiTestStatusText = ok ? $"✅ {message}" : $"❌ {message}";
            LogRequested?.Invoke($"AI provider check ({AiProvider}): {message}", ok ? LogLevel.Success : LogLevel.Warning);
        }
        catch (Exception ex)
        {
            AiTestStatusText = $"❌ Test failed: {ex.Message}";
        }
        finally
        {
            IsTestingAi = false;
        }
    }

    private void ClearAiCache()
    {
        OnlineFileIntelligenceService.ClearCache();
        AiTestStatusText = "AI reports cache cleared (0 items).";
        OnPropertyChanged(nameof(CachedAiReportsText));
    }

    /// <summary>
    /// How many AI reports are cached on disk. WPF printed this under its test button
    /// ("Ready. Cached local AI reports: N files."), so the number is visible before a
    /// user asks the provider anything.
    /// </summary>
    public string CachedAiReportsText =>
        $"Cached local AI reports: {OnlineFileIntelligenceService.GetCacheCount()} file(s).";

    // ---------------------------------------------------------------------
    // Write-through
    // ---------------------------------------------------------------------

    /// <summary>
    /// Writes one setting and saves. Every handler below goes through this so a load -
    /// which assigns the same properties - cannot write the file twenty times over, and
    /// so a load cannot fire a side effect (a re-registered Explorer verb, a re-armed
    /// timer) that merely opening a settings screen must never cause.
    /// </summary>
    private void Persist(Action<AppSettings> apply)
    {
        if (_isLoading)
        {
            return;
        }

        SettingsService.Update(apply);
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        if (_isLoading)
        {
            return;
        }

        SettingsService.Update(s => s.IsDarkMode = value);
        ThemeChangeRequested?.Invoke(value);
    }

    partial void OnSoundEnabledChanged(bool value)
    {
        if (_isLoading)
        {
            return;
        }

        SettingsService.Update(s => s.SoundEnabled = value);

        // SoundService holds the switch the click/scrape effects actually read. The
        // WPF app pushes it on save; without this the toggle persisted a preference
        // that no sound ever consulted.
        SoundService.IsSoundEnabled = value;

        // Turning it on is the one moment the user needs proof that it works.
        if (value)
        {
            SoundService.PlayClickSound();
        }
    }

    partial void OnLanguageChanged(string value)
    {
        if (_isLoading || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        SettingsService.Update(s => s.Language = value);
        LocalizationService.CurrentLanguage = value;
    }

    partial void OnEnableAutoPilotChanged(bool value)
    {
        if (_isLoading)
        {
            return;
        }

        SettingsService.Update(s => s.EnableAutoPilot = value);
        AutoCleanService.Start();
    }

    partial void OnAutoCleanIntervalHoursChanged(int value)
    {
        if (_isLoading)
        {
            return;
        }

        SettingsService.Update(s => s.AutoCleanIntervalHours = value);
        AutoCleanService.Start();
    }

    partial void OnAutoCleanNotifyChanged(bool value)
        => Persist(s => s.AutoCleanNotify = value);

    partial void OnAutoCleanOnIdleChanged(bool value)
    {
        Persist(s => s.AutoCleanOnIdle = value);

        if (_isLoading)
        {
            return;
        }

        // The idle watcher lives in Core and is started and stopped by the same
        // service the WPF app used. Nothing here reimplements its timer.
        //
        // Start() re-reads every gate and stops the previous timers first, so it is
        // also the correct call when the user switches the watcher off: the old code
        // called StopAutoClean(), which cancels the run in flight but leaves the
        // 60-second timer armed.
        AutoCleanService.Start();
    }

    partial void OnAutoCleanIdleMinutesChanged(int value)
    {
        // SettingsService.Normalize clamps this to 1..600 on save; the same
        // normalisation runs here so the number the user sees is the number that is
        // actually stored.
        int clamped = Math.Clamp(value, 1, 600);
        Persist(s => s.AutoCleanIdleMinutes = clamped);
        OnPropertyChanged(nameof(AutoCleanIdleMinutesValue));

        if (_isLoading)
        {
            return;
        }

        // Re-arm with the new threshold. A running watcher would otherwise keep
        // polling the old number until the process restarted.
        AutoCleanService.Start();
    }

    partial void OnMinimizeToTrayChanged(bool value)
    {
        if (_isLoading)
        {
            return;
        }

        Persist(s => s.MinimizeToTray = value);

        // The icon is created once at launch, from MemoryShowInTray, in both shells.
        // Saying so beats pretending a switch took effect that did not.
        LogRequested?.Invoke(
            value
                ? "Minimize to tray enabled. The notification icon is created at the next launch."
                : "Minimize to tray disabled. The notification icon closes at the next launch.",
            LogLevel.Info);
    }

    partial void OnEnableShellContextMenuChanged(bool value)
    {
        if (!_isLoading)
        {
            ApplyShellContextMenu(value);
        }
    }

    /// <summary>
    /// WPF registered and unregistered the Explorer verbs from its Save button. The
    /// result is logged rather than swallowed: the registry is user-scoped but it can
    /// still fail, and a switch that says "on" while nothing was written is a lie.
    /// </summary>
    private void ApplyShellContextMenu(bool enabled)
    {
        Persist(s => s.EnableShellContextMenu = enabled);

        try
        {
            var (success, message) = enabled
                ? ShellExtensionService.Register()
                : ShellExtensionService.Unregister();

            if (!success)
            {
                LogRequested?.Invoke($"Explorer context menu: {message}", LogLevel.Warning);
            }
        }
        catch (Exception ex)
        {
            LogRequested?.Invoke($"Explorer context menu failed: {ex.Message}", LogLevel.Error);
        }
    }

    partial void OnEnableWeeklyScheduledTaskChanged(bool value)
    {
        if (!_isLoading)
        {
            ApplyWeeklyTask(value);
        }
    }

    /// <summary>
    /// WPF enabled and disabled the Task Scheduler job from its Save button. Still
    /// opt-in: the task is created only when the switch is turned on.
    /// </summary>
    private void ApplyWeeklyTask(bool enabled)
    {
        Persist(s => s.EnableWeeklyScheduledTask = enabled);

        try
        {
            var (success, message) = enabled
                ? TaskSchedulerService.EnableWeeklyTask()
                : TaskSchedulerService.DisableWeeklyTask();

            if (!success)
            {
                LogRequested?.Invoke($"Weekly maintenance task: {message}", LogLevel.Warning);
            }
        }
        catch (Exception ex)
        {
            LogRequested?.Invoke($"Weekly maintenance task failed: {ex.Message}", LogLevel.Error);
        }
    }

    partial void OnCheckUpdatesOnStartupChanged(bool value)
        => Persist(s => s.CheckUpdatesOnStartup = value);

    partial void OnUpdateCheckFrequencyDaysChanged(int value)
        => Persist(s => s.UpdateCheckFrequencyDays = value);

    partial void OnUpdateChannelChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        Persist(s => s.UpdateChannel = value);
    }

    partial void OnAutoDownloadUpdatesChanged(bool value)
        => Persist(s => s.AutoDownloadUpdates = value);

    partial void OnMemoryAutoOptimizeEnabledChanged(bool value)
        => Persist(s => s.MemoryAutoOptimizeEnabled = value);

    partial void OnMemoryAutoOptimizeIntervalHoursChanged(int value)
        => Persist(s => s.MemoryAutoOptimizeIntervalHours = value);

    partial void OnMemoryAutoOptimizeFreeRamThresholdPercentChanged(int value)
        => Persist(s => s.MemoryAutoOptimizeFreeRamThresholdPercent = value);

    partial void OnMemoryShowInTrayChanged(bool value)
    {
        if (_isLoading)
        {
            return;
        }

        Persist(s => s.MemoryShowInTray = value);

        // Same startup-only rule as WPF: the icon is created (or not) at launch.
        LogRequested?.Invoke(
            value
                ? "Tray icon enabled. It appears at the next launch."
                : "Tray icon disabled. It disappears at the next launch.",
            LogLevel.Info);
    }

    partial void OnMemoryAlwaysOnTopChanged(bool value)
    {
        Persist(s => s.MemoryAlwaysOnTop = value);

        if (!_isLoading)
        {
            AlwaysOnTopRequested?.Invoke(value);
        }
    }

    partial void OnMemoryCompactModeChanged(bool value)
        => Persist(s => s.MemoryCompactMode = value);

    partial void OnMemoryCloseToTrayChanged(bool value)
        => Persist(s => s.MemoryCloseToTray = value);

    partial void OnMemoryShowNotificationsChanged(bool value)
        => Persist(s => s.MemoryShowNotifications = value);

    partial void OnSendToRecycleBinChanged(bool value)
        => Persist(s => s.SendToRecycleBin = value);

    partial void OnLowDiskAlertEnabledChanged(bool value)
        => Persist(s => s.LowDiskAlertEnabled = value);

    partial void OnLowDiskThresholdGbChanged(int value)
    {
        int clamped = Math.Clamp(value, 1, 500);
        Persist(s => s.LowDiskAlertThresholdGb = clamped);
        OnPropertyChanged(nameof(LowDiskThresholdValue));
    }

    partial void OnAggressiveEmptyFolderPruneChanged(bool value)
        => Persist(s => s.AggressiveEmptyFolderPrune = value);

    partial void OnEnableOnlineAiSafetyChanged(bool value)
        => Persist(s => s.EnableOnlineAiSafety = value);

    partial void OnAiProviderChanged(string value)
    {
        OnPropertyChanged(nameof(ShowAiApiKeyRow));
        OnPropertyChanged(nameof(ShowAiOllamaRow));
        OnPropertyChanged(nameof(ShowAiModelRow));
        OnPropertyChanged(nameof(AiDefaultModelText));

        if (!string.IsNullOrWhiteSpace(value))
        {
            Persist(s => s.AiProvider = value);
        }
    }

    partial void OnAiApiKeyChanged(string value)
    {
        // Stored through SettingsService, which DPAPI-protects it at the persistence
        // boundary. Empty means "leave the saved key alone", matching WPF's
        // PasswordBox check; the field is pre-filled with the real key so an edit is
        // explicit.
        if (!string.IsNullOrEmpty(value))
        {
            Persist(s => s.AiApiKey = value);
        }
    }

    partial void OnAiModelNameChanged(string value)
        => Persist(s => s.AiModelName = value.Trim());

    partial void OnAiOllamaEndpointChanged(string value)
    {
        string endpoint = string.IsNullOrWhiteSpace(value) ? "http://localhost:11434" : value.Trim();
        Persist(s => s.AiOllamaEndpoint = endpoint);
    }

    partial void OnAutoQueryAiForLargeFilesChanged(bool value)
        => Persist(s => s.AutoQueryAiForLargeFiles = value);
}
