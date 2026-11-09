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

    /// <summary>
    /// The AI providers, in the order the settings show them. Built from Core's catalog
    /// rather than a second hand-written list: the copy that lived here still labelled
    /// OpenAI "GPT-4o-mini" long after the request path had moved to gpt-5.4-mini.
    /// </summary>
    public static readonly (string Tag, string Label)[] AiProviders = BuildAiProviders();

    private static (string Tag, string Label)[] BuildAiProviders()
    {
        IReadOnlyList<AiProviderOption> options = AiModelCatalog.GetProviderOptions();
        var providers = new (string Tag, string Label)[options.Count];
        for (int i = 0; i < options.Count; i++)
        {
            providers[i] = (options[i].Id, options[i].Label);
        }

        return providers;
    }

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
        RefreshAiModelsCommand = new AsyncRelayCommand(() => RefreshAiModelsAsync(force: true));
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

    /// <summary>
    /// The row the picker shows as chosen. Separate from <see cref="AiModelName"/> because
    /// a refresh can drop the selected row: it must not clear the saved id, and an
    /// id the provider no longer lists must still survive the next settings write.
    /// </summary>
    [ObservableProperty]
    public partial AiModelOption? SelectedAiModel { get; set; }

    [ObservableProperty]
    public partial string AiOllamaEndpoint { get; set; } = "http://localhost:11434";

    [ObservableProperty]
    public partial bool AutoQueryAiForLargeFiles { get; set; }

    [ObservableProperty]
    public partial string AiTestStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsTestingAi { get; set; }

    /// <summary>One line about where the selected provider sends the request.</summary>
    public string AiProviderDescription => EnableOnlineAiSafety
        ? AiModelCatalog.DescribeProvider(AiProvider)
        : "OFF - large-file analysis uses the local knowledge base. Turn the switch above on (or pick a provider) to use this provider.";

    /// <summary>
    /// The rows the picker currently shows: the provider's own list (or the catalog until a
    /// key arrives). A saved id that neither list contains keeps its own row so a choice is
    /// never silently forgotten.
    /// </summary>
    public ObservableCollection<AiModelOption> AiModelSuggestions { get; } = [];

    /// <summary>The full rows behind the picker, in provider order.</summary>
    private IReadOnlyList<AiModelOption> _aiModelPickerSource = Array.Empty<AiModelOption>();

    /// <summary>True when the rows came from the provider rather than the static catalog.</summary>
    private bool _aiModelPickerFromProvider;

    /// <summary>True while the visible rows are being replaced, so a transient null
    /// selection cannot overwrite the saved model id.</summary>
    private bool _isRebuildingAiModelPicker;

    /// <summary>One line under the model picker: loading, how many models, or why not.</summary>
    [ObservableProperty]
    public partial string AiModelStatusText { get; set; } = string.Empty;

    /// <summary>True while a model listing is in flight.</summary>
    [ObservableProperty]
    public partial bool IsLoadingAiModels { get; set; }

    /// <summary>
    /// Models the current key can actually call, as the provider reported them. Null means
    /// "not asked yet", which is why the picker falls back to the catalog.
    /// </summary>
    private IReadOnlyList<AiModelOption>? _aiFetchedModels;

    /// <summary>The provider+key+endpoint the fetched list belongs to.</summary>
    private string? _aiFetchedFor;

    /// <summary>Cancels the in-flight listing when the key, endpoint or provider changes.</summary>
    private CancellationTokenSource? _aiModelRefreshCts;

    /// <summary>Guards against an older listing overwriting a newer one.</summary>
    private int _aiModelFetchGeneration;

    /// <summary>
    /// Raised when the key, endpoint or provider changed, so the view can debounce and ask
    /// the provider for its model list. The view owns the timer because only it has a
    /// dispatcher.
    /// </summary>
    public event Action? AiModelRefreshRequested;

    /// <summary>True when the Refresh button should be clickable.</summary>
    public bool CanRefreshAiModels => !IsLoadingAiModels && AiProvider != "BuiltIn";

    /// <summary>
    /// The master switch gates every AI control, so a user cannot configure a provider
    /// that the scan path will never call.
    /// </summary>
    public bool CanEditAi => EnableOnlineAiSafety;

    /// <summary>Test is only meaningful while AI is on and no other test is in flight.</summary>
    public bool CanTestAi => EnableOnlineAiSafety && !IsTestingAi;

    /// <summary>
    /// WPF's UpdateAiSettingsUiVisibility: a key is only asked for from providers that
    /// need one, the endpoint only from Ollama, and neither shows a model field.
    /// </summary>
    public bool ShowAiApiKeyRow =>
        AiModelCatalog.TryGetProvider(AiProvider, out AiProviderDescriptor descriptor) &&
        descriptor.RequiresApiKey;

    public bool ShowAiOllamaRow => AiProvider == "Ollama";

    public bool ShowAiModelRow => ShowAiApiKeyRow || ShowAiOllamaRow;

    /// <summary>The saved key for <paramref name="provider"/>, never another provider's.</summary>
    public string GetAiApiKey(string provider) => SettingsService.Current.GetAiApiKey(provider);

    /// <summary>
    /// Forgets the saved key for the selected provider. An empty key is how the store
    /// represents "no credential", so this is a real removal rather than a blank string
    /// that would still be sent.
    /// </summary>
    public void RemoveAiApiKey()
    {
        AiApiKey = string.Empty;
        Persist(s => s.SetAiApiKey(s.AiProvider, string.Empty));

        // Without a key there is no live list; fall back to the catalog and say why.
        ResetAiFetchedModels();
        PopulateAiModelSuggestions(null);
        AiModelStatusText = "Add an API key to load the models this account can use.";
    }

    /// <summary>
    /// "Ask AI don't work" was often really "which model is actually being hit?".
    /// This surfaces the model the next request will use - the saved pick, or the provider
    /// default when nothing is chosen - so the effective model is visible without reading
    /// the code or opening the drop-down.
    /// </summary>
    public string AiDefaultModelText
    {
        get
        {
            if (AiProvider == "BuiltIn")
            {
                return "The built-in engine answers without a model call.";
            }

            string fallback = AiModelCatalog.ResolveModelId(AiProvider, userModelName: null);
            return string.IsNullOrWhiteSpace(AiModelName)
                ? "Pick a model. With none chosen the provider default is used: " +
                  $"{AiModelCatalog.DescribeChosenModel(AiProvider, fallback)}."
                : $"Requests use: {AiModelCatalog.DescribeChosenModel(AiProvider, AiModelName)}";
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

    public IAsyncRelayCommand RefreshAiModelsCommand { get; }

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
        AiApiKey = s.GetAiApiKey(AiProvider);
        AiModelName = s.AiModelName;
        AiOllamaEndpoint = string.IsNullOrWhiteSpace(s.AiOllamaEndpoint) ? "http://localhost:11434" : s.AiOllamaEndpoint;
        AutoQueryAiForLargeFiles = s.AutoQueryAiForLargeFiles && s.EnableOnlineAiSafety;

        // Reopening the panel on a possibly different account: drop any list fetched
        // earlier and let the view's open path ask again.
        ResetAiFetchedModels();
        PopulateAiModelSuggestions(null);
        AiModelStatusText = string.Empty;
        OnPropertyChanged(nameof(AiDefaultModelText));

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
        OnPropertyChanged(nameof(AiProviderDescription));
        OnPropertyChanged(nameof(CanEditAi));
        OnPropertyChanged(nameof(CanTestAi));
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
            s.AiApiKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

    /// <summary>
    /// Asks the provider for the models the current key can use and fills the picker with
    /// them. Failure is reported but never fatal: the catalog list and the manual box stay
    /// usable, which is what a provider outage or an unverified key looks like.
    /// </summary>
    /// <param name="force">Ask even when the key/endpoint identity has not changed.</param>
    /// <param name="quiet">
    /// A background re-ask. It does not show "Loading...", does not clear the list on
    /// failure, and only rebuilds the picker when the provider actually answered with a
    /// different set - replacing the bound collection closes an open dropdown for no
    /// reason.
    /// </param>
    public async Task RefreshAiModelsAsync(bool force, bool quiet = false)
    {
        string provider = AiProvider;
        string key = AiApiKey?.Trim() ?? string.Empty;
        string endpoint = AiOllamaEndpoint?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(provider) ||
            provider.Equals("BuiltIn", StringComparison.OrdinalIgnoreCase))
        {
            if (quiet)
            {
                return;
            }

            ResetAiFetchedModels();
            PopulateAiModelSuggestions(null);
            AiModelStatusText = string.Empty;
            return;
        }

        if (AiModelCatalog.TryGetProvider(provider, out var descriptor) &&
            descriptor.RequiresApiKey && string.IsNullOrWhiteSpace(key))
        {
            if (quiet)
            {
                return;
            }

            // Nothing to ask with. Show the catalog and say so instead of firing a request
            // that can only come back 401.
            ResetAiFetchedModels();
            PopulateAiModelSuggestions(null);
            AiModelStatusText = "Add an API key to load the models this account can use.";
            return;
        }

        string identity = $"{provider}|{key}|{endpoint}";
        if (!force && _aiFetchedModels is not null &&
            string.Equals(_aiFetchedFor, identity, StringComparison.Ordinal))
        {
            return;
        }

        _aiModelRefreshCts?.Cancel();
        _aiModelRefreshCts?.Dispose();
        _aiModelRefreshCts = new CancellationTokenSource();
        CancellationToken ct = _aiModelRefreshCts.Token;
        int generation = ++_aiModelFetchGeneration;

        if (!quiet)
        {
            IsLoadingAiModels = true;
            AiModelStatusText = $"Loading models from {provider}...";
        }

        try
        {
            IReadOnlyList<AiModelOption> models =
                await AiProviderClient.ListModelsAsync(provider, key, endpoint, ct);

            if (generation != _aiModelFetchGeneration) return;

            bool changed = !AiModelCatalog.IsSameModelSet(_aiFetchedModels, models);
            _aiFetchedModels = models;
            _aiFetchedFor = identity;

            // A quiet answer that matches what is already shown is left alone: rebuilding
            // the collection would close the dropdown the user may be reading.
            if (!quiet || changed)
            {
                PopulateAiModelSuggestions(models);
                AiModelStatusText = AiModelCatalog.DescribeFetchedModels(provider, models, AiModelName);
            }
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer request. Nothing to report.
        }
        catch (AiProviderException ex)
        {
            if (generation != _aiModelFetchGeneration) return;

            if (quiet)
            {
                AiModelStatusText = "Could not refresh models: " + ex.UserMessage +
                    " Showing the last list.";
                return;
            }

            ResetAiFetchedModels();
            PopulateAiModelSuggestions(null);
            AiModelStatusText = "Could not load models: " + ex.UserMessage;
        }
        catch (Exception ex)
        {
            if (generation != _aiModelFetchGeneration) return;

            if (quiet)
            {
                AiModelStatusText = "Could not refresh models: " + ex.Message +
                    " Showing the last list.";
                return;
            }

            ResetAiFetchedModels();
            PopulateAiModelSuggestions(null);
            AiModelStatusText = "Could not load models: " + ex.Message;
        }
        finally
        {
            if (generation == _aiModelFetchGeneration)
            {
                IsLoadingAiModels = false;
            }
        }
    }

    /// <summary>
    /// Rebuilds the picker. The provider's own list wins over the catalog: the catalog is
    /// only a starting point and a model it lists may be unavailable to this account. A
    /// saved id missing from both keeps its own row so the choice survives the round trip.
    /// </summary>
    private void PopulateAiModelSuggestions(IReadOnlyList<AiModelOption>? models)
    {
        IReadOnlyList<AiModelOption> source =
            models ?? _aiFetchedModels ?? AiModelCatalog.GetSuggestedModels(AiProvider);

        // A saved id that neither list contains keeps its own row so the choice survives a
        // round trip; the rows are then narrowed to the free ones when the provider
        // publishes pricing. OpenRouter therefore never offers a row that answers 402 on a
        // zero-credit account, while a provider with no pricing keeps its full list.
        _aiModelPickerSource = AiModelCatalog.FreeModelsOnly(
            AiModelCatalog.WithSavedModel(source, AiModelName, AiProvider));
        _aiModelPickerFromProvider = models is not null || _aiFetchedModels is not null;

        RebuildAiModelPicker();
    }

    /// <summary>
    /// Rebuilds the visible rows from the full set. The saved model is re-selected whenever
    /// its row is present; when the provider no longer lists it, the selection is cleared
    /// but <see cref="AiModelName"/> keeps the value that will be sent.
    /// </summary>
    private void RebuildAiModelPicker()
    {
        IReadOnlyList<AiModelOption> visible = _aiModelPickerSource;

        // Assigning SelectedAiModel while the collection is being replaced would let a
        // transient null overwrite the saved id, so the flag keeps the two in step.
        _isRebuildingAiModelPicker = true;
        try
        {
            AiModelSuggestions.Clear();
            foreach (AiModelOption option in visible)
            {
                AiModelSuggestions.Add(option);
            }

            SelectedAiModel = visible.FirstOrDefault(
                option => string.Equals(option.Id, AiModelName, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _isRebuildingAiModelPicker = false;
        }
    }

    private void ResetAiFetchedModels()
    {
        _aiModelFetchGeneration++;
        _aiModelRefreshCts?.Cancel();
        _aiFetchedModels = null;
        _aiFetchedFor = null;
    }

    private async Task TestAiConnectionAsync()
    {
        if (IsTestingAi)
        {
            return;
        }

        // Validate locally first. A missing key used to spend a network round trip only
        // to come back as "connection failed", which read like an outage.
        IReadOnlyList<AiSettingsIssue> issues = AiSettingsValidator.Validate(
            AiProvider,
            AiApiKey,
            AiOllamaEndpoint,
            AiModelName,
            EnableOnlineAiSafety,
            AutoQueryAiForLargeFiles);

        if (!AiSettingsValidator.IsValid(issues))
        {
            AiTestStatusText = "⚠ " + AiSettingsValidator.Describe(issues);
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
        int removed = OnlineFileIntelligenceService.ClearCache();
        AiTestStatusText = removed == 0
            ? "AI reports cache was already empty."
            : $"AI reports cache cleared ({removed} {(removed == 1 ? "item" : "items")}).";
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
    {
        Persist(s => s.EnableOnlineAiSafety = value);
        OnPropertyChanged(nameof(CanEditAi));
        OnPropertyChanged(nameof(CanTestAi));
        OnPropertyChanged(nameof(AiProviderDescription));

        // Auto-query is gated on this switch in the scan path, so leaving it on while AI
        // is off would promise a batch that can never run.
        if (!_isLoading && !value && AutoQueryAiForLargeFiles)
        {
            AutoQueryAiForLargeFiles = false;
        }
    }

    partial void OnAiProviderChanged(string value)
    {
        OnPropertyChanged(nameof(ShowAiApiKeyRow));
        OnPropertyChanged(nameof(ShowAiOllamaRow));
        OnPropertyChanged(nameof(ShowAiModelRow));
        OnPropertyChanged(nameof(AiDefaultModelText));
        OnPropertyChanged(nameof(AiProviderDescription));

        // Choosing a provider IS the opt-in to using it. Leaving the master switch off made
        // the choice inert: Large Files answered from the local knowledge base and the row
        // said "Local Intelligence (Offline)", which reads as if the provider was ignored.
        if (!_isLoading &&
            !string.IsNullOrWhiteSpace(value) &&
            !value.Equals("BuiltIn", StringComparison.OrdinalIgnoreCase) &&
            !EnableOnlineAiSafety)
        {
            EnableOnlineAiSafety = true;
            Log($"Online file intelligence enabled for {value}.", LogLevel.Info);
        }

        // The suggestion list follows the provider. A new provider is a different account
        // and a different model namespace: the id chosen for the previous one is not a
        // valid id here, so it is cleared along with the fetched list.
        if (!_isLoading)
        {
            AiModelName = string.Empty;
        }

        ResetAiFetchedModels();
        PopulateAiModelSuggestions(null);
        OnPropertyChanged(nameof(CanRefreshAiModels));

        if (!string.IsNullOrWhiteSpace(value))
        {
            Persist(s => s.AiProvider = value);
        }

        if (!_isLoading)
        {
            AiModelStatusText = string.Empty;
            AiModelRefreshRequested?.Invoke();
        }
    }

    partial void OnAiApiKeyChanged(string value)
    {
        // The box is pre-filled from GetAiApiKey(provider), so an empty value means the
        // user cleared it and a removal is intended. The store keeps one key per
        // provider, so switching providers can no longer send the wrong credential.
        Persist(s => s.SetAiApiKey(s.AiProvider, value));

        // A different key is a different account, so any fetched list is now a lie.
        if (!_isLoading)
        {
            ResetAiFetchedModels();
            AiModelRefreshRequested?.Invoke();
        }
    }

    partial void OnIsLoadingAiModelsChanged(bool value)
        => OnPropertyChanged(nameof(CanRefreshAiModels));

    partial void OnIsTestingAiChanged(bool value) => OnPropertyChanged(nameof(CanTestAi));

    partial void OnAiModelNameChanged(string value)
    {
        Persist(s => s.AiModelName = value.Trim());
        OnPropertyChanged(nameof(AiDefaultModelText));
    }

    /// <summary>
    /// The picker's chosen row is the only place a new model id comes from. A null
    /// selection happens while a refresh drops the saved row, and must not erase it.
    /// </summary>
    partial void OnSelectedAiModelChanged(AiModelOption? value)
    {
        if (_isRebuildingAiModelPicker || value is null)
        {
            return;
        }

        if (!string.Equals(AiModelName, value.Id, StringComparison.Ordinal))
        {
            AiModelName = value.Id;
        }
    }

    partial void OnAiOllamaEndpointChanged(string value)
    {
        string endpoint = string.IsNullOrWhiteSpace(value) ? "http://localhost:11434" : value.Trim();
        Persist(s => s.AiOllamaEndpoint = endpoint);

        if (!_isLoading)
        {
            ResetAiFetchedModels();
            AiModelRefreshRequested?.Invoke();
        }
    }

    partial void OnAutoQueryAiForLargeFilesChanged(bool value)
        => Persist(s => s.AutoQueryAiForLargeFiles = value);
}
