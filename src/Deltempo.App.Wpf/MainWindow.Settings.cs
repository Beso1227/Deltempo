using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace WinTempCleaner;

// Settings hub: categorized navigation, update choices, AI provider configuration.
public partial class MainWindow
{
    /// <summary>
    /// Plaintext keys for every provider while the settings modal is open. A single
    /// password box cannot represent more than one provider: switching from Gemini to Groq
    /// used to leave the Gemini key in the box and send it to Groq, which answered 401.
    /// Keys are stashed here on switch and written back on save.
    /// </summary>
    private Dictionary<string, string> _pendingAiApiKeys = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The provider the password box currently represents, so a switch can stash
    /// the outgoing key before the incoming one is loaded.</summary>
    private string _lastAiProvider = "BuiltIn";

    /// <summary>True while the provider list or the selected item is being set from code,
    /// so the SelectionChanged handler does not treat that as a user switch.</summary>
    private bool _suppressAiProviderChange;

    /// <summary>True while the model suggestion list is being repopulated.</summary>
    private bool _suppressAiModelSuggestion;

    /// <summary>The provider combo is filled once, from the shared catalog.</summary>
    private bool _aiProvidersPopulated;

    /// <summary>
    /// Models the current key can actually call, as the provider reported them. Null means
    /// "not asked yet", which is why the card falls back to the static catalog rather than
    /// showing an empty list while the first request is in flight.
    /// </summary>
    private IReadOnlyList<AiModelOption>? _aiFetchedModels;

    /// <summary>
    /// The provider+key+endpoint the fetched list belongs to. Typing a different key makes
    /// the old list a lie about a different account, so it is discarded and refetched.
    /// </summary>
    private string? _aiFetchedFor;

    /// <summary>Coalesces the keystrokes of a pasted API key into one request.</summary>
    private DispatcherTimer? _aiModelRefreshTimer;

    /// <summary>
    /// Re-asks the provider while the settings modal stays open. Models are added and
    /// retired without notice, and a list fetched once when the modal opened is stale by
    /// the time a long settings session ends; two minutes is quiet enough to go unnoticed
    /// and frequent enough that the picker follows the provider.
    /// </summary>
    private DispatcherTimer? _aiModelAutoRefreshTimer;

    /// <summary>Cancels the in-flight listing when the user types again or switches provider.</summary>
    private CancellationTokenSource? _aiModelRefreshCts;

    /// <summary>Guards against an older listing overwriting a newer one.</summary>
    private int _aiModelFetchGeneration;

    /// <summary>True while the model list is being rebuilt, so the picker does not fire
    /// SelectionChanged for every item it adds.</summary>
    private bool _isRefreshingAiModels;

    /// <summary>
    /// The model id the card will save. Kept apart from the combo's SelectedItem because
    /// the list can drop rows: a refresh must not silently clear the saved choice,
    /// and an id the provider no longer lists still has to survive a Save.
    /// </summary>
    private string _aiSelectedModelId = string.Empty;

    /// <summary>The full rows behind the picker, in provider order.</summary>
    private IReadOnlyList<AiModelOption> _aiModelPickerSource = Array.Empty<AiModelOption>();

    /// <summary>True when the picker rows came from the provider rather than the catalog.</summary>
    private bool _aiModelPickerFromProvider;

    /// <summary>Mirrors the master switch, so a rebuild can re-enable the picker
    /// without recomputing the card's visibility rules.</summary>
    private bool _aiModelPickerEnabled;

    private void SettingsTab_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag)
        {
            if (SettingsUpdatesPanel != null) SettingsUpdatesPanel.Visibility = tag == "UPDATES" ? Visibility.Visible : Visibility.Collapsed;
            if (SettingsGeneralPanel != null) SettingsGeneralPanel.Visibility = tag == "GENERAL" ? Visibility.Visible : Visibility.Collapsed;
            if (SettingsMemoryPanel != null) SettingsMemoryPanel.Visibility = tag == "MEMORY" ? Visibility.Visible : Visibility.Collapsed;
            if (SettingsSafetyPanel != null) SettingsSafetyPanel.Visibility = tag == "SAFETY" ? Visibility.Visible : Visibility.Collapsed;
            SoundService.PlayClickSound();
        }
    }

    private void SettingsSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string term = SettingsSearchBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(term))
        {
            SettingsUpdatesPanel.Visibility = Visibility.Visible;
            SettingsGeneralPanel.Visibility = Visibility.Visible;
            SettingsMemoryPanel.Visibility = Visibility.Visible;
            SettingsSafetyPanel.Visibility = Visibility.Visible;
            return;
        }

        bool updatesMatch = IsPanelMatch(SettingsUpdatesPanel, term);
        bool generalMatch = IsPanelMatch(SettingsGeneralPanel, term);
        bool memoryMatch = IsPanelMatch(SettingsMemoryPanel, term);
        bool safetyMatch = IsPanelMatch(SettingsSafetyPanel, term);

        if (SettingsUpdatesPanel != null) SettingsUpdatesPanel.Visibility = updatesMatch ? Visibility.Visible : Visibility.Collapsed;
        if (SettingsGeneralPanel != null) SettingsGeneralPanel.Visibility = generalMatch ? Visibility.Visible : Visibility.Collapsed;
        if (SettingsMemoryPanel != null) SettingsMemoryPanel.Visibility = memoryMatch ? Visibility.Visible : Visibility.Collapsed;
        if (SettingsSafetyPanel != null) SettingsSafetyPanel.Visibility = safetyMatch ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool IsPanelMatch(Panel panel, string term)
    {
        if (panel == null) return false;
        return panel.FindName("SettingsSearchScope") is TextBlock tb && tb.Text.Contains(term, StringComparison.OrdinalIgnoreCase)
            || panel.FindName("SettingsAiApiKeyRow") is FrameworkElement fe && fe.Visibility != Visibility.Collapsed;
    }

    private void ResetSettings_Click(object sender, RoutedEventArgs e)
    {
        SettingsService.Update(s =>
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
            s.AutoQueryAiForLargeFiles = defaults.AutoQueryAiForLargeFiles;
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

        LoadSettingsIntoUI();
        AddLog("Settings restored to factory defaults.", LogLevel.Info);
        SoundService.PlayClickSound();
    }

    private void SettingsViewReleaseNotes_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/Beso1227/Deltempo/releases",
                UseShellExecute = true
            });
            SoundService.PlayClickSound();
        }
        catch (Exception ex)
        {
            AddLog($"Unable to open release notes: {ex.Message}", LogLevel.Warning);
        }
    }

    private async Task CleanSafeFromTrayAsync()
    {
        if (_isBusy) return;
        SelectSafeOnlyButton_Click(this, new RoutedEventArgs());
        await ExecuteCleanupAsync();
    }

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        OpenSettingsModal();
    }

    private void OpenSettingsModal()
    {
        LoadSettingsIntoUI();
        SettingsModalOverlay.Visibility = Visibility.Visible;
        SoundService.PlayClickSound();

        // Not at startup: the modal is the only surface that shows the model list, so the
        // provider is asked when it opens rather than on every app launch.
        ScheduleAiModelRefresh();
        StartAiModelAutoRefresh();
    }

    private void CloseSettings_Click(object sender, RoutedEventArgs e)
    {
        StopAiModelAutoRefresh();
        SettingsModalOverlay.Visibility = Visibility.Collapsed;
        SoundService.PlayClickSound();
    }

    /// <summary>
    /// Starts the slow re-ask that keeps the model list live while the modal is open.
    /// It is a separate timer from the 700 ms typing debounce: that one coalesces edits,
    /// this one notices the provider changing its catalog during a long session.
    /// </summary>
    private void StartAiModelAutoRefresh()
    {
        if (_aiModelAutoRefreshTimer is null)
        {
            _aiModelAutoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(2) };
            _aiModelAutoRefreshTimer.Tick += (_, _) =>
                _ = RefreshAiModelsAsync(force: true, quiet: true);
        }

        _aiModelAutoRefreshTimer.Stop();
        _aiModelAutoRefreshTimer.Start();
    }

    private void StopAiModelAutoRefresh() => _aiModelAutoRefreshTimer?.Stop();

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        // Validate the AI card before writing anything. Saving a provider with no key used
        // to succeed silently and only fail later, inside the large-file batch, as a line
        // that looked like a network outage.
        string aiProvider = SelectedAiProviderId();
        string aiApiKey = SettingsAiApiKeyPasswordBox?.Password?.Trim() ?? string.Empty;
        string aiOllamaEndpoint = SettingsAiOllamaEndpointBox?.Text?.Trim() ?? string.Empty;
        string aiModel = SelectedAiModelId();
        bool aiEnabled = SettingsEnableAiCheckBox?.IsChecked == true;
        bool autoQueryAi = SettingsAutoQueryAiCheckBox?.IsChecked == true;

        var aiIssues = AiSettingsValidator.Validate(
            aiProvider,
            aiApiKey,
            aiOllamaEndpoint,
            aiModel,
            aiEnabled,
            autoQueryAi);

        if (!AiSettingsValidator.IsValid(aiIssues))
        {
            string message = AiSettingsValidator.Describe(aiIssues);
            SettingsAiTestStatusText.Text = "⚠ " + message;
            MessageBox.Show(message, "AI settings need attention", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SettingsService.Update(s =>
        {
            s.EnableAutoPilot = SettingsAutoPilotCheckBox.IsChecked == true;
            s.MinimizeToTray = SettingsTrayCheckBox.IsChecked == true;
            s.AutoCleanNotify = SettingsNotifyCheckBox.IsChecked == true;
            s.SoundEnabled = SettingsSoundCheckBox.IsChecked == true;
            SoundService.IsSoundEnabled = s.SoundEnabled;

            s.SendToRecycleBin = SettingsRecycleBinCheckBox.IsChecked == true;
            s.LowDiskAlertEnabled = SettingsLowDiskAlertCheckBox.IsChecked == true;
            s.EnableShellContextMenu = SettingsShellContextMenuCheckBox.IsChecked == true;
            s.EnableWeeklyScheduledTask = SettingsWeeklyTaskCheckBox.IsChecked == true;

            if (SettingsDiskThresholdComboBox.SelectedItem is ComboBoxItem dItem && dItem.Tag is string dTag && int.TryParse(dTag, out int dGb))
            {
                s.LowDiskAlertThresholdGb = dGb;
            }

            s.CheckUpdatesOnStartup = SettingsCheckUpdatesCheckBox.IsChecked == true;
            s.AutoDownloadUpdates = SettingsAutoDownloadCheckBox.IsChecked == true;

            if (SettingsUpdateChannelComboBox.SelectedItem is ComboBoxItem cItem && cItem.Tag is string cTag)
            {
                s.UpdateChannel = cTag;
            }

            if (SettingsUpdateFrequencyComboBox.SelectedItem is ComboBoxItem fItem && fItem.Tag is string fTag && int.TryParse(fTag, out int fDays))
            {
                s.UpdateCheckFrequencyDays = fDays;
            }

            if (SettingsIntervalComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag && int.TryParse(tag, out int hours))
            {
                s.AutoCleanIntervalHours = hours;
            }

            s.MemoryAutoOptimizeEnabled = MemoryAutoOptCheckBox.IsChecked == true;
            s.MemoryShowInTray = MemoryShowInTrayCheckBox.IsChecked == true;
            s.MemoryAlwaysOnTop = MemoryAlwaysOnTopCheckBox.IsChecked == true;
            s.MemoryCompactMode = MemoryCompactModeCheckBox.IsChecked == true;
            s.MemoryCloseToTray = MemoryCloseToTrayCheckBox.IsChecked == true;
            s.MemoryShowNotifications = MemoryShowNotifyCheckBox.IsChecked == true;

            if (MemoryAutoOptIntervalComboBox.SelectedItem is ComboBoxItem mi && mi.Tag is string mt && int.TryParse(mt, out int mph))
            {
                s.MemoryAutoOptimizeIntervalHours = mph;
            }

            if (MemoryThresholdComboBox.SelectedItem is ComboBoxItem mt2 && mt2.Tag is string ttt && int.TryParse(ttt, out int tval))
            {
                s.MemoryAutoOptimizeFreeRamThresholdPercent = tval;
            }

            s.EnableOnlineAiSafety = aiEnabled;
            s.AutoQueryAiForLargeFiles = aiEnabled && autoQueryAi;
            s.AiProvider = aiProvider;
            s.AiModelName = aiModel;
            s.AiOllamaEndpoint = aiOllamaEndpoint;

            // Rebuild the per-provider map from the modal's working copy so a key removed
            // for a provider the user visited is actually removed, then let the box for the
            // selected provider win over whatever was stashed for it.
            s.AiApiKeys.Clear();
            foreach (var pair in _pendingAiApiKeys)
            {
                s.SetAiApiKey(pair.Key, pair.Value);
            }
            s.SetAiApiKey(aiProvider, aiApiKey);

            if (SettingsIdleCleanCheckBox != null)
            {
                s.AutoCleanOnIdle = SettingsIdleCleanCheckBox.IsChecked == true;
            }
            if (SettingsIdleMinutesComboBox?.SelectedItem is ComboBoxItem idItem && idItem.Tag is string idTag && int.TryParse(idTag, out int idMins))
            {
                s.AutoCleanIdleMinutes = idMins;
            }

            if (SettingsAggressivePruneCheckBox != null)
            {
                s.AggressiveEmptyFolderPrune = SettingsAggressivePruneCheckBox.IsChecked == true;
            }

            if (SettingsExcludedPathsListBox?.ItemsSource is IEnumerable<string> paths)
            {
                s.CustomPathExclusions = paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }
            if (SettingsExcludedExtensionsListBox?.ItemsSource is IEnumerable<string> exts)
            {
                s.CustomExtensionExclusions = exts.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }
        });

        AutoCleanService.Start();
        ApplyMemorySettingsToWindow();

        if (SettingsShellContextMenuCheckBox != null)
        {
            if (SettingsShellContextMenuCheckBox.IsChecked == true)
            {
                ShellExtensionService.Register();
            }
            else
            {
                ShellExtensionService.Unregister();
            }
        }

        if (SettingsWeeklyTaskCheckBox != null)
        {
            if (SettingsWeeklyTaskCheckBox.IsChecked == true)
            {
                TaskSchedulerService.EnableWeeklyTask();
            }
            else
            {
                TaskSchedulerService.DisableWeeklyTask();
            }
        }

        StopAiModelAutoRefresh();
        SettingsModalOverlay.Visibility = Visibility.Collapsed;
        SoundService.PlayClickSound();
        AddLog("Preferences & Auto-Pilot Guardian settings saved.", LogLevel.Success);
    }

    private void SettingsAiProviderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressAiProviderChange) return;

        // Stash the key the user was looking at for the provider being left, then load
        // the incoming provider's own key. Without this, the box kept one provider's
        // secret and handed it to the next provider on save.
        if (!string.IsNullOrEmpty(_lastAiProvider) && SettingsAiApiKeyPasswordBox != null)
        {
            string outgoing = SettingsAiApiKeyPasswordBox.Password;
            if (string.IsNullOrWhiteSpace(outgoing))
            {
                _pendingAiApiKeys.Remove(_lastAiProvider);
            }
            else
            {
                _pendingAiApiKeys[_lastAiProvider] = outgoing.Trim();
            }
        }

        _lastAiProvider = SelectedAiProviderId();
        if (SettingsAiApiKeyPasswordBox != null)
        {
            SettingsAiApiKeyPasswordBox.Password = _pendingAiApiKeys.TryGetValue(_lastAiProvider, out string? stored)
                ? stored
                : string.Empty;
        }

        // A new provider is a different account and a different model namespace: the list
        // fetched for the previous one would offer ids that cannot be called here, and the
        // model id chosen for the previous provider is not a valid id here either.
        ResetAiFetchedModels();
        _aiSelectedModelId = string.Empty;

        // Choosing a provider IS the opt-in to using it. Leaving the master switch off made
        // the choice inert: Large Files then answered from the local knowledge base and the
        // row said "Local Intelligence (Offline)", which reads as if the provider was
        // ignored. Turn the switch on and let the description state the state.
        if (_lastAiProvider != "BuiltIn" && SettingsEnableAiCheckBox?.IsChecked != true)
        {
            SettingsEnableAiCheckBox!.IsChecked = true;
            AddLog($"Online file intelligence enabled for {_lastAiProvider}.", LogLevel.Info);
        }

        UpdateAiSettingsUiVisibility();
        ScheduleAiModelRefresh();
    }

    private void SettingsEnableAiCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        // Auto-query cannot run while the master switch is off. Unchecking it now keeps
        // the card from showing a promise that Save would silently drop.
        if (SettingsEnableAiCheckBox?.IsChecked != true &&
            SettingsAutoQueryAiCheckBox?.IsChecked == true)
        {
            SettingsAutoQueryAiCheckBox.IsChecked = false;
        }

        UpdateAiSettingsUiVisibility();
    }

    private void SettingsAiModelSuggestionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressAiModelSuggestion || _isRefreshingAiModels) return;

        if (SettingsAiModelSuggestionComboBox.SelectedItem is AiModelOption option)
        {
            // The picker is the only place the model id comes from now. Remember it apart
            // from the selection so a rebuild that drops the row cannot clear the choice.
            _aiSelectedModelId = option.Id;
            UpdateAiModelHint();
        }
    }

    /// <summary>
    /// Rebuilds the picker from the full row set. The saved model stays selected whenever
    /// its row is present; when the provider no longer lists it, the selection is cleared
    /// but <see cref="_aiSelectedModelId"/> keeps the value Save writes.
    /// </summary>
    private void RebuildAiModelPicker()
    {
        if (SettingsAiModelSuggestionComboBox == null) return;

        IReadOnlyList<AiModelOption> visible = _aiModelPickerSource;
        bool hasRows = _aiModelPickerSource.Count > 0;

        _suppressAiModelSuggestion = true;
        _isRefreshingAiModels = true;
        try
        {
            SettingsAiModelSuggestionComboBox.ItemsSource = visible;
            SettingsAiModelSuggestionComboBox.SelectedItem = visible.FirstOrDefault(
                option => string.Equals(option.Id, _aiSelectedModelId, StringComparison.OrdinalIgnoreCase));

            SettingsAiModelSuggestionComboBox.Visibility = hasRows ? Visibility.Visible : Visibility.Collapsed;
            SettingsAiModelSuggestionComboBox.IsEnabled = _aiModelPickerEnabled && visible.Count > 0;
            SettingsAiModelSuggestionComboBox.ToolTip = _aiModelPickerFromProvider
                ? "Models this API key can use, as reported by the provider"
                : "Known models for this provider. Add a key to load the live list.";
        }
        finally
        {
            _isRefreshingAiModels = false;
            _suppressAiModelSuggestion = false;
        }
    }

    /// <summary>The model id the card will save, or empty when the provider default applies.</summary>
    private string SelectedAiModelId() => _aiSelectedModelId.Trim();

    /// <summary>
    /// Says which model a request will actually use. The old hint only named the provider
    /// default, which stopped being the whole truth once the picker became the only way to
    /// choose: a user could not see the saved id anywhere in the card.
    /// </summary>
    private void UpdateAiModelHint()
    {
        if (SettingsAiModelHint == null) return;

        string provider = SelectedAiProviderId();
        if (provider == "BuiltIn")
        {
            SettingsAiModelHint.Text = "The built-in engine answers without a model call.";
            return;
        }

        string fallback = AiModelCatalog.ResolveModelId(provider, null);
        SettingsAiModelHint.Text = string.IsNullOrWhiteSpace(_aiSelectedModelId)
            ? "Pick a model. With none chosen the provider default is used: " +
              $"{AiModelCatalog.DescribeChosenModel(provider, fallback)}."
            : $"Requests use: {AiModelCatalog.DescribeChosenModel(provider, _aiSelectedModelId)}";
    }

    /// <summary>
    /// The key changed. Stash it for the provider it belongs to and, once the user stops
    /// typing, ask that provider which models the key can use.
    /// </summary>
    private void SettingsAiApiKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        // LoadAiSettingsIntoUi and the provider switch set the box from code; the value is
        // already in the working copy and the switch schedules its own refresh.
        if (_suppressAiProviderChange) return;

        string provider = SelectedAiProviderId();
        string key = SettingsAiApiKeyPasswordBox?.Password?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(key))
        {
            _pendingAiApiKeys.Remove(provider);
        }
        else
        {
            _pendingAiApiKeys[provider] = key;
        }

        ScheduleAiModelRefresh();
    }

    /// <summary>A local Ollama host change is a different model list.</summary>
    private void SettingsAiOllamaEndpointBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressAiProviderChange) return;
        ScheduleAiModelRefresh();
    }

    private void SettingsAiModelRefresh_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        _ = RefreshAiModelsAsync(force: true);
    }

    /// <summary>
    /// Debounces discovery. Typing a key character by character used to be one request per
    /// keystroke if it fired on every change, and a pasted key fires once.
    /// </summary>
    private void ScheduleAiModelRefresh()
    {
        if (SelectedAiProviderId() == "BuiltIn") return;

        _aiModelRefreshTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _aiModelRefreshTimer.Tick -= AiModelRefreshTimer_Tick;
        _aiModelRefreshTimer.Tick += AiModelRefreshTimer_Tick;
        _aiModelRefreshTimer.Stop();
        _aiModelRefreshTimer.Start();
    }

    private void AiModelRefreshTimer_Tick(object? sender, EventArgs e)
    {
        _aiModelRefreshTimer?.Stop();
        _ = RefreshAiModelsAsync(force: false);
    }

    /// <summary>
    /// Asks the provider for the models the current key can use and fills the picker with
    /// them. Failure is reported but never fatal: the catalog list and the manual box stay
    /// usable, which is what a provider outage or an unverified key looks like.
    /// </summary>
    /// <param name="force">Ask even when the key/endpoint identity has not changed.</param>
    /// <param name="quiet">
    /// A background re-ask. It does not show "Loading...", does not clear the list on
    /// failure, and only rebuilds the picker when the provider actually answered with a
    /// different set - rebuilding closes an open dropdown for no reason.
    /// </param>
    private async Task RefreshAiModelsAsync(bool force, bool quiet = false)
    {
        string provider = SelectedAiProviderId();
        string key = SettingsAiApiKeyPasswordBox?.Password?.Trim() ?? string.Empty;
        string endpoint = SettingsAiOllamaEndpointBox?.Text?.Trim() ?? string.Empty;

        if (provider == "BuiltIn")
        {
            if (quiet) return;

            ResetAiFetchedModels();
            UpdateAiSettingsUiVisibility();
            return;
        }

        if (AiModelCatalog.TryGetProvider(provider, out var descriptor) &&
            descriptor.RequiresApiKey && string.IsNullOrWhiteSpace(key))
        {
            if (quiet) return;

            // Nothing to ask with. Show the catalog and say so instead of firing a request
            // that can only come back 401.
            ResetAiFetchedModels();
            SetAiModelStatus(provider, "Add an API key to load the models this account can use.");
            UpdateAiSettingsUiVisibility();
            return;
        }

        string identity = $"{provider}|{key}|{endpoint}";
        if (!force && _aiFetchedModels is not null && string.Equals(_aiFetchedFor, identity, StringComparison.Ordinal))
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
            SetAiModelStatus(provider, $"Loading models from {provider}...");
        }

        try
        {
            IReadOnlyList<AiModelOption> models =
                await AiProviderClient.ListModelsAsync(provider, key, endpoint, ct);

            if (generation != _aiModelFetchGeneration) return;

            bool changed = !AiModelCatalog.IsSameModelSet(_aiFetchedModels, models);
            _aiFetchedModels = models;
            _aiFetchedFor = identity;

            // A quiet answer that matches what is already shown is left alone: assigning a
            // new ItemsSource would close the dropdown the user may be reading.
            if (!quiet || changed)
            {
                UpdateAiSettingsUiVisibility();
                SetAiModelStatus(provider, AiModelCatalog.DescribeFetchedModels(
                    provider, models, _aiSelectedModelId));
            }
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer request or the modal closed. Nothing to report.
        }
        catch (AiProviderException ex)
        {
            if (generation != _aiModelFetchGeneration) return;

            if (quiet)
            {
                SetAiModelStatus(provider, "Could not refresh models: " + ex.UserMessage +
                    " Showing the last list.");
                return;
            }

            ResetAiFetchedModels();
            UpdateAiSettingsUiVisibility();
            SetAiModelStatus(provider, "Could not load models: " + ex.UserMessage);
        }
        catch (Exception ex)
        {
            if (generation != _aiModelFetchGeneration) return;

            if (quiet)
            {
                SetAiModelStatus(provider, "Could not refresh models: " + ex.Message +
                    " Showing the last list.");
                return;
            }

            ResetAiFetchedModels();
            UpdateAiSettingsUiVisibility();
            SetAiModelStatus(provider, "Could not load models: " + ex.Message);
        }
    }

    private void ResetAiFetchedModels()
    {
        _aiModelFetchGeneration++;
        _aiModelRefreshCts?.Cancel();
        _aiFetchedModels = null;
        _aiFetchedFor = null;
    }

    private void SetAiModelStatus(string provider, string message)
    {
        if (SettingsAiModelStatusText == null) return;
        SettingsAiModelStatusText.Text = message;
        SettingsAiModelStatusText.ToolTip = message;
    }

    private void SettingsAiClearKey_Click(object sender, RoutedEventArgs e)
    {
        string provider = SelectedAiProviderId();
        _pendingAiApiKeys.Remove(provider);
        SettingsAiApiKeyPasswordBox?.Clear();
        SettingsAiTestStatusText.Text = $"Saved key for {provider} removed. Save to apply.";

        // Without a key there is no live list; fall back to the catalog and say why.
        ResetAiFetchedModels();
        UpdateAiSettingsUiVisibility();
        SetAiModelStatus(provider, "Add an API key to load the models this account can use.");
        SoundService.PlayClickSound();
    }

    /// <summary>The provider id behind the combo selection.</summary>
    private string SelectedAiProviderId() =>
        (SettingsAiProviderComboBox?.SelectedItem as ComboBoxItem)?.Tag as string ?? "BuiltIn";

    /// <summary>
    /// Fills the provider list once from <see cref="AiModelCatalog.GetProviderOptions"/>.
    /// The labels used to be six static ComboBoxItems in XAML, which is how they drifted
    /// from WinUI's copy and still advertised a model OpenAI had retired.
    /// </summary>
    private void EnsureAiProvidersPopulated()
    {
        if (_aiProvidersPopulated || SettingsAiProviderComboBox == null) return;

        _suppressAiProviderChange = true;
        try
        {
            SettingsAiProviderComboBox.Items.Clear();
            foreach (AiProviderOption option in AiModelCatalog.GetProviderOptions())
            {
                SettingsAiProviderComboBox.Items.Add(new ComboBoxItem
                {
                    Content = option.Label,
                    Tag = option.Id,
                    ToolTip = option.Description,
                });
            }

            _aiProvidersPopulated = true;
        }
        finally
        {
            _suppressAiProviderChange = false;
        }
    }

    private void SelectAiProvider(string providerId)
    {
        if (SettingsAiProviderComboBox == null) return;

        foreach (ComboBoxItem item in SettingsAiProviderComboBox.Items)
        {
            if (item.Tag is string tag && string.Equals(tag, providerId, StringComparison.OrdinalIgnoreCase))
            {
                SettingsAiProviderComboBox.SelectedItem = item;
                return;
            }
        }

        if (SettingsAiProviderComboBox.Items.Count > 0)
        {
            SettingsAiProviderComboBox.SelectedIndex = 0;
        }
    }

    /// <summary>
    /// Loads the AI card from the saved settings. Replaces the inline block in
    /// LoadSettingsIntoUI so the modal, the reset path and the startup path all read the
    /// same state.
    /// </summary>
    private void LoadAiSettingsIntoUi()
    {
        var settings = SettingsService.Current;

        EnsureAiProvidersPopulated();

        // The modal is reopened on a possibly different account: drop any list fetched
        // earlier in this session and let the open path ask again.
        ResetAiFetchedModels();

        _pendingAiApiKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in settings.AiApiKeys)
        {
            _pendingAiApiKeys[pair.Key] = pair.Value;
        }

        _suppressAiProviderChange = true;
        try
        {
            if (SettingsEnableAiCheckBox != null)
            {
                SettingsEnableAiCheckBox.IsChecked = settings.EnableOnlineAiSafety;
            }

            if (SettingsAutoQueryAiCheckBox != null)
            {
                // Never show a checked auto-query while the master switch is off: the scan
                // path gates the batch on both, so the toggle would promise work that cannot
                // run. Old settings files can still carry the inconsistent pair.
                SettingsAutoQueryAiCheckBox.IsChecked =
                    settings.AutoQueryAiForLargeFiles && settings.EnableOnlineAiSafety;
            }

            SelectAiProvider(string.IsNullOrWhiteSpace(settings.AiProvider) ? "BuiltIn" : settings.AiProvider);
            _lastAiProvider = SelectedAiProviderId();

            if (SettingsAiApiKeyPasswordBox != null)
            {
                SettingsAiApiKeyPasswordBox.Password = settings.GetAiApiKey(_lastAiProvider);
            }

            _aiSelectedModelId = settings.AiModelName?.Trim() ?? string.Empty;

            if (SettingsAiOllamaEndpointBox != null)
            {
                SettingsAiOllamaEndpointBox.Text = string.IsNullOrWhiteSpace(settings.AiOllamaEndpoint)
                    ? "http://localhost:11434" // DevSkim: ignore DS162092
                    : settings.AiOllamaEndpoint;
            }
        }
        finally
        {
            _suppressAiProviderChange = false;
        }

        UpdateAiSettingsUiVisibility();

        if (SettingsAiTestStatusText != null)
        {
            SettingsAiTestStatusText.Text =
                $"Ready. Cached local AI reports: {OnlineFileIntelligenceService.GetCacheCount()} files.";
        }
    }

    /// <summary>
    /// Shows the rows the selected provider actually uses, disables the whole card while
    /// the master switch is off, and keeps the model hint and suggestion list in step.
    /// </summary>
    private void UpdateAiSettingsUiVisibility()
    {
        if (SettingsAiProviderComboBox == null || SettingsAiApiKeyRow == null ||
            SettingsAiOllamaRow == null || SettingsAiModelRow == null)
        {
            return;
        }

        string provider = SelectedAiProviderId();
        bool enabled = SettingsEnableAiCheckBox?.IsChecked == true;
        // Key/model visibility comes from the catalog descriptor, so adding a provider does
        // not require a second hand-written list here that silently omits it.
        bool needsKey = AiModelCatalog.TryGetProvider(provider, out var descriptor) &&
            descriptor.RequiresApiKey;
        bool isOllama = provider == "Ollama";
        bool usesModel = needsKey || isOllama;

        SettingsAiApiKeyRow.Visibility = needsKey ? Visibility.Visible : Visibility.Collapsed;
        SettingsAiOllamaRow.Visibility = isOllama ? Visibility.Visible : Visibility.Collapsed;
        SettingsAiModelRow.Visibility = usesModel ? Visibility.Visible : Visibility.Collapsed;

        // The provider list stays clickable while the master switch is off: choosing a
        // provider is how a user says "use this one", and it turns the switch on. Disabling
        // the list left the choice unchangeable and looked like the provider was ignored.
        SettingsAiProviderComboBox.IsEnabled = true;
        if (SettingsAutoQueryAiCheckBox != null)
        {
            // Auto-query is gated on the master switch in the scan path, so leaving it
            // clickable while AI is off would let the user promise something that can
            // never run.
            SettingsAutoQueryAiCheckBox.IsEnabled = enabled;
            SettingsAutoQueryAiCheckBox.ToolTip = enabled
                ? "Analyze unverified large files automatically after a scan"
                : "Enable Online file intelligence first";
        }

        if (SettingsAiApiKeyPasswordBox != null) SettingsAiApiKeyPasswordBox.IsEnabled = enabled;
        if (SettingsAiClearKeyBtn != null) SettingsAiClearKeyBtn.IsEnabled = enabled;
        if (SettingsAiOllamaEndpointBox != null) SettingsAiOllamaEndpointBox.IsEnabled = enabled;
        if (SettingsAiModelRefreshBtn != null) SettingsAiModelRefreshBtn.IsEnabled = enabled;
        if (SettingsTestAiBtn != null) SettingsTestAiBtn.IsEnabled = enabled;

        if (!usesModel && SettingsAiModelStatusText != null)
        {
            SettingsAiModelStatusText.Text = string.Empty;
        }

        if (SettingsAiProviderDescription != null)
        {
            SettingsAiProviderDescription.Text = enabled
                ? AiModelCatalog.DescribeProvider(provider)
                : "OFF - large-file analysis uses the local knowledge base. " +
                  "Turn the switch above on (or pick a provider) to use this provider.";
        }

        UpdateAiModelHint();

        PopulateAiModelSuggestions(provider, enabled);
    }

    private void PopulateAiModelSuggestions(string provider, bool enabled, IReadOnlyList<AiModelOption>? models = null)
    {
        if (SettingsAiModelSuggestionComboBox == null) return;

        // Prefer what the provider just told us over the static catalog. The catalog is
        // only a starting point: it goes stale, and a model it lists may be unavailable to
        // this account while a model it omits may be the right choice.
        IReadOnlyList<AiModelOption> options = models ?? _aiFetchedModels ?? AiModelCatalog.GetSuggestedModels(provider);

        // A saved id that neither list contains keeps its own row. Dropping it would make
        // the card look like it forgot the user's choice, and Save would write an empty id.
        // The rows are then narrowed to the free ones when the provider publishes pricing,
        // so OpenRouter never offers a row that answers 402 on a zero-credit account; a
        // provider with no pricing keeps its full list. A saved paid row is therefore
        // dropped too - the status line says so - while the hint still names what a
        // request would use.
        _aiModelPickerSource = AiModelCatalog.FreeModelsOnly(
            AiModelCatalog.WithSavedModel(options, _aiSelectedModelId, provider));
        _aiModelPickerFromProvider = models is not null || _aiFetchedModels is not null;
        _aiModelPickerEnabled = enabled;

        RebuildAiModelPicker();
    }

    private async void SettingsTestAi_Click(object sender, RoutedEventArgs e)
    {
        string provider = SelectedAiProviderId();
        string key = SettingsAiApiKeyPasswordBox?.Password?.Trim() ?? string.Empty;
        string endpoint = SettingsAiOllamaEndpointBox?.Text?.Trim() ?? string.Empty;
        string model = SelectedAiModelId();

        // Validate locally first. A missing key used to spend a network round trip (up to
        // 45 seconds against a hosted provider) only to come back as "connection failed".
        var issues = AiSettingsValidator.Validate(provider, key, endpoint, model);
        if (!AiSettingsValidator.IsValid(issues))
        {
            SettingsAiTestStatusText.Text = "⚠ " + AiSettingsValidator.Describe(issues);
            return;
        }

        SettingsTestAiBtn.IsEnabled = false;
        SettingsAiTestStatusText.Text = "Connecting & verifying provider...";
        try
        {
            var (ok, msg) = await OnlineFileIntelligenceService.TestConnectionAsync(provider, key, endpoint, model);
            SettingsAiTestStatusText.Text = ok ? $"✅ {msg}" : $"❌ {msg}";
        }
        catch (Exception ex)
        {
            SettingsAiTestStatusText.Text = $"❌ {ex.Message}";
        }
        finally
        {
            UpdateAiSettingsUiVisibility();
        }
    }

    private void SettingsClearAiCache_Click(object sender, RoutedEventArgs e)
    {
        int removed = OnlineFileIntelligenceService.ClearCache();
        SettingsAiTestStatusText.Text = removed == 0
            ? "AI reports cache was already empty."
            : $"AI reports cache cleared ({removed} {(removed == 1 ? "item" : "items")}).";
        AddLog($"AI report cache cleared: {removed} entries removed.", LogLevel.Info);
        SoundService.PlayClickSound();
    }

    private async void AskAiLargeFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is LargeFileInfo info)
        {
            // Say why instead of quietly producing an offline verdict: the row used to read
            // "Local Intelligence (Offline)" with no hint that the master switch was off.
            if (!TryPrepareAiForUserRequest(out string? reason))
            {
                info.AiProviderLabel = "AI unavailable";
                AddLog($"AI analysis skipped: {reason}", LogLevel.Warning);
                MessageBox.Show(reason, "AI Intelligence", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            btn.IsEnabled = false;
            info.AiProviderLabel = "⚡ Analyzing...";
            try
            {
                await LargeFileHunterService.AnalyzeItemWithAiAsync(info, _cts?.Token ?? default);
                RefreshLargeFileHeroStats();
                AddLog($"AI Intelligence analyzed '{info.FileName}': {info.AiVerdict}", LogLevel.Info);
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }
    }

    /// <summary>
    /// Turns "the user pressed Ask AI" into a request that can actually run.
    /// </summary>
    /// <remarks>
    /// A cloud provider plus a saved key plus a click on Ask AI is unambiguous intent, but
    /// the master switch sat in front of it: the click produced an offline verdict and the
    /// row read "Local Intelligence (Offline)", which is exactly the report a user reads as
    /// "my provider is being ignored". When the only obstacle is that switch, it is turned
    /// on here - in the click path only, never at startup or in a background scan - and the
    /// log records it so the change is visible and reversible in Settings.
    /// </remarks>
    private bool TryPrepareAiForUserRequest(out string? reason)
    {
        var readiness = LargeFileHunterService.GetAiReadiness(out reason);

        if (readiness == LargeFileHunterService.AiReadiness.SwitchOff &&
            LargeFileHunterService.TryEnableConfiguredProvider())
        {
            string provider = SettingsService.Current.AiProvider;
            if (SettingsEnableAiCheckBox != null)
            {
                SettingsEnableAiCheckBox.IsChecked = true;
            }

            AddLog($"Online AI was off; enabled {provider} because you asked AI about a file.", LogLevel.Info);
            readiness = LargeFileHunterService.GetAiReadiness(out reason);
        }

        return readiness is LargeFileHunterService.AiReadiness.Ready
            or LargeFileHunterService.AiReadiness.BuiltInLocal;
    }

    private async void AskAiSelectedLargeFiles_Click(object sender, RoutedEventArgs e)
    {
        var selected = LargeFileHunterService.SelectAnalyzeTargets(_largeFiles);

        if (selected.Count == 0)
        {
            MessageBox.Show("No files need AI analysis. Select files or scan a folder.", "AI Intelligence", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!TryPrepareAiForUserRequest(out string? reason))
        {
            AddLog($"AI analysis skipped: {reason}", LogLevel.Warning);
            MessageBox.Show(reason, "AI Intelligence", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // The target rule above used to be inline here and was duplicated as the
        // Settings auto-query promise by the WinUI app only. Extracted so the
        // manual drive and the automatic drive pick the same files.
        AskAiSelectedLargeFilesBtn.IsEnabled = false;
        try
        {
            await RunLargeFileAiBatchAsync(selected, _cts?.Token ?? default);
        }
        finally
        {
            AskAiSelectedLargeFilesBtn.IsEnabled = true;
        }
    }

    /// <summary>
    /// The Ask-AI batch body shared by the manual button and the post-scan
    /// auto-query. The progress readback overwrites the selection summary (as it
    /// always did); the hero stats are refreshed afterwards because verdicts can
    /// reclassify safety, which is also what the child sheet's trace used to rely on.
    /// </summary>
    private async Task RunLargeFileAiBatchAsync(List<LargeFileInfo> selected, CancellationToken ct)
    {
        AddLog($"Starting online AI analysis for {selected.Count} large files...", LogLevel.Info);
        var progress = new Progress<int>(pct =>
        {
            LargeFilesSelectedSummaryText.Text = $"AI Analyzing {selected.Count} files: {pct}%";
        });

        await LargeFileHunterService.BatchAnalyzeWithAiAsync(selected, progress, ct);

        if (ct.IsCancellationRequested)
        {
            AddLog($"AI intelligence analysis cancelled after {selected.Count} queued files.", LogLevel.Info);
            return;
        }

        AddLog($"Completed AI intelligence analysis for {selected.Count} files.", LogLevel.Success);
        RefreshLargeFileHeroStats();
        UpdateLargeFileSelectionSummary();
    }

    /// <summary>
    /// The Settings switch AutoQueryAiForLargeFiles finally does something: after a
    /// clean large-file scan, run the same Ask-AI batch over WPF's shared target
    /// rule (ticked, then never-verified, then everything). Requires online AI too
    /// - auto-querying a surface the user turned off would make both checkboxes lie.
    /// Runs before RunLargeFileScanAsync's finally re-enables the scan button, so a
    /// second scan cannot clear the rows out from under the running analysis, and
    /// LargeFileFilter_Changed's guard keeps its cancel path reachable.
    /// </summary>
    private async Task AutoQueryLargeFilesAfterScanAsync(CancellationToken ct)
    {
        var config = SettingsService.Current;
        if (!config.AutoQueryAiForLargeFiles || !config.EnableOnlineAiSafety) return;
        if (_isLargeFileAiBatchRunning) return;

        var selected = LargeFileHunterService.SelectAnalyzeTargets(_largeFiles);
        if (selected.Count == 0) return;

        _isLargeFileAiBatchRunning = true;
        AskAiSelectedLargeFilesBtn.IsEnabled = false;
        try
        {
            LargeFilesStatusText.Text = $"AI analyzing {selected.Count} large files...";
            await RunLargeFileAiBatchAsync(selected, ct);
            LargeFilesStatusText.Text = $"AI analysis completed for {selected.Count} large files.";
        }
        catch (OperationCanceledException)
        {
            LargeFilesStatusText.Text = "AI analysis cancelled.";
        }
        finally
        {
            _isLargeFileAiBatchRunning = false;
            AskAiSelectedLargeFilesBtn.IsEnabled = true;
        }
    }

    /// <summary>
    /// Applies memory optimizer runtime settings to the live window.
    /// </summary>
    private void ApplyMemorySettingsToWindow()
    {
        try
        {
            Topmost = SettingsService.Current.MemoryAlwaysOnTop;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] ApplyMemorySettingsToWindow suppressed: {ex.Message}");
        }
    }

    private void SettingsBrowseExcludeFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Folder to Protect from Cleaning",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
            {
                SettingsNewPathBox.Text = dialog.FolderName;
            }
        }
        catch (Exception ex)
        {
            AddLog($"Folder selection error: {ex.Message}", LogLevel.Warning);
        }
    }

    private void SettingsAddExcludePath_Click(object sender, RoutedEventArgs e)
    {
        string path = SettingsNewPathBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path)) return;

        var items = SettingsExcludedPathsListBox.ItemsSource as ObservableCollection<string>
                    ?? new ObservableCollection<string>();

        if (!items.Any(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)))
        {
            items.Add(path);
            SettingsExcludedPathsListBox.ItemsSource = items;
            SettingsNewPathBox.Clear();
            SoundService.PlayClickSound();
        }
    }

    private void SettingsRemoveExcludePath_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsExcludedPathsListBox.SelectedItem is string selected &&
            SettingsExcludedPathsListBox.ItemsSource is ObservableCollection<string> items)
        {
            items.Remove(selected);
            SoundService.PlayClickSound();
        }
    }

    private void SettingsAddExcludeExtension_Click(object sender, RoutedEventArgs e)
    {
        string ext = SettingsNewExtensionBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(ext)) return;
        if (!ext.StartsWith('.')) ext = "." + ext;

        var items = SettingsExcludedExtensionsListBox.ItemsSource as ObservableCollection<string>
                    ?? new ObservableCollection<string>();

        if (!items.Any(x => string.Equals(x, ext, StringComparison.OrdinalIgnoreCase)))
        {
            items.Add(ext);
            SettingsExcludedExtensionsListBox.ItemsSource = items;
            SettingsNewExtensionBox.Clear();
            SoundService.PlayClickSound();
        }
    }

    private void SettingsRemoveExcludeExtension_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsExcludedExtensionsListBox.SelectedItem is string selected &&
            SettingsExcludedExtensionsListBox.ItemsSource is ObservableCollection<string> items)
        {
            items.Remove(selected);
            SoundService.PlayClickSound();
        }
    }
}
