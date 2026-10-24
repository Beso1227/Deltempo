using Deltempo.App.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinTempCleaner.Models;

namespace Deltempo.App.WinUI.Views;

/// <summary>
/// Workspace panel for the shared settings.
///
/// The combo boxes are filled here rather than in XAML because every item carries the
/// value next to its label - a language code, an hour count, a gigabyte threshold - and
/// the value is what the setting stores. WPF's settings modal filled the same lists in
/// code for the same reason.
/// </summary>
public sealed partial class SettingsWorkspaceView : UserControl
{
    /// <summary>True while the controls are being loaded from the settings, so the
    /// SelectionChanged handlers do not write a value back that they were just given.</summary>
    private bool _isSyncing;

    public SettingsWorkspaceView()
    {
        InitializeComponent();

        ExcludedPathsList.ItemsSource = ViewModel.ExcludedPaths;
        ExcludedExtensionsList.ItemsSource = ViewModel.ExcludedExtensions;

        // The panel lives in a grid rather than a frame, so it is never navigated to
        // and the navigation events would never fire: the subscriptions live here.
        ViewModel.LogRequested += OnLog;
        ViewModel.ThemeChangeRequested += OnThemeChangeRequested;
        ViewModel.AlwaysOnTopRequested += OnAlwaysOnTopRequested;

        Refresh();
    }

    public SettingsWorkspaceViewModel ViewModel { get; } = new();

    public event Action<string, LogLevel>? LogRequested;

    public event Action<bool>? ThemeChangeRequested;

    /// <summary>Raised when the always-on-top switch moves. Only the shell has a window.</summary>
    public event Action<bool>? AlwaysOnTopRequested;

    /// <summary>
    /// Raised when the user asks for an update check. The shell runs it because a
    /// newer release opens the window-level offer, which this panel does not own.
    /// </summary>
    public event Action? CheckForUpdatesRequested;

    /// <summary>
    /// Re-reads every setting into the panel. WPF's LoadSettingsIntoUI ran each time its
    /// modal opened; the shell calls this when this workspace is shown for the same
    /// reason - another surface (the tray menu, the WPF app) may have changed a value.
    /// </summary>
    public void Refresh()
    {
        _isSyncing = true;
        try
        {
            ViewModel.LoadFromSettings();

            FillChoices();
            SelectCurrentChoices();

            // Setting Password fires PasswordChanged, which would write the key straight
            // back. Harmless but pointless, and it would defeat the "empty means leave
            // the saved key alone" rule the field relies on.
            AiApiKeyBox.Password = ViewModel.AiApiKey;
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void OnLog(string message, LogLevel level)
        => LogRequested?.Invoke(message, level);

    private void OnThemeChangeRequested(bool isDark)
        => ThemeChangeRequested?.Invoke(isDark);

    private void OnAlwaysOnTopRequested(bool alwaysOnTop)
        => AlwaysOnTopRequested?.Invoke(alwaysOnTop);

    // ---------------------------------------------------------------------
    // Choice lists
    // ---------------------------------------------------------------------

    /// <summary>
    /// Fills every drop-down from the static tables on the ViewModel. Rebuilt on each
    /// load because WPF's lists were filled the same way and rebuilding keeps one source
    /// of truth for the labels.
    /// </summary>
    private void FillChoices()
    {
        LanguageCombo.Items.Clear();
        foreach ((string code, string name) in SettingsWorkspaceViewModel.Languages)
        {
            LanguageCombo.Items.Add(new ComboBoxItem { Content = name, Tag = code });
        }

        AutoCleanIntervalCombo.Items.Clear();
        foreach ((int hours, string label) in SettingsWorkspaceViewModel.AutoCleanIntervals)
        {
            AutoCleanIntervalCombo.Items.Add(new ComboBoxItem { Content = label, Tag = hours });
        }

        IdleMinutesCombo.Items.Clear();
        foreach ((int minutes, string label) in SettingsWorkspaceViewModel.IdleWaits)
        {
            IdleMinutesCombo.Items.Add(new ComboBoxItem { Content = label, Tag = minutes });
        }

        UpdateFrequencyCombo.Items.Clear();
        foreach ((int days, string label) in SettingsWorkspaceViewModel.UpdateFrequencies)
        {
            UpdateFrequencyCombo.Items.Add(new ComboBoxItem { Content = label, Tag = days });
        }

        UpdateChannelCombo.Items.Clear();
        foreach ((string tag, string label) in SettingsWorkspaceViewModel.UpdateChannels)
        {
            UpdateChannelCombo.Items.Add(new ComboBoxItem { Content = label, Tag = tag });
        }

        MemoryIntervalCombo.Items.Clear();
        foreach ((int hours, string label) in SettingsWorkspaceViewModel.MemoryIntervals)
        {
            MemoryIntervalCombo.Items.Add(new ComboBoxItem { Content = label, Tag = hours });
        }

        MemoryThresholdCombo.Items.Clear();
        foreach ((int percent, string label) in SettingsWorkspaceViewModel.MemoryThresholds)
        {
            MemoryThresholdCombo.Items.Add(new ComboBoxItem { Content = label, Tag = percent });
        }

        DiskThresholdCombo.Items.Clear();
        foreach ((int gigabytes, string label) in SettingsWorkspaceViewModel.DiskThresholds)
        {
            DiskThresholdCombo.Items.Add(new ComboBoxItem { Content = label, Tag = gigabytes });
        }

        AiProviderCombo.Items.Clear();
        foreach ((string tag, string label) in SettingsWorkspaceViewModel.AiProviders)
        {
            AiProviderCombo.Items.Add(new ComboBoxItem { Content = label, Tag = tag });
        }
    }

    /// <summary>
    /// Points every drop-down at the stored value. A value that is not in its table
    /// (a settings file from a newer build, or a hand-edited number) is left unselected
    /// rather than silently rewritten - the setting keeps what the user has.
    /// </summary>
    private void SelectCurrentChoices()
    {
        SelectChoice(LanguageCombo, ViewModel.Language);
        SelectChoice(AutoCleanIntervalCombo, ViewModel.AutoCleanIntervalHours);
        SelectChoice(IdleMinutesCombo, ViewModel.AutoCleanIdleMinutes);
        SelectChoice(UpdateFrequencyCombo, ViewModel.UpdateCheckFrequencyDays);
        SelectChoice(UpdateChannelCombo, ViewModel.UpdateChannel);
        SelectChoice(MemoryIntervalCombo, ViewModel.MemoryAutoOptimizeIntervalHours);
        SelectChoice(MemoryThresholdCombo, ViewModel.MemoryAutoOptimizeFreeRamThresholdPercent);
        SelectChoice(DiskThresholdCombo, ViewModel.LowDiskThresholdGb);
        SelectChoice(AiProviderCombo, ViewModel.AiProvider);

        ExcludedPathsList.SelectedItem = null;
        ExcludedExtensionsList.SelectedItem = null;
    }

    private static void SelectChoice(ComboBox combo, object value)
    {
        combo.SelectedItem = null;

        foreach (ComboBoxItem item in combo.Items)
        {
            if (Equals(item.Tag, value))
            {
                combo.SelectedItem = item;
                return;
            }
        }
    }

    // ---------------------------------------------------------------------
    // Selectors
    // ---------------------------------------------------------------------

    private void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncing)
        {
            return;
        }

        if ((LanguageCombo.SelectedItem as ComboBoxItem)?.Tag is string code)
        {
            ViewModel.Language = code;
        }
    }

    private void OnAutoCleanIntervalChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isSyncing && (AutoCleanIntervalCombo.SelectedItem as ComboBoxItem)?.Tag is int hours)
        {
            ViewModel.AutoCleanIntervalHours = hours;
        }
    }

    private void OnIdleMinutesChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isSyncing && (IdleMinutesCombo.SelectedItem as ComboBoxItem)?.Tag is int minutes)
        {
            ViewModel.AutoCleanIdleMinutes = minutes;
        }
    }

    private void OnUpdateFrequencyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isSyncing && (UpdateFrequencyCombo.SelectedItem as ComboBoxItem)?.Tag is int days)
        {
            ViewModel.UpdateCheckFrequencyDays = days;
        }
    }

    private void OnUpdateChannelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncing)
        {
            return;
        }

        if ((UpdateChannelCombo.SelectedItem as ComboBoxItem)?.Tag is string channel)
        {
            ViewModel.UpdateChannel = channel;
        }
    }

    private void OnMemoryIntervalChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isSyncing && (MemoryIntervalCombo.SelectedItem as ComboBoxItem)?.Tag is int hours)
        {
            ViewModel.MemoryAutoOptimizeIntervalHours = hours;
        }
    }

    private void OnMemoryThresholdChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isSyncing && (MemoryThresholdCombo.SelectedItem as ComboBoxItem)?.Tag is int percent)
        {
            ViewModel.MemoryAutoOptimizeFreeRamThresholdPercent = percent;
        }
    }

    private void OnDiskThresholdChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isSyncing && (DiskThresholdCombo.SelectedItem as ComboBoxItem)?.Tag is int gigabytes)
        {
            ViewModel.LowDiskThresholdGb = gigabytes;
        }
    }

    private void OnAiProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncing)
        {
            return;
        }

        if ((AiProviderCombo.SelectedItem as ComboBoxItem)?.Tag is string provider)
        {
            ViewModel.AiProvider = provider;
        }
    }

    /// <summary>
    /// The key field is write-only to the settings: an empty box means "leave the saved
    /// key alone", which is how WPF's PasswordBox behaved. The stored key is pre-filled
    /// here so an edit is explicit.
    /// </summary>
    private void OnAiApiKeyChanged(object sender, RoutedEventArgs e)
    {
        if (!_isSyncing)
        {
            ViewModel.AiApiKey = AiApiKeyBox.Password;
        }
    }

    // ---------------------------------------------------------------------
    // Exclusions
    // ---------------------------------------------------------------------

    private void OnExcludedPathSelected(object sender, SelectionChangedEventArgs e)
        => ViewModel.SelectedExcludedPath = ExcludedPathsList.SelectedItem as string;

    private void OnExcludedExtensionSelected(object sender, SelectionChangedEventArgs e)
        => ViewModel.SelectedExcludedExtension = ExcludedExtensionsList.SelectedItem as string;

    private void OnBrowseExcludedFolder(object sender, RoutedEventArgs e)
        => _ = PickExcludedFolderAsync();

    /// <summary>
    /// WinUI 3 has no OpenFolderDialog; the WinRT picker has to be handed the window
    /// handle explicitly or it throws. Confined here so the ViewModel stays picker-free.
    /// </summary>
    private async Task PickExcludedFolderAsync()
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
        };

        picker.FileTypeFilter.Add("*");

        try
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(picker, hwnd);

            Windows.Storage.StorageFolder? folder = await picker.PickSingleFolderAsync();
            if (folder is not null)
            {
                ViewModel.SetBrowsedExcludedFolder(folder.Path);
            }
        }
        catch (Exception ex)
        {
            // Cancelling surfaces as an exception too; only a real failure is worth a
            // log line.
            if (ex is not System.Runtime.InteropServices.COMException
                and not OperationCanceledException)
            {
                ViewModel.Log($"Folder picker error: {ex.Message}", LogLevel.Warning);
            }
        }
    }

    // ---------------------------------------------------------------------
    // Actions
    // ---------------------------------------------------------------------

    private void OnCheckForUpdatesClick(object sender, RoutedEventArgs e)
        => CheckForUpdatesRequested?.Invoke();

    private void OnOpenReleasesClick(object sender, RoutedEventArgs e)
        => ViewModel.OpenReleasesPageCommand.Execute(null);

    private void OnResetDefaultsClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ResetToDefaultsCommand.Execute(null);

        // Restoring defaults writes the settings; the controls have to be told.
        Refresh();
    }
}
