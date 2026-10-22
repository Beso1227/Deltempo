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
/// the actual theme, so the panel asks for a change and the shell applies it.
/// </summary>
public sealed partial class SettingsWorkspaceViewModel : ObservableObject
{
    /// <summary>The languages the WPF app ships translations for.</summary>
    public static readonly (string Code, string Name)[] Languages =
    {
        ("en", "English"),
        ("ar", "العربية"),
        ("es", "Español"),
        ("fr", "Français"),
        ("de", "Deutsch"),
    };

    public SettingsWorkspaceViewModel()
    {
        CheckForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync);
        OpenReleasesPageCommand = new RelayCommand(OpenReleasesPage);

        IsDarkTheme = SettingsService.Current.IsDarkMode;
        SoundEnabled = SettingsService.Current.SoundEnabled;
        Language = SettingsService.Current.Language;
        AutoCleanOnIdle = SettingsService.Current.AutoCleanOnIdle;
        AutoCleanIdleMinutes = SettingsService.Current.AutoCleanIdleMinutes;
        LowDiskAlertEnabled = SettingsService.Current.LowDiskAlertEnabled;
        LowDiskThresholdGb = SettingsService.Current.LowDiskAlertThresholdGb;
        CheckUpdatesOnStartup = SettingsService.Current.CheckUpdatesOnStartup;
    }

    [ObservableProperty]
    public partial bool IsDarkTheme { get; set; }

    [ObservableProperty]
    public partial bool SoundEnabled { get; set; }

    [ObservableProperty]
    public partial string Language { get; set; }

    [ObservableProperty]
    public partial bool AutoCleanOnIdle { get; set; }

    [ObservableProperty]
    public partial int AutoCleanIdleMinutes { get; set; }

    [ObservableProperty]
    public partial bool LowDiskAlertEnabled { get; set; }

    [ObservableProperty]
    public partial int LowDiskThresholdGb { get; set; }

    [ObservableProperty]
    public partial bool CheckUpdatesOnStartup { get; set; }

    [ObservableProperty]
    public partial bool IsCheckingUpdates { get; set; }

    [ObservableProperty]
    public partial string UpdateStatusText { get; set; } = "Update status has not been checked yet.";

    /// <summary>The version this build reports, straight from the assembly.</summary>
    public string CurrentVersionText { get; } =
        $"v{typeof(SettingsWorkspaceViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"}";

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

    public IAsyncRelayCommand CheckForUpdatesCommand { get; }

    public IRelayCommand OpenReleasesPageCommand { get; }

    /// <summary>Raised when the user picks the other theme. The shell applies it.</summary>
    public event Action<bool>? ThemeChangeRequested;

    public event Action<string, LogLevel>? LogRequested;

    /// <summary>
    /// Asks Core whether a newer release exists. Read-only: it queries the release
    /// feed and reports what it finds, and never downloads or installs anything.
    /// </summary>
    private async Task CheckForUpdatesAsync()
    {
        if (IsCheckingUpdates)
        {
            return;
        }

        IsCheckingUpdates = true;
        UpdateStatusText = "Checking for updates...";

        try
        {
            ReleaseInfo? info = await UpdateService.CheckForUpdatesAsync();

            UpdateStatusText = info is null
                ? "Could not reach the release feed. Nothing was changed."
                : info.IsNewer
                    ? $"Version {info.VersionString} is available. This build is {CurrentVersionText}."
                    : $"This build is up to date ({CurrentVersionText}).";

            LogRequested?.Invoke(UpdateStatusText, LogLevel.Info);
        }
        catch (Exception ex)
        {
            UpdateStatusText = $"Update check failed: {ex.Message}";
            LogRequested?.Invoke(UpdateStatusText, LogLevel.Error);
        }
        finally
        {
            IsCheckingUpdates = false;
        }
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

    partial void OnIsDarkThemeChanged(bool value)
    {
        SettingsService.Update(s => s.IsDarkMode = value);
        ThemeChangeRequested?.Invoke(value);
    }

    partial void OnSoundEnabledChanged(bool value)
    {
        SettingsService.Update(s => s.SoundEnabled = value);

        // SoundService holds the switch the click/scrape effects actually read. The
        // WPF app pushes it on save; without this the toggle persisted a preference
        // that no sound ever consulted.
        SoundService.IsSoundEnabled = value;
    }

    partial void OnLanguageChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        SettingsService.Update(s => s.Language = value);
        LocalizationService.CurrentLanguage = value;
    }

    partial void OnAutoCleanOnIdleChanged(bool value)
    {
        SettingsService.Update(s => s.AutoCleanOnIdle = value);

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
        SettingsService.Update(s => s.AutoCleanIdleMinutes = clamped);
        OnPropertyChanged(nameof(AutoCleanIdleMinutesValue));

        // Re-arm with the new threshold. A running watcher would otherwise keep
        // polling the old number until the process restarted.
        AutoCleanService.Start();
    }

    partial void OnLowDiskAlertEnabledChanged(bool value)
        => SettingsService.Update(s => s.LowDiskAlertEnabled = value);

    partial void OnLowDiskThresholdGbChanged(int value)
    {
        int clamped = Math.Clamp(value, 1, 500);
        SettingsService.Update(s => s.LowDiskAlertThresholdGb = clamped);
        OnPropertyChanged(nameof(LowDiskThresholdValue));
    }

    partial void OnCheckUpdatesOnStartupChanged(bool value)
        => SettingsService.Update(s => s.CheckUpdatesOnStartup = value);
}
