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
using WinTempCleaner.ViewModels;

namespace WinTempCleaner;

// Log stream, theme/sound/language preferences and target filtering/search.
public partial class MainWindow
{

    private void CopyLogButton_Click(object sender, RoutedEventArgs e)
    {
        var sb = new StringBuilder();
        foreach (var log in _logs)
        {
            sb.AppendLine($"[{log.FormattedTime}] [{log.Level}] {log.Message}");
        }

        Clipboard.SetText(sb.ToString());
        AddLog("Activity log copied to clipboard.", LogLevel.Info);
    }

    private void ClearLogButton_Click(object sender, RoutedEventArgs e)
    {
        _logs.Clear();
    }

    private const int MaxLogEntries = 2000;
    private long _lastLogScrollTicks;

    private void AddLog(string message, LogLevel level = LogLevel.Info)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message
        };

        // Non-blocking dispatch: background scan/clean threads must never stall
        // waiting on the UI thread for a log line (was synchronous Invoke).
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _logs.Add(entry);

            // Bounded stream: drop the oldest entries once the cap is reached so a
            // long scan cannot grow the collection and visual tree without limit.
            int overflow = _logs.Count - MaxLogEntries;
            for (int i = 0; i < overflow; i++)
            {
                _logs.RemoveAt(0);
            }

            // Throttled auto-scroll: avoids forcing a layout pass per entry.
            if (LogDrawerBorder.Visibility == Visibility.Visible &&
                Stopwatch.GetElapsedTime(_lastLogScrollTicks).TotalMilliseconds >= 120)
            {
                _lastLogScrollTicks = Stopwatch.GetTimestamp();
                LogScrollViewer.ScrollToEnd();
            }
        }));
    }

    private void ThemeToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        ThemeService.SetTheme(!ThemeService.IsDarkMode);
        ThemeToggleIcon.Text = ThemeService.IsDarkMode ? "\uE708" : "\uE706";
        SoundService.PlayClickSound();
        AddLog($"Theme switched to {(ThemeService.IsDarkMode ? "Dark Obsidian" : "Nordic Frost Light")}", LogLevel.Info);
    }

    private void SoundToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        SoundService.IsSoundEnabled = !SoundService.IsSoundEnabled;
        SoundToggleIcon.Text = SoundService.IsSoundEnabled ? "\uE767" : "\uE74F";
        SoundToggleIcon.Foreground = SoundService.IsSoundEnabled
            ? (Brush)FindResource("ElectricCyanBrush")
            : (Brush)FindResource("TextMutedBrush");

        if (SoundService.IsSoundEnabled)
        {
            SoundService.PlayClickSound();
            AddLog("Haptic sound effects enabled.", LogLevel.Info);
        }
        else
        {
            AddLog("Haptic sound effects muted.", LogLevel.Info);
        }
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string langCode)
        {
            LocalizationService.CurrentLanguage = langCode;
            SoundService.PlayClickSound();
            ApplyLocalization();
        }
    }

    private void ApplyLocalization()
    {
        bool isAr = LocalizationService.CurrentLanguage == "ar";
        FlowDirection = isAr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        // 1. Header & Badges
        BrandSubtitleText.Text = LocalizationService.Get("AppSubtitle");
        AdminBadgeText.Text = _isAdmin ? LocalizationService.Get("AdminLabel") : "Elevate";

        // 2. Hero & Telemetry
        HeroHeaderLabel.Text = LocalizationService.Get("ReclaimableSpace");
        HeroSubtext.Text = LocalizationService.Get("HeroScanSubtext");
        UpdateDriveTelemetry();

        // 3. Toolbar & Buttons
        SelectSafeBtnText.Text = LocalizationService.Get("SelectSafe");
        SmartCleanBtnText.Text = LocalizationService.Get("SmartClean");
        SelectAllBtn.Content = LocalizationService.Get("SelectAll");
        ClearBtn.Content = LocalizationService.Get("Clear");
        QuickScanBtnText.Text = LocalizationService.Get("Rescan");
        SafeModeLabelText.Text = LocalizationService.Get("SafetyShield");

        // 4. Bottom Dock
        if (!_isBusy)
        {
            ProgressStatusText.Text = LocalizationService.Get("ReadyStatus");
        }
        ToggleLogText.Text = LogDrawerBorder.Visibility == Visibility.Visible ? LocalizationService.Get("HideLog") : LocalizationService.Get("ActivityLog");
        ExportReportBtnText.Text = LocalizationService.Get("ExportReport");
        CancelButton.Content = LocalizationService.Get("Cancel");

        // 5. Modals & Overlays
        InspectorTitleText.Text = LocalizationService.Get("InspectorTitle");
        InspectorSubtitleText.Text = LocalizationService.Get("InspectorSubtitle");
        CloseInspectorBtn.Content = LocalizationService.Get("CloseInspector");

        ConfirmModalTitleText.Text = LocalizationService.Get("ConfirmTitle");
        ConfirmModalSubtitleText.Text = LocalizationService.Get("ConfirmSubtitle");
        ConfirmModalReclaimableLabel.Text = LocalizationService.Get("ConfirmReclaimableLabel");
        ConfirmModalShieldText.Text = SafeModeCheckBox.IsChecked == true ? LocalizationService.Get("ConfirmShieldOn") : LocalizationService.Get("ConfirmShieldOff");
        ConfirmModalSummaryText.Text = LocalizationService.Get("ConfirmSummary");
        ConfirmModalCancelBtn.Content = LocalizationService.Get("Cancel");
        ConfirmModalProceedBtnText.Text = LocalizationService.Get("StartCleanup");

        CelebrationModalTitleText.Text = LocalizationService.Get("CompletedTitle");
        CelebrationFilesLabel.Text = LocalizationService.Get("FilesDeleted");
        CelebrationFoldersLabel.Text = LocalizationService.Get("FoldersPurged");
        CelebrationTimeLabel.Text = LocalizationService.Get("TimeElapsed");
        CelebrationExportBtn.Content = LocalizationService.Get("ExportReport");
        CelebrationDoneBtn.Content = LocalizationService.Get("Awesome");

        // 6. Localize All 12 Target Categories
        foreach (var target in _targets)
        {
            LocalizationService.LocalizeTarget(target);
        }

        _targetsCollectionView?.Refresh();
        RecalculateTotals();
        AddLog($"Language switched to {LocalizationService.CurrentLanguage.ToUpperInvariant()}", LogLevel.Info);
    }

    private ReleaseInfo? _pendingRelease;

    private bool FilterTargetPredicate(object item)
    {
        // Filter/search semantics live in CleaningPipelineViewModel (unit-tested, MVVM phase 2).
        return item is TargetFolderInfo target &&
               CleaningPipelineViewModel.MatchesFilter(target, _currentFilterTag, _currentSearchText);
    }

    private void CategorySearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _currentSearchText = CategorySearchBox.Text;
        SearchPlaceholderText.Visibility = string.IsNullOrEmpty(_currentSearchText) ? Visibility.Visible : Visibility.Collapsed;
        ClearSearchBtn.Visibility = string.IsNullOrEmpty(_currentSearchText) ? Visibility.Collapsed : Visibility.Visible;
        _targetsCollectionView?.Refresh();
    }

    private void ClearSearchBtn_Click(object sender, RoutedEventArgs e)
    {
        CategorySearchBox.Text = string.Empty;
        SoundService.PlayClickSound();
    }

    private void FilterChip_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag)
        {
            _currentFilterTag = tag;
            _targetsCollectionView?.Refresh();
            SoundService.PlayClickSound();
        }
    }


    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        AboutModalOverlay.Visibility = Visibility.Visible;
    }

    private void CloseAbout_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        AboutModalOverlay.Visibility = Visibility.Collapsed;
    }
}
