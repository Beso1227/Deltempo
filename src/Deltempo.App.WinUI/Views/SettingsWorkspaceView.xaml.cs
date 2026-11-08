using Deltempo.App.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

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

    /// <summary>True while the key box is being filled from the selected provider, so
    /// PasswordChanged does not treat that as an edit.</summary>
    private bool _syncingKeyBox;

    /// <summary>Coalesces the keystrokes of a pasted API key into one model listing.</summary>
    private readonly DispatcherTimer _aiModelRefreshTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(700),
    };

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

        // The view owns the timer because only it has a dispatcher; the view-model asks
        // for a refresh whenever the provider, key or endpoint changes.
        _aiModelRefreshTimer.Tick += OnAiModelRefreshTimerTick;
        ViewModel.AiModelRefreshRequested += ScheduleAiModelRefresh;

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

            // Setting Password fires PasswordChanged. It is already guarded while
            // syncing, but keeping the fill explicit avoids a pointless write-back of
            // the value that was just read from the store.
            AiApiKeyBox.Password = ViewModel.AiApiKey;
        }
        finally
        {
            _isSyncing = false;
        }

        // Not at app startup: this panel is the only surface that shows the model list,
        // so the provider is asked when the panel opens rather than on every launch.
        ScheduleAiModelRefresh();
    }

    private void ScheduleAiModelRefresh()
    {
        _aiModelRefreshTimer.Stop();
        _aiModelRefreshTimer.Start();
    }

    private void OnAiModelRefreshTimerTick(object? sender, object e)
    {
        _aiModelRefreshTimer.Stop();
        _ = ViewModel.RefreshAiModelsAsync(force: false);
    }

    private void OnLog(string message, LogLevel level)
        => LogRequested?.Invoke(message, level);

    // ---------------------------------------------------------------------
    // Settings search
    // ---------------------------------------------------------------------

    /// <summary>
    /// WPF's SettingsSearchBox_TextChanged. WPF showed/hid its four settings
    /// panels by matching a hidden scope line; the WinUI settings is a stack of
    /// titled cards per category, so the same search shows/hides those cards
    /// instead. A card with no matching text hides; an empty query shows all.
    /// Visibility inside the card tree is untouched - the search never changes
    /// what a setting can do, only what is in the way.
    /// </summary>
    private void OnSettingsSearchChanged(object sender, TextChangedEventArgs e)
    {
        string term = SettingsSearchBox.Text.Trim();

        // All four category panels, but only the visible one has cards laid out;
        // the rest are cheap no-ops. The filter is over the card borders, which
        // is the settings' own StorageCard family - the search never changes
        // what a setting can do, only what is in the way.
        FilterCards(GeneralSettingsPanel, term);
        FilterCards(UpdatesSettingsPanel, term);
        FilterCards(MemorySettingsPanel, term);
        FilterCards(SafetySettingsPanel, term);
    }

    /// <summary>Shows/hides each search-scope card under <paramref name="panel"/>.</summary>
    private static void FilterCards(Panel panel, string term)
    {
        if (panel is null)
        {
            return;
        }

        foreach (UIElement child in panel.Children)
        {
            if (child is Border card)
            {
                card.Visibility = term.Length == 0 || CardMatches(card, term)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }
    }

    /// <summary>True when any text anywhere inside the card contains the term.</summary>
    private static bool CardMatches(DependencyObject root, string term)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBlock tb &&
                tb.Text.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (CardMatches(child, term))
            {
                return true;
            }
        }

        return false;
    }

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

        if ((AiProviderCombo.SelectedItem as ComboBoxItem)?.Tag is not string provider)
        {
            return;
        }

        // Stash what the box holds for the provider being left, then show the incoming
        // provider's own key. One shared field used to hand the previous provider's key
        // to the next one, which came back as a 401 that looked like an outage.
        ViewModel.AiApiKey = AiApiKeyBox.Password;
        ViewModel.AiProvider = provider;

        string savedKey = ViewModel.GetAiApiKey(provider);

        _syncingKeyBox = true;
        try
        {
            AiApiKeyBox.Password = savedKey;
        }
        finally
        {
            _syncingKeyBox = false;
        }

        // Test and the request path read the view-model, not the box, so it has to
        // follow the provider too; otherwise the newly selected provider would be
        // tested with the key that was just stashed for the previous one.
        ViewModel.AiApiKey = savedKey;
    }

    /// <summary>
    /// The key field writes through to the store for the selected provider. The saved key
    /// is pre-filled here, so clearing it is an explicit removal.
    /// </summary>
    private void OnAiApiKeyChanged(object sender, RoutedEventArgs e)
    {
        if (!_isSyncing && !_syncingKeyBox)
        {
            ViewModel.AiApiKey = AiApiKeyBox.Password;
        }
    }

    private void OnRemoveAiApiKey(object sender, RoutedEventArgs e)
    {
        _syncingKeyBox = true;
        try
        {
            AiApiKeyBox.Password = string.Empty;
        }
        finally
        {
            _syncingKeyBox = false;
        }

        ViewModel.RemoveAiApiKey();
        ViewModel.AiTestStatusText = $"Saved key for {ViewModel.AiProvider} removed.";
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
