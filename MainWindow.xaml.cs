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
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace WinTempCleaner;

public partial class MainWindow : Window
{
    private readonly CleanerService _cleanerService = new();
    private readonly ObservableCollection<TargetFolderInfo> _targets = new();
    private readonly ObservableCollection<LogEntry> _logs = new();
    private readonly ObservableCollection<JunkFileItem> _inspectedFiles = new();
    private readonly ObservableCollection<LargeFileInfo> _largeFiles = new();
    private readonly ICollectionView _targetsCollectionView;
    private string _currentFilterTag = "ALL";
    private string _currentSearchText = string.Empty;
    private CancellationTokenSource? _cts;
    private bool _isBusy;
    private bool _isAdmin;
    private long _sessionTotalFreed;
    private CleanSummary? _lastSummary;
    private uint _restoreMsgId;
    private bool _isLoaded;

    public MainWindow()
    {
        InitializeComponent();

        _targetsCollectionView = CollectionViewSource.GetDefaultView(_targets);
        _targetsCollectionView.Filter = FilterTargetPredicate;

        TargetCardsItemsControl.ItemsSource = _targetsCollectionView;
        LogItemsControl.ItemsSource = _logs;
        InspectorItemsControl.ItemsSource = _inspectedFiles;
        LargeFilesItemsControl.ItemsSource = _largeFiles;

        Loaded += MainWindow_Loaded;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ChangeWindowMessageFilter(uint message, uint dwFlag);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private const uint MSGFLT_ADD = 1;
    private const uint WM_SETICON = 0x0080;
    private const IntPtr ICON_SMALL = 0;
    private const IntPtr ICON_BIG = (IntPtr)1;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            try
            {
                IntPtr hModule = GetModuleHandle(null);
                IntPtr hIcon = LoadIcon(hModule, (IntPtr)1);
                if (hIcon != IntPtr.Zero)
                {
                    SendMessage(source.Handle, WM_SETICON, ICON_SMALL, hIcon);
                    SendMessage(source.Handle, WM_SETICON, ICON_BIG, hIcon);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] WM_SETICON suppressed: {ex.Message}");
            }

            _restoreMsgId = SingleInstanceManager.RegisterWindowMessage(SingleInstanceManager.ShowWindowMessageName);
            if (_restoreMsgId != 0)
            {
                try
                {
                    ChangeWindowMessageFilter(_restoreMsgId, MSGFLT_ADD);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
                }
            }
            source.AddHook(WndProcInstanceHook);
        }
    }

    private IntPtr WndProcInstanceHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_restoreMsgId != 0 && msg == _restoreMsgId)
        {
            Dispatcher.Invoke(() =>
            {
                Show();
                Visibility = Visibility.Visible;
                if (WindowState == WindowState.Minimized)
                {
                    WindowState = WindowState.Normal;
                }
                Activate();
                Topmost = true;
                Topmost = false;
                Focus();
            });
            handled = true;
        }
        return IntPtr.Zero;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        CheckAdminPrivileges();
        UpdateDriveTelemetry();
        UpdateMemoryTelemetry();
        InitializeTargets();
        ApplyLocalization();

        // Initialize Tray and Auto-Pilot Guardian
        TrayService.Initialize(
            this,
            () => Dispatcher.Invoke(async () => await CleanSafeFromTrayAsync()),
            () => Dispatcher.Invoke(() => OpenSettingsModal()),
            () => Dispatcher.Invoke(async () =>
            {
                var res = await MemoryOptimizerService.OptimizeRamAsync();
                UpdateMemoryTelemetry();
                AddLog($"⚡ Tray RAM Boost: Reclaimed {res.FormattedReclaimed} across {res.ProcessesOptimized} tasks in {res.ExecutionTimeMs}ms.", LogLevel.Success);
                return res;
            }));

        AutoCleanService.Start();
        LoadSettingsIntoUI();

        if (SettingsService.Current.CheckUpdatesOnStartup)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(2500);
                await CheckForUpdatesInternalAsync(silent: true);
            });
        }

        await RunScanAllAsync();
    }

    private void LoadSettingsIntoUI()
    {
        SettingsAutoPilotCheckBox.IsChecked = SettingsService.Current.EnableAutoPilot;
        SettingsTrayCheckBox.IsChecked = SettingsService.Current.MinimizeToTray;
        SettingsNotifyCheckBox.IsChecked = SettingsService.Current.AutoCleanNotify;
        SettingsCheckUpdatesCheckBox.IsChecked = SettingsService.Current.CheckUpdatesOnStartup;
        ManualCheckStatusText.Text = $"Current: v{UpdateService.CurrentVersion}";

        // Find matching interval combo box item (no loop needed — just pick by tag)
        ComboBoxItem? foundInterval = null;
        foreach (ComboBoxItem candidate in SettingsIntervalComboBox.Items)
        {
            if (candidate.Tag is string itag && int.TryParse(itag, out int ival) && ival == SettingsService.Current.AutoCleanIntervalHours)
            {
                foundInterval = candidate;
                break;
            }
        }
        SettingsIntervalComboBox.SelectedItem = foundInterval;

        // Memory Optimizer settings
        MemoryAutoOptCheckBox.IsChecked = SettingsService.Current.MemoryAutoOptimizeEnabled;
        MemoryShowInTrayCheckBox.IsChecked = SettingsService.Current.MemoryShowInTray;
        MemoryAlwaysOnTopCheckBox.IsChecked = SettingsService.Current.MemoryAlwaysOnTop;
        MemoryCompactModeCheckBox.IsChecked = SettingsService.Current.MemoryCompactMode;
        MemoryCloseToTrayCheckBox.IsChecked = SettingsService.Current.MemoryCloseToTray;
        MemoryShowNotifyCheckBox.IsChecked = SettingsService.Current.MemoryShowNotifications;

        foreach (ComboBoxItem mi in MemoryAutoOptIntervalComboBox.Items)
        {
            if (mi.Tag is string mt && int.TryParse(mt, out int mh) && mh == SettingsService.Current.MemoryAutoOptimizeIntervalHours)
            {
                MemoryAutoOptIntervalComboBox.SelectedItem = mi;
                break;
            }
        }

        foreach (ComboBoxItem mt in MemoryThresholdComboBox.Items)
        {
            if (mt.Tag is string ttt && int.TryParse(ttt, out int tv) && tv == SettingsService.Current.MemoryAutoOptimizeFreeRamThresholdPercent)
            {
                MemoryThresholdComboBox.SelectedItem = mt;
                break;
            }
        }

        ApplyMemorySettingsToWindow();
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
        SettingsService.Current.EnableAutoPilot = SettingsAutoPilotCheckBox.IsChecked == true;
        SettingsService.Current.MinimizeToTray = SettingsTrayCheckBox.IsChecked == true;
        SettingsService.Current.AutoCleanNotify = SettingsNotifyCheckBox.IsChecked == true;
        SettingsService.Current.CheckUpdatesOnStartup = SettingsCheckUpdatesCheckBox.IsChecked == true;

        if (SettingsIntervalComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag && int.TryParse(tag, out int hours))
        {
            SettingsService.Current.AutoCleanIntervalHours = hours;
        }

        // Memory Optimizer settings
        SettingsService.Current.MemoryAutoOptimizeEnabled = MemoryAutoOptCheckBox.IsChecked == true;
        SettingsService.Current.MemoryShowInTray = MemoryShowInTrayCheckBox.IsChecked == true;
        SettingsService.Current.MemoryAlwaysOnTop = MemoryAlwaysOnTopCheckBox.IsChecked == true;
        SettingsService.Current.MemoryCompactMode = MemoryCompactModeCheckBox.IsChecked == true;
        SettingsService.Current.MemoryCloseToTray = MemoryCloseToTrayCheckBox.IsChecked == true;
        SettingsService.Current.MemoryShowNotifications = MemoryShowNotifyCheckBox.IsChecked == true;

        if (MemoryAutoOptIntervalComboBox.SelectedItem is ComboBoxItem mi && mi.Tag is string mt && int.TryParse(mt, out int mph))
        {
            SettingsService.Current.MemoryAutoOptimizeIntervalHours = mph;
        }

        if (MemoryThresholdComboBox.SelectedItem is ComboBoxItem mt2 && mt2.Tag is string ttt && int.TryParse(ttt, out int tval))
        {
            SettingsService.Current.MemoryAutoOptimizeFreeRamThresholdPercent = tval;
        }

        SettingsService.SaveSettings();
        AutoCleanService.Start();
        ApplyMemorySettingsToWindow();

        SettingsModalOverlay.Visibility = Visibility.Collapsed;
        SoundService.PlayClickSound();
        AddLog("Preferences & Auto-Pilot Guardian settings saved.", LogLevel.Success);
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

    private void CheckAdminPrivileges()
    {
        _isAdmin = ElevationService.IsRunAsAdmin();
        if (_isAdmin)
        {
            AdminBadgeText.Text = "Admin";
            AdminBadgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));
            AdminBadgeIcon.Text = "\uE73E";
            AdminBadgeIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            AdminBadgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#142B20"));
            AdminBadgeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3D10B981"));
            AdminElevationButton.ToolTip = "Running with full Administrator privileges (Full access to all system locations).";
            AddLog("Running with Administrator privileges (Full access to all system locations)", LogLevel.Success);
        }
        else
        {
            AdminBadgeText.Text = "Elevate";
            AdminBadgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FBBF24"));
            AdminBadgeIcon.Text = "\uE7EF";
            AdminBadgeIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
            AdminBadgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#281C0E"));
            AdminBadgeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4DF59E0B"));
            AdminElevationButton.ToolTip = "Running as Standard User. Click to relaunch as Administrator to clean system-level caches.";
            AddLog("Running as Standard User. Windows system caches require Administrator rights.", LogLevel.Warning);
        }
    }

    private void AdminElevationButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ElevationService.IsRunAsAdmin())
        {
            var result = MessageBox.Show(
                "Relaunch Deltempo with Administrator privileges?\n\nThis grants access to clean Windows Update cache, System Temp, Prefetch, and Driver packages.",
                "Elevate to Administrator",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                ElevationService.RestartAsAdmin();
            }
        }
    }

    private void UpdateDriveTelemetry(long additionalFreedBytes = 0)
    {
        var telemetry = DriveTelemetryService.GetSystemDriveTelemetry();
        DriveTelemetryLabel.Text = $"OS Drive ({telemetry.DriveLetter})";
        DriveTelemetryPercentage.Text = $"{telemetry.FreePercentage:F1}% Free";
        DriveTelemetryDetails.Text = $"{telemetry.FormattedFree} free of {telemetry.FormattedTotal}";
        DriveUsageBar.Value = telemetry.UsedPercentage;

        if (additionalFreedBytes > 0)
        {
            _sessionTotalFreed += additionalFreedBytes;
            HeroSubtext.Text = $"Reclaimed {TargetFolderInfo.FormatBytes(_sessionTotalFreed)} this session • {telemetry.FormattedFree} currently free";
        }
    }

    private void InitializeTargets()
    {
        _targets.Clear();
        var defaultTargets = CleanerService.GetDefaultTargets();
        foreach (var target in defaultTargets)
        {
            _targets.Add(target);
        }
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (SettingsModalOverlay.Visibility == Visibility.Visible)
            {
                CloseSettings_Click(sender, e);
                e.Handled = true;
            }
            else if (ConfirmModalOverlay.Visibility == Visibility.Visible)
            {
                CancelConfirmModal_Click(sender, e);
                e.Handled = true;
            }
            else if (CelebrationModalOverlay.Visibility == Visibility.Visible)
            {
                CloseCelebration_Click(sender, e);
                e.Handled = true;
            }
            else if (InspectorModalOverlay.Visibility == Visibility.Visible)
            {
                CloseInspector_Click(sender, e);
                e.Handled = true;
            }
            else if (StartupModalOverlay.Visibility == Visibility.Visible)
            {
                CloseStartupModal_Click(sender, e);
                e.Handled = true;
            }
            else if (LargeFilesModalOverlay.Visibility == Visibility.Visible)
            {
                CloseLargeFilesModal_Click(sender, e);
                e.Handled = true;
            }
            else if (ProcessModalOverlay.Visibility == Visibility.Visible)
            {
                CloseProcessModal_Click(sender, e);
                e.Handled = true;
            }
            else if (UpdateModalOverlay.Visibility == Visibility.Visible)
            {
                CloseUpdateModal_Click(sender, e);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.F5 || (e.Key == Key.R && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control))
        {
            if (!_isBusy)
            {
                ScanButton_Click(sender, e);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (!_isBusy)
            {
                CleanButton_Click(sender, e);
                e.Handled = true;
            }
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsService.Current.MinimizeToTray)
        {
            TrayService.MinimizeToTray();
        }
        else
        {
            WindowState = WindowState.Minimized;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            var res = MessageBox.Show("A cleanup operation is currently in progress. Do you really want to exit?",
                "Operation in Progress", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes) return;
        }

        if (SettingsService.Current.MinimizeToTray)
        {
            TrayService.MinimizeToTray();
        }
        else
        {
            TrayService.Dispose();
            Close();
        }
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        await RunScanAllAsync();
    }

    private async Task RunScanAllAsync()
    {
        _isBusy = true;
        SetControlsEnabled(false);
        _cts = new CancellationTokenSource();

        AppProgressBar.IsIndeterminate = true;
        ProgressStatusText.Text = "Scanning all target categories...";
        ProgressPercentageText.Text = "--";
        AddLog("Starting full precision scan of temporary locations...", LogLevel.Info);

        try
        {
            bool safeMode = SafeModeCheckBox.IsChecked == true;
            var scanTasks = _targets.Select(target => _cleanerService.ScanFolderAsync(target, AddLog, _cts.Token, safeMode));
            await Task.WhenAll(scanTasks);

            RecalculateTotals();
            ProgressStatusText.Text = "Scan completed. Ready to clean.";
            AddLog($"Scan finished. Total reclaimable space: {HeroSizeText.Text}", LogLevel.Success);
        }
        catch (OperationCanceledException)
        {
            ProgressStatusText.Text = "Scan cancelled.";
            AddLog("Scan operation was cancelled by user.", LogLevel.Warning);
        }
        catch (Exception ex)
        {
            ProgressStatusText.Text = "Scan error.";
            AddLog($"Scan error: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            AppProgressBar.IsIndeterminate = false;
            AppProgressBar.Value = 0;
            ProgressPercentageText.Text = "0%";
            _isBusy = false;
            SetControlsEnabled(true);
        }
    }

    private void CleanButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        var selectedTargets = _targets.Where(t => t.IsSelected).ToList();
        if (selectedTargets.Count == 0)
        {
            AddLog("Please select at least one category to clean.", LogLevel.Warning);
            ProgressStatusText.Text = "No categories selected.";
            return;
        }

        long totalEstimatedBytes = selectedTargets.Sum(t => t.SizeBytes);
        bool safeMode = SafeModeCheckBox.IsChecked == true;

        ConfirmModalSizeText.Text = TargetFolderInfo.FormatBytes(totalEstimatedBytes);
        ConfirmModalShieldText.Text = safeMode ? "🟢 Safety Shield: ON" : "⚠️ Safety Shield: OFF";
        ConfirmModalShieldBadge.BorderBrush = safeMode ? (Brush)FindResource("EmeraldGreenBrush") : (Brush)FindResource("AmberWarningBrush");
        ConfirmModalShieldBadge.Background = safeMode ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10241B")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A1E16"));

        ConfirmModalSummaryText.Text = $"Purging {selectedTargets.Count} selected categories. System integrity, active accounts, and work documents are strictly protected.";
        ConfirmModalOverlay.Visibility = Visibility.Visible;
    }

    private void CancelConfirmModal_Click(object sender, RoutedEventArgs e)
    {
        ConfirmModalOverlay.Visibility = Visibility.Collapsed;
    }

    private async void ProceedConfirmModal_Click(object sender, RoutedEventArgs e)
    {
        ConfirmModalOverlay.Visibility = Visibility.Collapsed;
        await ExecuteCleanupAsync();
    }

    private async Task ExecuteCleanupAsync()
    {
        var selectedTargets = _targets.Where(t => t.IsSelected).ToList();
        if (selectedTargets.Count == 0) return;

        bool safeMode = SafeModeCheckBox.IsChecked == true;
        _isBusy = true;
        SetControlsEnabled(false);
        CancelButton.Visibility = Visibility.Visible;
        _cts = new CancellationTokenSource();

        var stopwatch = Stopwatch.StartNew();
        long totalFreed = 0;
        int totalFilesDeleted = 0;
        int totalFoldersDeleted = 0;
        int totalFilesSkipped = 0;

        AddLog($"Starting cleanup with Safety Shield {(safeMode ? "ENABLED (>24h old only)" : "DISABLED (all files)")}...", LogLevel.Info);

        try
        {
            for (int i = 0; i < selectedTargets.Count; i++)
            {
                if (_cts.IsCancellationRequested) break;

                var target = selectedTargets[i];
                double targetBaseProgress = (double)i / selectedTargets.Count * 100;
                double targetSpan = 100.0 / selectedTargets.Count;

                ProgressStatusText.Text = $"Cleaning {target.Name}...";

                var progressHandler = new Action<double>(val =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        double currentPct = targetBaseProgress + (val * targetSpan);
                        AppProgressBar.Value = Math.Min(100, currentPct);
                        ProgressPercentageText.Text = $"{(int)AppProgressBar.Value}%";
                    });
                });

                var (freed, filesDel, foldersDel, filesSkip) = await _cleanerService.CleanFolderAsync(
                    target,
                    safeMode,
                    AddLog,
                    progressHandler,
                    _cts.Token);

                totalFreed += freed;
                totalFilesDeleted += filesDel;
                totalFoldersDeleted += foldersDel;
                totalFilesSkipped += filesSkip;
            }

            stopwatch.Stop();
            AppProgressBar.Value = 100;
            ProgressPercentageText.Text = "100%";

            RecalculateTotals();
            UpdateDriveTelemetry(totalFreed);

            // Zero out cleaned targets immediately so hero size & button reflect reality right now,
            // then schedule a background rescan to re-measure all remaining dirty targets.
            foreach (var t in selectedTargets)
            {
                t.SizeBytes = 0;
                t.FileCount = 0;
            }
            RecalculateTotals(); // push zeroed values to HeroSizeText + CleanButtonText instantly

            // Background rescan: re-measures remaining targets and updates the hero with real numbers
            _ = Task.Run(async () =>
            {
                await Task.Delay(600);
                await Dispatcher.InvokeAsync(() => ScanButton_Click(this, new RoutedEventArgs()));
            });

            _lastSummary = new CleanSummary
            {
                TotalFreedBytes = totalFreed,
                TotalFilesDeleted = totalFilesDeleted,
                TotalFoldersDeleted = totalFoldersDeleted,
                TotalFilesSkipped = totalFilesSkipped,
                ElapsedTime = stopwatch.Elapsed
            };

            ProgressStatusText.Text = "Cleanup complete!";
            AddLog($"Cleanup Finished: Freed {_lastSummary.FormattedFreedSize} ({totalFilesDeleted:N0} deleted, {totalFilesSkipped:N0} protected) in {stopwatch.Elapsed.TotalSeconds:N1}s", LogLevel.Success);

            // Show Animated Celebration Modal Dialog
            CelebrationModalTitleText.Text = "Cleanup Completed!";
            CelebrationReclaimedText.Text = $"Successfully Reclaimed {_lastSummary.FormattedFreedSize}";
            CelebrationFilesText.Text = $"{totalFilesDeleted:N0}";
            CelebrationFoldersText.Text = $"{totalFoldersDeleted:N0}";
            CelebrationRamText.Text = "-- MB";
            CelebrationTimeText.Text = $"{stopwatch.Elapsed.TotalSeconds:N1}s";
            CelebrationModalOverlay.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException)
        {
            ProgressStatusText.Text = "Cleanup cancelled.";
            AddLog("Cleanup was cancelled by user.", LogLevel.Warning);
        }
        catch (Exception ex)
        {
            ProgressStatusText.Text = "Error during cleanup.";
            AddLog($"Cleanup Error: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            _isBusy = false;
            CancelButton.Visibility = Visibility.Collapsed;
            SetControlsEnabled(true);
        }
    }

    private void CloseCelebration_Click(object sender, RoutedEventArgs e)
    {
        CelebrationModalOverlay.Visibility = Visibility.Collapsed;
    }

    private void CelebrationExport_Click(object sender, RoutedEventArgs e)
    {
        CelebrationModalOverlay.Visibility = Visibility.Collapsed;
        ExportAuditReport_Click(sender, e);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        CancelButton.IsEnabled = false;
        ProgressStatusText.Text = "Cancelling operation...";
    }

    private async void HeroOneClickDeepCleanBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        SoundService.PlayClickSound();
        _isBusy = true;
        _cts = new CancellationTokenSource();
        SetControlsEnabled(false);
        CancelButton.Visibility = Visibility.Visible;
        CancelButton.IsEnabled = true;

        ProgressStatusText.Text = "⚡ Running 1-Click Deep Clean...";
        HeroSubtext.Text = "Autonomous deep clean in progress...";

        try
        {
            var progress = new Progress<DeepCleanProgress>(p =>
            {
                Dispatcher.Invoke(() =>
                {
                    ProgressStatusText.Text = $"⚡ [{p.CurrentStage}] {p.DetailMessage}";
                    HeroSubtext.Text = p.DetailMessage;
                });
            });

            var result = await DeepCleanEngine.ExecuteDeepCleanAsync(
                logAction: (msg, lvl) => Dispatcher.Invoke(() => AddLog(msg, lvl)),
                progress: progress,
                purgeAllRestorePoints: false,
                ct: _cts.Token);

            _lastSummary = new CleanSummary
            {
                TotalFreedBytes = result.DiskFreedBytes,
                TotalFilesDeleted = result.FilesDeleted,
                TotalFoldersDeleted = result.FoldersDeleted,
                TotalFilesSkipped = result.FilesSkipped,
                ElapsedTime = result.Duration
            };

            ProgressStatusText.Text = $"1-Click Deep Clean complete! Reclaimed {result.FormattedDiskFreed} disk, {result.FormattedRamFreed} RAM.";
            HeroSubtext.Text = $"Last Clean: {result.FormattedDiskFreed} disk, {result.FormattedRamFreed} RAM freed in {result.Duration.TotalSeconds:0.1}s";
            AddLog($"1-Click Deep Clean complete: Reclaimed {result.FormattedDiskFreed} disk, {result.FormattedRamFreed} RAM ({result.FilesDeleted:N0} files deleted)", LogLevel.Success);

            // Update Telemetry & Hero Cards
            UpdateDriveTelemetry();
            UpdateMemoryTelemetry();

            // Display Celebration Modal
            CelebrationModalTitleText.Text = "⚡ 1-Click Deep Clean Complete!";
            CelebrationReclaimedText.Text = $"Reclaimed {result.FormattedDiskFreed} Disk & {result.FormattedRamFreed} RAM";
            CelebrationFilesText.Text = $"{result.FilesDeleted:N0}";
            CelebrationFoldersText.Text = $"{result.FoldersDeleted:N0}";
            CelebrationRamText.Text = result.FormattedRamFreed;
            CelebrationTimeText.Text = $"{result.Duration.TotalSeconds:0.1}s";
            CelebrationModalOverlay.Visibility = Visibility.Visible;

            // Trigger a background rescan to refresh target cards
            _ = Task.Run(async () =>
            {
                await Task.Delay(800);
                await Dispatcher.InvokeAsync(() => ScanButton_Click(this, new RoutedEventArgs()));
            });
        }
        catch (OperationCanceledException)
        {
            ProgressStatusText.Text = "Deep Clean cancelled.";
            HeroSubtext.Text = "Deep clean cancelled by user.";
            AddLog("1-Click Deep Clean was cancelled by user.", LogLevel.Warning);
        }
        catch (Exception ex)
        {
            ProgressStatusText.Text = "Error during Deep Clean.";
            HeroSubtext.Text = $"Error: {ex.Message}";
            AddLog($"1-Click Deep Clean error: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            _isBusy = false;
            CancelButton.Visibility = Visibility.Collapsed;
            SetControlsEnabled(true);
        }
    }

    private void SelectSafeOnlyButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var target in _targets)
        {
            // Select all 100% Safe caches, leave orphaned app leftovers for user review
            target.IsSelected = !target.IsOrphanedAppFolder;
        }
        RecalculateTotals();
        AddLog("Selected all 🟢 100% Safe Cache categories.", LogLevel.Info);
    }

    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var target in _targets)
        {
            target.IsSelected = true;
        }
        RecalculateTotals();
    }

    private void DeselectAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var target in _targets)
        {
            target.IsSelected = false;
        }
        RecalculateTotals();
    }

    private void TargetCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        RecalculateTotals();
    }

    private void SafeModeCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        bool isSafe = SafeModeCheckBox.IsChecked == true;
        AddLog(isSafe 
            ? "Safety Shield ENABLED: Files created/modified within last 24 hours will be preserved." 
            : "Safety Shield DISABLED: All files in selected categories will be removed.", 
            LogLevel.Info);
    }

    private void InspectTarget_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TargetFolderInfo target)
        {
            _inspectedFiles.Clear();
            foreach (var f in target.TopFiles)
            {
                _inspectedFiles.Add(f);
            }

            InspectorTitleText.Text = $"{target.Name} — Top Junk Files";
            InspectorSubtitleText.Text = $"Showing {target.TopFiles.Count} largest files found in {target.FolderPath}";
            InspectorModalOverlay.Visibility = Visibility.Visible;
        }
    }

    private void CloseInspector_Click(object sender, RoutedEventArgs e)
    {
        InspectorModalOverlay.Visibility = Visibility.Collapsed;
    }

    private void ExportAuditReport_Click(object sender, RoutedEventArgs e)
    {
        var summary = new CleanSummary
        {
            TotalFreedBytes = _sessionTotalFreed,
            TotalFilesDeleted = 0,
            TotalFilesSkipped = 0
        };

        var report = CleanerService.GenerateAuditReport(_targets, summary, SafeModeCheckBox.IsChecked == true);
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var filePath = Path.Combine(desktopPath, $"Deltempo_Audit_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

        try
        {
            File.WriteAllText(filePath, report);
            AddLog($"Audit report saved to: {filePath}", LogLevel.Success);
            MessageBox.Show($"Audit report exported successfully to your Desktop:\n\n{filePath}",
                "Audit Report Exported",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to export report: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RecalculateTotals()
    {
        long selectedBytes = 0;
        int selectedFiles = 0;

        foreach (var target in _targets.Where(t => t.IsSelected))
        {
            selectedBytes += target.SizeBytes;
            selectedFiles += target.FileCount;
        }

        var formattedSize = TargetFolderInfo.FormatBytes(selectedBytes);
        HeroSizeText.Text = formattedSize;
        CleanButtonText.Text = $"Clean Selected ({formattedSize})";

        if (selectedBytes == 0)
        {
            HeroSubtext.Text = "Selected categories are clean or ready for scan";
        }
        else
        {
            HeroSubtext.Text = $"{selectedFiles:N0} junk items selected for precision removal";
        }
    }

    private void SetControlsEnabled(bool enabled)
    {
        QuickScanBtn.IsEnabled = enabled;
        CleanButton.IsEnabled = enabled;
        if (HeroOneClickDeepCleanBtn != null) HeroOneClickDeepCleanBtn.IsEnabled = enabled;
        if (HeroRescanBtn != null) HeroRescanBtn.IsEnabled = enabled;
        if (FooterOneClickBtn != null) FooterOneClickBtn.IsEnabled = enabled;
    }

    private void ToggleLogBtn_Click(object sender, RoutedEventArgs e)
    {
        if (LogDrawerBorder.Visibility == Visibility.Visible)
        {
            LogDrawerBorder.Visibility = Visibility.Collapsed;
            BottomActionDockBorder.CornerRadius = new CornerRadius(0, 0, 19, 19);
            ToggleLogText.Text = "Activity Log";
            ToggleLogIcon.Text = "\uE756";
        }
        else
        {
            LogDrawerBorder.Visibility = Visibility.Visible;
            BottomActionDockBorder.CornerRadius = new CornerRadius(0, 0, 0, 0);
            LogDrawerBorder.CornerRadius = new CornerRadius(0, 0, 19, 19);
            ToggleLogText.Text = "Hide Log";
            ToggleLogIcon.Text = "\uE70D";
            LogScrollViewer.ScrollToEnd();
        }
    }

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

    private async Task CheckForUpdatesInternalAsync(bool silent)
    {
        try
        {
            var release = await UpdateService.CheckForUpdatesAsync();
            if (release != null && release.IsNewer && !string.IsNullOrEmpty(release.DownloadUrl))
            {
                _pendingRelease = release;
                Dispatcher.Invoke(() =>
                {
                    UpdateVersionTagText.Text = release.TagName;
                    if (!string.IsNullOrWhiteSpace(release.Body))
                    {
                        UpdateChangelogText.Text = release.Body;
                    }
                    else
                    {
                        UpdateChangelogText.Text = "• Performance optimizations & precision engine enhancements\n• Direct in-place hot-swap update (zero installer leftovers)";
                    }
                    UpdateProgressContainer.Visibility = Visibility.Collapsed;
                    ApplyUpdateBtn.IsEnabled = true;
                    ApplyUpdateBtn.Content = "Update Now ⚡";
                    UpdateLaterBtn.IsEnabled = true;
                    UpdateModalOverlay.Visibility = Visibility.Visible;
                    SoundService.PlayClickSound();
                    AddLog($"New version available: {release.TagName}", LogLevel.Info);
                });
            }
            else if (!silent)
            {
                Dispatcher.Invoke(() =>
                {
                    ManualCheckStatusText.Text = $"Up to date! (v{UpdateService.CurrentVersion})";
                    MessageBox.Show(
                        $"You are running the latest version of Deltempo (v{UpdateService.CurrentVersion}).\n\nNo updates are currently available.",
                        "Deltempo is Up to Date",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                });
            }
        }
        catch (Exception ex)
        {
            if (!silent)
            {
                Dispatcher.Invoke(() =>
                {
                    ManualCheckStatusText.Text = "Update check failed";
                    MessageBox.Show(
                        $"Unable to check for updates: {ex.Message}",
                        "Update Check Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                });
            }
        }
    }

    private async void ManualCheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        ManualCheckStatusText.Text = "Checking GitHub Releases...";
        ManualCheckUpdateBtn.IsEnabled = false;
        try
        {
            await CheckForUpdatesInternalAsync(silent: false);
        }
        finally
        {
            ManualCheckUpdateBtn.IsEnabled = true;
        }
    }

    private async void ApplyUpdateBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingRelease == null || string.IsNullOrEmpty(_pendingRelease.DownloadUrl))
            return;

        ApplyUpdateBtn.IsEnabled = false;
        UpdateLaterBtn.IsEnabled = false;
        ApplyUpdateBtn.Content = "Updating...";
        UpdateProgressContainer.Visibility = Visibility.Visible;
        UpdateProgressBar.Value = 0;
        UpdatePercentText.Text = "0%";
        UpdateDownloadStatusText.Text = "Streaming update...";

        var progress = new Progress<double>(val =>
        {
            Dispatcher.Invoke(() =>
            {
                UpdateProgressBar.Value = val;
                UpdatePercentText.Text = $"{val:F0}%";
                UpdateDownloadStatusText.Text = $"Downloading update ({val:F0}%)...";
            });
        });

        try
        {
            AddLog($"Starting atomic in-place update to {_pendingRelease.TagName}...", LogLevel.Info);
            await UpdateService.DownloadAndApplyUpdateAsync(_pendingRelease.DownloadUrl, progress);
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateProgressContainer.Visibility = Visibility.Collapsed;
                ApplyUpdateBtn.IsEnabled = true;
                UpdateLaterBtn.IsEnabled = true;
                ApplyUpdateBtn.Content = "Retry Update ⚡";
                MessageBox.Show(
                    $"Update failed: {ex.Message}\n\nYou can manually download the latest version from GitHub Releases.",
                    "Update Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                AddLog($"Update failed: {ex.Message}", LogLevel.Error);
            });
        }
    }

    private void CloseUpdateModal_Click(object sender, RoutedEventArgs e)
    {
        UpdateModalOverlay.Visibility = Visibility.Collapsed;
        SoundService.PlayClickSound();
    }

    // ==========================================
    // ELITE PC PERFORMANCE TOOLKIT HANDLERS
    // ==========================================

    private void UpdateMemoryTelemetry()
    {
        try
        {
            var mem = MemoryOptimizerService.GetMemoryInfo();
            if (HeroRamPercentText != null)
                HeroRamPercentText.Text = $"{mem.UsedPercent:F0}%";
            if (HeroRamDetailText != null)
                HeroRamDetailText.Text = $"{mem.FormattedUsed} / {mem.FormattedTotal} Used";
            if (HeroRamProgressBar != null)
                HeroRamProgressBar.Value = mem.UsedPercent;

            if (MemoryModalOverlay != null && MemoryModalOverlay.Visibility == Visibility.Visible)
            {
                UpdateModalMemoryTelemetry(mem);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }

    private void UpdateModalMemoryTelemetry(MemoryInfo? mem = null)
    {
        try
        {
            mem ??= MemoryOptimizerService.GetMemoryInfo();
            if (ModalTotalRamText != null)
                ModalTotalRamText.Text = mem.FormattedTotal;
            if (ModalUsedRamText != null)
                ModalUsedRamText.Text = $"{mem.FormattedUsed} ({mem.UsedPercent:F0}%) In Use";
            if (ModalStandbyCacheText != null)
                ModalStandbyCacheText.Text = mem.FormattedSystemCache;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Telemetry update suppressed: {ex.Message}");
        }
    }

    private void OpenMemoryCleanerModal_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        MemoryModalOverlay.Visibility = Visibility.Visible;
        RefreshMemoryModalData();
    }

    private void CloseMemoryCleanerModal_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        MemoryModalOverlay.Visibility = Visibility.Collapsed;
        UpdateMemoryTelemetry();
    }

    private void RefreshMemoryModalData()
    {
        try
        {
            UpdateModalMemoryTelemetry();
            var snapshots = MemoryOptimizerService.GetMemoryAreaSnapshots();
            MemoryAreaItemsControl.ItemsSource = snapshots;

            int available = snapshots.Count(s => s.IsAvailableOnThisOs);
            MemoryModalStatusText.Text = $"{snapshots.Count} memory zones · {available} available for privileged NT flush";
        }
        catch (Exception ex)
        {
            AddLog($"Failed to refresh memory zones: {ex.Message}", LogLevel.Warning);
        }
    }

    private void SelectAllMemoryZones_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (MemoryAreaItemsControl.ItemsSource is IEnumerable<MemoryAreaSnapshot> list)
        {
            foreach (var item in list) item.IsSelected = true;
            MemoryAreaItemsControl.Items.Refresh();
        }
    }

    private void DeselectAllMemoryZones_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (MemoryAreaItemsControl.ItemsSource is IEnumerable<MemoryAreaSnapshot> list)
        {
            foreach (var item in list) item.IsSelected = false;
            MemoryAreaItemsControl.Items.Refresh();
        }
    }

    private async void QuickTrimWorkingSets_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        AddLog("⚡ Quick trimming non-whitelisted process working sets...", LogLevel.Info);
        var res = await MemoryOptimizerService.OptimizeAreaAsync(MemoryTargetType.WorkingSet);
        if (res.Success)
        {
            AddLog($"✓ Process Working Sets Trimmed: Reclaimed {res.FormattedFreed} across {res.ProcessesOptimized} tasks.", LogLevel.Success);
            RefreshMemoryModalData();
            UpdateMemoryTelemetry();
        }
        else
        {
            AddLog($"Working sets trim failed: {res.ErrorMessage}", LogLevel.Error);
        }
    }

    private async void PurgeSelectedZones_Click(object sender, RoutedEventArgs e)
    {
        if (MemoryAreaItemsControl.ItemsSource is not IEnumerable<MemoryAreaSnapshot> list)
            return;

        var selected = list.Where(s => s.IsSelected && s.IsAvailableOnThisOs).Select(s => s.Target).ToArray();
        if (selected.Length == 0)
        {
            AddLog("No memory zones selected for purge.", LogLevel.Warning);
            return;
        }

        PurgeSelectedZonesBtn.IsEnabled = false;
        PurgeSelectedZonesBtn.Content = "⚡ Purging NT Cache...";
        SoundService.PlayClickSound();

        try
        {
            var result = await MemoryOptimizerService.OptimizeRamAsync(selected);
            AddLog($"⚡ NT Kernel Memory Clean Complete: Purged {result.FormattedReclaimed} in {result.ExecutionTimeMs}ms across {result.AreaResults.Count} zones.", LogLevel.Success);
            RefreshMemoryModalData();
            UpdateMemoryTelemetry();
        }
        catch (Exception ex)
        {
            AddLog($"Memory purge error: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            PurgeSelectedZonesBtn.IsEnabled = true;
            PurgeSelectedZonesBtn.Content = "⚡ Purge Selected Zones";
        }
    }

    private async void PerAreaBoostButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not MemoryTargetType target)
            return;

        btn.IsEnabled = false;
        btn.Content = "⋯";
        SoundService.PlayClickSound();

        try
        {
            var result = await MemoryOptimizerService.OptimizeAreaAsync(target);
            if (result.Success)
            {
                AddLog($"⚡ Flushed {target}: reclaimed {result.FormattedFreed}.", LogLevel.Success);
                RefreshMemoryModalData();
                UpdateMemoryTelemetry();
            }
            else
            {
                AddLog($"⚠ Failed to flush {target}: {result.ErrorMessage}", LogLevel.Error);
            }
        }
        catch (Exception ex)
        {
            AddLog($"Error flushing {target}: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            btn.IsEnabled = true;
            btn.Content = "⚡ Flush";
        }
    }

    private async void HeroBoostRamBtn_Click(object sender, RoutedEventArgs e)
    {
        HeroBoostRamBtn.IsEnabled = false;
        HeroBoostRamBtn.Content = "⚡ Boosting...";
        SoundService.PlayClickSound();

        try
        {
            var res = await MemoryOptimizerService.OptimizeRamAsync();
            UpdateMemoryTelemetry();
            AddLog($"⚡ RAM Boost Complete: Reclaimed {res.FormattedReclaimed} across {res.ProcessesOptimized} processes in {res.ExecutionTimeMs}ms.", LogLevel.Success);
            HeroBoostRamBtn.Content = $"✓ -{res.FormattedReclaimed}";
            await Task.Delay(1500);
        }
        catch (Exception ex)
        {
            AddLog($"RAM optimization error: {ex.Message}", LogLevel.Warning);
        }
        finally
        {
            HeroBoostRamBtn.IsEnabled = true;
            HeroBoostRamBtn.Content = "⚡ Boost";
        }
    }

    // 1. Startup Accelerator Handlers
    private List<StartupItem> _allStartupItems = new();

    private async void OpenStartupModal_Click(object sender, RoutedEventArgs e)
    {
        StartupModalOverlay.Visibility = Visibility.Visible;
        SoundService.PlayClickSound();
        await ReloadStartupItemsAsync();
    }

    private async Task ReloadStartupItemsAsync()
    {
        StartupStatusText.Text = "Scanning startup entries & Windows registry hives...";
        StartupSearchBox.Text = string.Empty;
        
        _allStartupItems = await StartupManagerService.GetStartupItemsAsync();
        ApplyStartupFilter();
        
        int enabledCount = _allStartupItems.Count(x => x.IsEnabled);
        int disabledCount = _allStartupItems.Count - enabledCount;
        StartupStatusText.Text = $"Found {_allStartupItems.Count} startup programs ({enabledCount} enabled, {disabledCount} disabled)";
    }

    private void RefreshStartup_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        _ = ReloadStartupItemsAsync();
    }

    private void StartupSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        StartupSearchPlaceholder.Visibility = string.IsNullOrWhiteSpace(StartupSearchBox.Text) 
            ? Visibility.Visible 
            : Visibility.Collapsed;
        ApplyStartupFilter();
    }

    private void ApplyStartupFilter()
    {
        if (_allStartupItems == null) return;
        string query = StartupSearchBox.Text.Trim();
        if (string.IsNullOrEmpty(query))
        {
            StartupItemsControl.ItemsSource = _allStartupItems;
        }
        else
        {
            StartupItemsControl.ItemsSource = _allStartupItems.Where(x =>
                x.DisplayTitle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.LocationDisplay.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.Command.Contains(query, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }
    }

    private void CloseStartupModal_Click(object sender, RoutedEventArgs e)
    {
        StartupModalOverlay.Visibility = Visibility.Collapsed;
        SoundService.PlayClickSound();
    }

    private void StartupSwitch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.DataContext is StartupItem item)
        {
            bool isEnabled = cb.IsChecked == true;

            // If item requires Administrator rights and app is running non-elevated:
            if (!ElevationService.IsRunAsAdmin() && (item.Location.Contains("HKLM") || item.Location.Contains("Common")))
            {
                cb.IsChecked = !isEnabled;
                MessageBox.Show(
                    $"Administrator privileges are required to modify system-wide startup app '{item.DisplayTitle}'.\n\nPlease click the 'Admin' button in the top bar to elevate Deltempo.",
                    "Elevation Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                AddLog($"Elevation required to toggle '{item.DisplayTitle}'.", LogLevel.Warning);
                return;
            }

            bool success = StartupManagerService.ToggleStartupItem(item, isEnabled);
            if (success)
            {
                AddLog($"Startup app '{item.DisplayTitle}' is now {(isEnabled ? "ENABLED" : "DISABLED")}.", LogLevel.Info);
                int enabledCount = _allStartupItems.Count(x => x.IsEnabled);
                int disabledCount = _allStartupItems.Count - enabledCount;
                StartupStatusText.Text = $"Found {_allStartupItems.Count} startup programs ({enabledCount} enabled, {disabledCount} disabled)";
            }
            else
            {
                cb.IsChecked = !isEnabled;
                AddLog($"Could not change startup status for '{item.DisplayTitle}'.", LogLevel.Warning);
            }
        }
    }

    // 2. Large File Hunter Handlers
    private CancellationTokenSource? _largeFileScanCts;
    private bool _isLargeFileScanRunning;

    private void PopulateLargeFileDrives()
    {
        if (LargeFileDriveComboBox.Items.Count > 0) return;

        LargeFileDriveComboBox.Items.Add(new ComboBoxItem { Content = "All Drives", Tag = "ALL", IsSelected = true });

        var drives = LargeFileHunterService.GetAvailableDrives();
        foreach (var d in drives)
        {
            LargeFileDriveComboBox.Items.Add(new ComboBoxItem { Content = $"Drive {d}", Tag = d });
        }

        LargeFileDriveComboBox.Items.Add(new ComboBoxItem { Content = "User Profile & OneDrive", Tag = "USER" });
        if (LargeFileCategoryComboBox != null && LargeFileCategoryComboBox.SelectedIndex < 0)
        {
            LargeFileCategoryComboBox.SelectedIndex = 0;
        }
    }

    private void RefreshLargeFileHeroStats()
    {
        if (!_isLoaded || _largeFiles == null) return;
        long totalBytes = _largeFiles.Sum(f => f.SizeBytes);
        int safeCount = _largeFiles.Count(f => f.IsAiSafe);
        long safeBytes = _largeFiles.Where(f => f.IsAiSafe).Sum(f => f.SizeBytes);
        long protectedBytes = Math.Max(0, totalBytes - safeBytes);
        int protectedCount = Math.Max(0, _largeFiles.Count - safeCount);

        if (LargeFilesTotalStorageText != null)
            LargeFilesTotalStorageText.Text = $"{TargetFolderInfo.FormatBytes(totalBytes)} ({_largeFiles.Count} files)";

        if (LargeFilesSafeStorageText != null)
            LargeFilesSafeStorageText.Text = $"{TargetFolderInfo.FormatBytes(safeBytes)} ({safeCount} safe)";

        if (LargeFilesProtectedStorageText != null)
            LargeFilesProtectedStorageText.Text = $"{TargetFolderInfo.FormatBytes(protectedBytes)} ({protectedCount} protected)";
    }

    private async Task RunLargeFileScanAsync()
    {
        if (_isLargeFileScanRunning)
        {
            _largeFileScanCts?.Cancel();
            return;
        }

        _isLargeFileScanRunning = true;
        _largeFileScanCts = new CancellationTokenSource();
        var ct = _largeFileScanCts.Token;

        try
        {
            if (RescanLargeFilesBtn != null) RescanLargeFilesBtn.IsEnabled = false;

            string scope = "ALL";
            if (LargeFileDriveComboBox?.SelectedItem is ComboBoxItem selectedScope && selectedScope.Tag is string scopeTag)
            {
                scope = scopeTag;
            }

            long minBytes = 50L * 1024 * 1024;
            if (LargeFileSizeComboBox?.SelectedItem is ComboBoxItem selectedSize && selectedSize.Tag is string sizeStr && long.TryParse(sizeStr, out long parsedSize))
            {
                minBytes = parsedSize;
            }

            string readableScope = scope == "ALL" ? "all drives" : scope == "USER" ? "user profile" : scope;
            LargeFilesStatusText.Text = $"Scanning {readableScope} for files > {TargetFolderInfo.FormatBytes(minBytes)}...";
            if (LargeFilesEmptyState != null)
            {
                LargeFilesEmptyStateTitle.Text = $"Scanning {readableScope}...";
                LargeFilesEmptyStateHint.Text = "Analyzing drives for large files. This may take a moment.";
                LargeFilesEmptyState.Visibility = Visibility.Visible;
            }

            var progress = new Progress<int>(pct =>
            {
                LargeFilesStatusText.Text = $"Scanning {readableScope}... ({pct}%)";
            });

            var files = await LargeFileHunterService.ScanLargeFilesAsync(minBytes, scope, progress, ct);

            _largeFiles.Clear();
            foreach (var f in files)
            {
                _largeFiles.Add(f);
            }

            var view = CollectionViewSource.GetDefaultView(_largeFiles);
            if (view != null)
            {
                view.Filter = FilterLargeFileItem;
                view.Refresh();
            }

            UpdateLargeFileSelectionSummary();
            RefreshLargeFileHeroStats();

            long totalBytes = files.Sum(f => f.SizeBytes);
            int safeCount = files.Count(f => f.IsAiSafe);
            long safeBytes = files.Where(f => f.IsAiSafe).Sum(f => f.SizeBytes);

            LargeFilesStatusText.Text = $"Discovered {files.Count} files ({TargetFolderInfo.FormatBytes(totalBytes)}). AI verified {safeCount} 100% safe to clean.";

            // Update empty state visibility
            if (LargeFilesEmptyState != null)
            {
                if (files.Count == 0)
                {
                    LargeFilesEmptyStateTitle.Text = "No large files found";
                    LargeFilesEmptyStateHint.Text = "Try a different scope or lower the minimum file size.";
                    LargeFilesEmptyState.Visibility = Visibility.Visible;
                }
                else
                {
                    LargeFilesEmptyState.Visibility = Visibility.Collapsed;
                }
            }
        }
        catch (OperationCanceledException)
        {
            LargeFilesStatusText.Text = "Scan cancelled.";
            if (LargeFilesEmptyState != null)
            {
                LargeFilesEmptyStateTitle.Text = "Scan cancelled";
                LargeFilesEmptyStateHint.Text = "Click Scan Now to run a new scan.";
                LargeFilesEmptyState.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            LargeFilesStatusText.Text = $"Scan completed with warnings: {ex.Message}";
            if (LargeFilesEmptyState != null)
            {
                LargeFilesEmptyStateTitle.Text = "Scan encountered an issue";
                LargeFilesEmptyStateHint.Text = ex.Message;
                LargeFilesEmptyState.Visibility = Visibility.Visible;
            }
        }
        finally
        {
            _isLargeFileScanRunning = false;
            if (RescanLargeFilesBtn != null) RescanLargeFilesBtn.IsEnabled = true;
        }
    }

    private async void OpenLargeFilesModal_Click(object sender, RoutedEventArgs e)
    {
        LargeFilesModalOverlay.Visibility = Visibility.Visible;
        SoundService.PlayClickSound();
        PopulateLargeFileDrives();
        await RunLargeFileScanAsync();
    }

    private async void LargeFileFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || LargeFilesModalOverlay == null) return;
        if (LargeFilesModalOverlay.Visibility == Visibility.Visible && !_isLargeFileScanRunning)
        {
            await RunLargeFileScanAsync();
        }
    }

    private void LargeFileCategoryFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || _largeFiles == null || LargeFileCategoryComboBox == null || LargeFilesSelectedSummaryText == null) return;
        var view = CollectionViewSource.GetDefaultView(_largeFiles);
        if (view != null)
        {
            view.Filter = FilterLargeFileItem;
            view.Refresh();
        }
        UpdateLargeFileSelectionSummary();
    }

    private bool FilterLargeFileItem(object obj)
    {
        if (obj is not LargeFileInfo item) return false;

        if (LargeFileCategoryComboBox?.SelectedItem is ComboBoxItem cItem && cItem.Tag is string tag && tag != "ALL")
        {
            if (tag == "SAFE") return item.IsAiSafe;
            if (tag == "RISK") return !item.IsAiSafe;
            if (!string.Equals(tag, item.Category, StringComparison.OrdinalIgnoreCase)) return false;
        }

        return true;
    }

    private void UpdateLargeFileSelectionSummary()
    {
        if (!_isLoaded || LargeFilesSelectedSummaryText == null || RecycleSelectedLargeFilesBtn == null || _largeFiles == null)
            return;

        int selectedCount = _largeFiles.Count(f => f.IsSelected);
        long selectedBytes = _largeFiles.Where(f => f.IsSelected).Sum(f => f.SizeBytes);

        LargeFilesSelectedSummaryText.Text = $"Selected: {selectedCount} files ({TargetFolderInfo.FormatBytes(selectedBytes)})";
        RecycleSelectedLargeFilesBtn.Content = $"Recycle Selected ({TargetFolderInfo.FormatBytes(selectedBytes)})";
        RecycleSelectedLargeFilesBtn.IsEnabled = selectedCount > 0;
    }

    private void LargeFileItem_CheckChanged(object sender, RoutedEventArgs e)
    {
        UpdateLargeFileSelectionSummary();
    }

    private void SelectSafeLargeFiles_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        foreach (var f in _largeFiles)
        {
            f.IsSelected = f.IsAiSafe;
        }
        UpdateLargeFileSelectionSummary();
    }

    private void SelectAllLargeFiles_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        foreach (var f in _largeFiles)
        {
            f.IsSelected = true;
        }
        UpdateLargeFileSelectionSummary();
    }

    private void ClearLargeFilesSelection_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        foreach (var f in _largeFiles)
        {
            f.IsSelected = false;
        }
        UpdateLargeFileSelectionSummary();
    }

    private void OpenRecycleBin_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        LargeFileHunterService.OpenWindowsRecycleBin();
    }

    private void RecycleSelectedLargeFiles_Click(object sender, RoutedEventArgs e)
    {
        var selected = _largeFiles.Where(f => f.IsSelected).ToList();
        if (selected.Count == 0) return;

        long totalBytes = selected.Sum(f => f.SizeBytes);
        var res = MessageBox.Show(
            $"Safely move {selected.Count} selected files ({TargetFolderInfo.FormatBytes(totalBytes)}) to the Windows Recycle Bin?\n\nThey can be restored anytime from the Recycle Bin with undo.",
            "Recycle Selected Large Files",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (res == MessageBoxResult.Yes)
        {
            var (succ, fail, freed) = LargeFileHunterService.BatchMoveToRecycleBin(selected);
            foreach (var item in selected)
            {
                if (!File.Exists(item.FilePath))
                {
                    _largeFiles.Remove(item);
                }
            }

            UpdateDriveTelemetry(freed);
            UpdateLargeFileSelectionSummary();
            RefreshLargeFileHeroStats();
            AddLog($"Batch Recycled {succ} large files ({TargetFolderInfo.FormatBytes(freed)} freed) to Windows Recycle Bin.", LogLevel.Success);
            LargeFilesStatusText.Text = $"Successfully recycled {succ} files ({TargetFolderInfo.FormatBytes(freed)}) to Recycle Bin";
            SoundService.PlaySuccessSound();
        }
    }

    private async void RescanLargeFilesBtn_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        await RunLargeFileScanAsync();
    }

    private void CloseLargeFilesModal_Click(object sender, RoutedEventArgs e)
    {
        _largeFileScanCts?.Cancel();
        LargeFilesModalOverlay.Visibility = Visibility.Collapsed;
        SoundService.PlayClickSound();
    }

    private void RevealLargeFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is LargeFileInfo info)
        {
            try
            {
                if (File.Exists(info.FilePath))
                {
                    Process.Start("explorer.exe", $"/select,\"{info.FilePath}\"");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        }
    }

    private void RecycleLargeFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is LargeFileInfo info)
        {
            var res = MessageBox.Show(
                $"Send '{info.FileName}' ({info.FormattedSize}) to the Windows Recycle Bin?\n\nAI Verdict: {info.AiVerdict}\n\nThis file can be restored anytime from the Recycle Bin.",
                "Recycle Large File",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                bool ok = LargeFileHunterService.MoveToRecycleBin(info.FilePath);
                if (ok)
                {
                    AddLog($"Moved '{info.FileName}' ({info.FormattedSize}) to Recycle Bin.", LogLevel.Success);
                    _largeFiles.Remove(info);
                    UpdateDriveTelemetry();
                    UpdateLargeFileSelectionSummary();
                    RefreshLargeFileHeroStats();
                }
                else
                {
                    AddLog($"Could not recycle file '{info.FileName}'.", LogLevel.Warning);
                }
            }
        }
    }

    // 3. Process Optimizer Handlers
    private List<ProcessMemoryInfo> _allProcesses = new();

    private async void OpenProcessModal_Click(object sender, RoutedEventArgs e)
    {
        ProcessModalOverlay.Visibility = Visibility.Visible;
        SoundService.PlayClickSound();
        await ReloadProcessesAsync();
    }

    private async Task ReloadProcessesAsync()
    {
        ProcessStatusSummaryText.Text = "Analyzing running background tasks...";
        ProcessSearchBox.Text = string.Empty;
        
        _allProcesses = await ProcessOptimizerService.GetHeavyProcessesAsync(20L * 1024 * 1024);
        ApplyProcessFilter();

        long totalRam = _allProcesses.Sum(x => x.WorkingSetBytes);
        ProcessStatusSummaryText.Text = $"{_allProcesses.Count} active apps ({TargetFolderInfo.FormatBytes(totalRam)} RAM)";
    }

    private void RefreshProcess_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        _ = ReloadProcessesAsync();
    }

    private void ProcessSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ProcessSearchPlaceholder.Visibility = string.IsNullOrWhiteSpace(ProcessSearchBox.Text) 
            ? Visibility.Visible 
            : Visibility.Collapsed;
        ApplyProcessFilter();
    }

    private void ApplyProcessFilter()
    {
        if (_allProcesses == null) return;
        string query = ProcessSearchBox.Text.Trim();
        if (string.IsNullOrEmpty(query))
        {
            ProcessItemsControl.ItemsSource = _allProcesses;
        }
        else
        {
            ProcessItemsControl.ItemsSource = _allProcesses.Where(x =>
                x.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.CategoryDescription.Contains(query, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }
    }

    private void CloseProcessModal_Click(object sender, RoutedEventArgs e)
    {
        ProcessModalOverlay.Visibility = Visibility.Collapsed;
        SoundService.PlayClickSound();
    }

    private void TrimProcessMemory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ProcessMemoryInfo proc)
        {
            var pids = proc.ProcessIds.Count > 0 ? proc.ProcessIds : new List<int> { proc.ProcessId };
            var (ok, freed) = ProcessOptimizerService.TrimProcessMemoryEx(pids);
            if (ok)
            {
                if (freed > 0)
                {
                    AddLog($"⚡ Trimmed '{proc.DisplayName}': Reclaimed {TargetFolderInfo.FormatBytes(freed)} RAM!", LogLevel.Success);
                }
                else
                {
                    AddLog($"⚡ Trimmed working memory for '{proc.DisplayName}'.", LogLevel.Success);
                }
                UpdateMemoryTelemetry();
                _ = ReloadProcessesAsync();
            }
            else
            {
                AddLog($"Notice: '{proc.DisplayName}' working set is already minimal or restricted.", LogLevel.Info);
            }
        }
    }

    private void TerminateProcess_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ProcessMemoryInfo proc)
        {
            var res = MessageBox.Show(
                $"Safely end background task '{proc.DisplayName}'?",
                "End Background Task",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (res == MessageBoxResult.Yes)
            {
                bool ok = ProcessOptimizerService.SafeTerminateProcess(proc.ProcessIds.Count > 0 ? proc.ProcessIds : new List<int> { proc.ProcessId });
                if (ok)
                {
                    AddLog($"Terminated task '{proc.DisplayName}'.", LogLevel.Info);
                    UpdateMemoryTelemetry();
                    _ = ReloadProcessesAsync();
                }
                else
                {
                    AddLog($"Could not terminate task '{proc.DisplayName}' (access restricted or protected).", LogLevel.Warning);
                }
            }
        }
    }

    private async void TrimAllProcesses_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        var res = await MemoryOptimizerService.OptimizeRamAsync();
        AddLog($"⚡ Trimmed background working sets: Reclaimed {res.FormattedReclaimed}.", LogLevel.Success);
        UpdateMemoryTelemetry();
        _ = ReloadProcessesAsync();
    }

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
}
