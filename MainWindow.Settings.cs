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
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace WinTempCleaner;

// Settings hub: categorized navigation, update choices, AI provider configuration.
public partial class MainWindow
{

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
    }

    private void CloseSettings_Click(object sender, RoutedEventArgs e)
    {
        SettingsModalOverlay.Visibility = Visibility.Collapsed;
        SoundService.PlayClickSound();
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        SettingsService.Update(s =>
        {
            s.EnableAutoPilot = SettingsAutoPilotCheckBox.IsChecked == true;
            s.MinimizeToTray = SettingsTrayCheckBox.IsChecked == true;
            s.AutoCleanNotify = SettingsNotifyCheckBox.IsChecked == true;
            s.SoundEnabled = SettingsSoundCheckBox.IsChecked == true;
            SoundService.IsSoundEnabled = s.SoundEnabled;

            s.SendToRecycleBin = SettingsRecycleBinCheckBox.IsChecked == true;
            s.LowDiskAlertEnabled = SettingsLowDiskAlertCheckBox.IsChecked == true;

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

            s.EnableOnlineAiSafety = SettingsEnableAiCheckBox.IsChecked == true;
            if (SettingsAiProviderComboBox.SelectedItem is ComboBoxItem provItem && provItem.Tag is string provTag)
            {
                s.AiProvider = provTag;
            }
            if (!string.IsNullOrEmpty(SettingsAiApiKeyPasswordBox.Password))
            {
                s.AiApiKey = SettingsAiApiKeyPasswordBox.Password;
            }
            s.AiModelName = SettingsAiModelBox.Text.Trim();
            s.AiOllamaEndpoint = string.IsNullOrWhiteSpace(SettingsAiOllamaEndpointBox.Text)
                ? "http://localhost:11434"
                : SettingsAiOllamaEndpointBox.Text.Trim();
        });

        AutoCleanService.Start();
        ApplyMemorySettingsToWindow();

        SettingsModalOverlay.Visibility = Visibility.Collapsed;
        SoundService.PlayClickSound();
        AddLog("Preferences & Auto-Pilot Guardian settings saved.", LogLevel.Success);
    }

    private void SettingsAiProviderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateAiSettingsUiVisibility();
    }

    private void UpdateAiSettingsUiVisibility()
    {
        if (SettingsAiProviderComboBox == null || SettingsAiApiKeyRow == null || SettingsAiOllamaRow == null || SettingsAiModelRow == null) return;

        string prov = (SettingsAiProviderComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "BuiltIn";
        bool needsKey = prov is "Gemini" or "Groq" or "OpenAI" or "OpenRouter";
        bool isOllama = prov == "Ollama";

        SettingsAiApiKeyRow.Visibility = needsKey ? Visibility.Visible : Visibility.Collapsed;
        SettingsAiOllamaRow.Visibility = isOllama ? Visibility.Visible : Visibility.Collapsed;
        SettingsAiModelRow.Visibility = (needsKey || isOllama) ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void SettingsTestAi_Click(object sender, RoutedEventArgs e)
    {
        SettingsTestAiBtn.IsEnabled = false;
        SettingsAiTestStatusText.Text = "Connecting & verifying provider...";
        try
        {
            string prov = (SettingsAiProviderComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "BuiltIn";
            string key = SettingsAiApiKeyPasswordBox.Password;
            string end = SettingsAiOllamaEndpointBox.Text;
            string model = SettingsAiModelBox.Text;

            var (ok, msg) = await OnlineFileIntelligenceService.TestConnectionAsync(prov, key, end, model);
            SettingsAiTestStatusText.Text = ok ? $"✅ {msg}" : $"❌ {msg}";
        }
        finally
        {
            SettingsTestAiBtn.IsEnabled = true;
        }
    }

    private void SettingsClearAiCache_Click(object sender, RoutedEventArgs e)
    {
        OnlineFileIntelligenceService.ClearCache();
        SettingsAiTestStatusText.Text = "AI reports cache cleared (0 items).";
    }

    private async void AskAiLargeFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is LargeFileInfo info)
        {
            btn.IsEnabled = false;
            info.AiProviderLabel = "⚡ Analyzing...";
            try
            {
                await LargeFileHunterService.AnalyzeItemWithAiAsync(info);
                RefreshLargeFileHeroStats();
                AddLog($"AI Intelligence analyzed '{info.FileName}': {info.AiVerdict}", LogLevel.Info);
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }
    }

    private async void AskAiSelectedLargeFiles_Click(object sender, RoutedEventArgs e)
    {
        var selected = _largeFiles.Where(f => f.IsSelected).ToList();
        if (selected.Count == 0)
        {
            // If no specific checkboxes are marked, analyze all scanned files that haven't been verified yet
            selected = _largeFiles.Where(f => !f.IsAiOnlineVerified).ToList();
            if (selected.Count == 0 && _largeFiles.Count > 0)
            {
                // If all were previously analyzed, allow re-analyzing all scanned files
                selected = _largeFiles.ToList();
            }
        }

        if (selected.Count == 0)
        {
            MessageBox.Show("No files need AI analysis. Select files or scan a folder.", "AI Intelligence", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        AskAiSelectedLargeFilesBtn.IsEnabled = false;
        try
        {
            AddLog($"Starting online AI analysis for {selected.Count} large files...", LogLevel.Info);
            var progress = new Progress<int>(pct =>
            {
                LargeFilesSelectedSummaryText.Text = $"AI Analyzing {selected.Count} files: {pct}%";
            });

            await LargeFileHunterService.BatchAnalyzeWithAiAsync(selected, progress);
            AddLog($"Completed AI intelligence analysis for {selected.Count} files.", LogLevel.Success);
            RefreshLargeFileHeroStats();
            UpdateLargeFileSelectionSummary();
        }
        finally
        {
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
}
