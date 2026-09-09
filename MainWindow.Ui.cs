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

    private void AddLog(string message, LogLevel level = LogLevel.Info)
    {
        Dispatcher.Invoke(() =>
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message
            };
            _logs.Add(entry);
            if (LogDrawerBorder.Visibility == Visibility.Visible)
            {
                LogScrollViewer.ScrollToEnd();
            }
        });
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
        if (item is not TargetFolderInfo target) return false;

        // 1. Tag filter
        if (_currentFilterTag == "SAFE" && !target.IsSafeModeEligible) return false;
        if (_currentFilterTag == "SYSTEM" &&
            !target.Category.Contains("System", StringComparison.OrdinalIgnoreCase) &&
            !target.Category.Contains("Driver", StringComparison.OrdinalIgnoreCase) &&
            !target.Category.Contains("Diagnostics", StringComparison.OrdinalIgnoreCase) &&
            !target.Category.Contains("Security", StringComparison.OrdinalIgnoreCase) &&
            !target.Category.Contains("Storage", StringComparison.OrdinalIgnoreCase) &&
            !target.Category.Contains("SO", StringComparison.OrdinalIgnoreCase)) return false;
        if (_currentFilterTag == "GAMING" &&
            !target.Category.Contains("Gaming", StringComparison.OrdinalIgnoreCase) &&
            !target.Category.Contains("Shader", StringComparison.OrdinalIgnoreCase) &&
            !target.Category.Contains("GPU", StringComparison.OrdinalIgnoreCase)) return false;
        if (_currentFilterTag == "MEDIA" &&
            !target.Category.Contains("Media", StringComparison.OrdinalIgnoreCase) &&
            !target.Category.Contains("App", StringComparison.OrdinalIgnoreCase) &&
            !target.Category.Contains("Browser", StringComparison.OrdinalIgnoreCase) &&
            !target.Category.Contains("Store", StringComparison.OrdinalIgnoreCase) &&
            !target.Category.Contains("Dev", StringComparison.OrdinalIgnoreCase) &&
            !target.Category.Contains("User", StringComparison.OrdinalIgnoreCase) &&
            !target.Category.Contains("Creator", StringComparison.OrdinalIgnoreCase)) return false;

        // 2. Search text filter
        if (string.IsNullOrWhiteSpace(_currentSearchText)) return true;

        var term = _currentSearchText.Trim();
        return target.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
            || target.Description.Contains(term, StringComparison.OrdinalIgnoreCase)
            || target.Category.Contains(term, StringComparison.OrdinalIgnoreCase)
            || target.FolderPath.Contains(term, StringComparison.OrdinalIgnoreCase);
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

    // =======================================================
    // Windows System Integrity & Corruption Repair Handlers
    // =======================================================
    private CancellationTokenSource? _systemRepairCts;

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
