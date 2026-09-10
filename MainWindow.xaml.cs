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

    // ─── Global hotkey engine ─────────────────────────────────────────────
    private int _hotkeyIdAtom = HOTKEY_ID_MEMORY_BOOST;
    private bool _isHotkeyRegistered;

    public MainWindow()
    {
        InitializeComponent();
        SystemRepairModalOverlay.LogRequested += AddLog;
        MemoryModalOverlay.LogRequested += AddLog;
        MemoryModalOverlay.TelemetryRefreshRequested += UpdateMemoryTelemetry;

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
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern uint ExtractIconEx(string szFileName, int nIconIndex, IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);

    private const uint MSGFLT_ADD = 1;
    private const uint WM_SETICON = 0x0080;
    private const int WM_HOTKEY = 0x0312;
    private const int MOD_CONTROL = 0x0002;
    private const int MOD_SHIFT = 0x0004;
    private const int MOD_NOREPEAT = 0x4000;
    private const int HOTKEY_ID_MEMORY_BOOST = 1;
    private const int VK_M = 0x4D;
    private const IntPtr ICON_SMALL = 0;
    private const IntPtr ICON_BIG = (IntPtr)1;

    private const int SM_CXSMICON = 49;
    private const int SM_CYSMICON = 50;
    private const int SM_CXICON = 11;
    private const int SM_CYICON = 12;

    private const int WM_NCHITTEST = 0x0084;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;

    private System.Drawing.Icon? _nativeIconSmall;
    private System.Drawing.Icon? _nativeIconBig;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            SetupNativeWindowIcons(source.Handle);

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

            // Register global hotkey (Ctrl+Shift+M) if not already running minimized
            if (!SettingsService.Current.MemoryCompactMode)
            {
                try
                {
                    var hwnd = source.Handle;
                    if (RegisterHotKey(hwnd, _hotkeyIdAtom, (uint)(MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT), VK_M))
                    {
                        _isHotkeyRegistered = true;
                    }
                    else
                    {
                        System.Diagnostics.Trace.WriteLine($"[Deltempo] Global hotkey registration failed: {Marshal.GetLastWin32Error()}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[Deltempo] Global hotkey registration suppressed: {ex.Message}");
                }
            }
        }
    }

    private void SetupNativeWindowIcons(IntPtr hWnd)
    {
        try
        {
            var iconUri = new Uri("pack://application:,,,/app.ico", UriKind.Absolute);
            Icon = BitmapFrame.Create(iconUri);

            int smallWidth = GetSystemMetrics(SM_CXSMICON);
            int smallHeight = GetSystemMetrics(SM_CYSMICON);
            int bigWidth = GetSystemMetrics(SM_CXICON);
            int bigHeight = GetSystemMetrics(SM_CYICON);

            if (smallWidth <= 0) smallWidth = 16;
            if (smallHeight <= 0) smallHeight = 16;
            if (bigWidth <= 0) bigWidth = 32;
            if (bigHeight <= 0) bigHeight = 32;

            bool iconsApplied = false;

            var resInfo = System.Windows.Application.GetResourceStream(iconUri);
            if (resInfo != null)
            {
                using (var resStream = resInfo.Stream)
                using (var ms = new MemoryStream())
                {
                    resStream.CopyTo(ms);

                    ms.Seek(0, SeekOrigin.Begin);
                    _nativeIconSmall = new System.Drawing.Icon(ms, new System.Drawing.Size(smallWidth, smallHeight));

                    ms.Seek(0, SeekOrigin.Begin);
                    _nativeIconBig = new System.Drawing.Icon(ms, new System.Drawing.Size(bigWidth, bigHeight));

                    if (_nativeIconSmall.Handle != IntPtr.Zero && _nativeIconBig.Handle != IntPtr.Zero)
                    {
                        SendMessage(hWnd, WM_SETICON, ICON_SMALL, _nativeIconSmall.Handle);
                        SendMessage(hWnd, WM_SETICON, ICON_BIG, _nativeIconBig.Handle);
                        iconsApplied = true;
                    }
                }
            }

            if (!iconsApplied)
            {
                string exePath = Environment.ProcessPath ?? "";
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                {
                    exePath = Path.Combine(AppContext.BaseDirectory, "Deltempo.exe");
                }

                if (File.Exists(exePath))
                {
                    var largeIcons = new IntPtr[1];
                    var smallIcons = new IntPtr[1];
                    uint count = ExtractIconEx(exePath, 0, largeIcons, smallIcons, 1);
                    if (count > 0)
                    {
                        if (smallIcons[0] != IntPtr.Zero)
                        {
                            SendMessage(hWnd, WM_SETICON, ICON_SMALL, smallIcons[0]);
                        }
                        if (largeIcons[0] != IntPtr.Zero)
                        {
                            SendMessage(hWnd, WM_SETICON, ICON_BIG, largeIcons[0]);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Failed to set native window icons: {ex.Message}");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        try
        {
            if (_isHotkeyRegistered && PresentationSource.FromVisual(this) is HwndSource source)
            {
                UnregisterHotKey(source.Handle, _hotkeyIdAtom);
                _isHotkeyRegistered = false;
            }
            _nativeIconSmall?.Dispose();
            _nativeIconBig?.Dispose();
        }
        catch
        {
        }
        base.OnClosed(e);
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
        else if (msg == WM_HOTKEY && wParam.ToInt32() == _hotkeyIdAtom)
        {
            Dispatcher.Invoke(async () =>
            {
                var res = await MemoryOptimizerService.OptimizeRamAsync();
                UpdateMemoryTelemetry();
                AddLog($"[Hotkey Ctrl+Shift+M] Instant RAM optimization complete: Purged {res.FormattedReclaimed} in {res.ExecutionTimeMs}ms.", LogLevel.Success);
            });
            handled = true;
        }
        else if (_isLoaded && msg == WM_NCHITTEST && WindowState == WindowState.Normal)
        {
            try
            {
                int x = (short)(lParam.ToInt32() & 0xFFFF);
                int y = (short)((lParam.ToInt32() >> 16) & 0xFFFF);
                Point pt = PointFromScreen(new Point(x, y));

                int border = 8;
                bool left = pt.X <= border;
                bool right = pt.X >= ActualWidth - border;
                bool top = pt.Y <= border;
                bool bottom = pt.Y >= ActualHeight - border;

                if (top && left) { handled = true; return (IntPtr)HTTOPLEFT; }
                if (top && right) { handled = true; return (IntPtr)HTTOPRIGHT; }
                if (bottom && left) { handled = true; return (IntPtr)HTBOTTOMLEFT; }
                if (bottom && right) { handled = true; return (IntPtr)HTBOTTOMRIGHT; }
                if (left) { handled = true; return (IntPtr)HTLEFT; }
                if (right) { handled = true; return (IntPtr)HTRIGHT; }
                if (top) { handled = true; return (IntPtr)HTTOP; }
                if (bottom) { handled = true; return (IntPtr)HTBOTTOM; }
            }
            catch
            {
            }
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
                AddLog($"[RAM Engine] Reclaimed {res.FormattedReclaimed} across {res.ProcessesOptimized} tasks in {res.ExecutionTimeMs}ms.", LogLevel.Success);
                return res;
            }),
            () => Dispatcher.Invoke(async () =>
            {
                var res = await MemoryOptimizerService.OptimizeRamAsync(new[] { MemoryTargetType.StandbyList, MemoryTargetType.StandbyListLowPriority });
                UpdateMemoryTelemetry();
                AddLog($"[RAM Engine] Purged standby list: reclaimed {res.FormattedReclaimed} in {res.ExecutionTimeMs}ms.", LogLevel.Success);
                return res;
            }),
            () => Dispatcher.Invoke(async () =>
            {
                await CheckForUpdatesInternalAsync(silent: false);
            }));

        AutoCleanService.Start();
        AppVersionHeaderBadge.Text = BuildInfo.VersionWithPatchDisplay;
        AppVersionHeaderBadge.ToolTip = $"Commit: {BuildInfo.CommitSha}\nBuilt: {BuildInfo.BuildDateUtc:yyyy-MM-dd HH:mm} UTC";
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

        // Load orphaned app leftovers asynchronously after initial scan completes
        try
        {
            var orphans = await CleanerService.LoadOrphanedTargetsAsync(_cts?.Token ?? default);
            foreach (var o in orphans)
            {
                _targets.Add(o);
            }
            RecalculateTotals();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }

    private void LoadSettingsIntoUI()
    {
        // General & Audio
        SettingsAutoPilotCheckBox.IsChecked = SettingsService.Current.EnableAutoPilot;
        SettingsTrayCheckBox.IsChecked = SettingsService.Current.MinimizeToTray;
        SettingsNotifyCheckBox.IsChecked = SettingsService.Current.AutoCleanNotify;
        SettingsSoundCheckBox.IsChecked = SettingsService.Current.SoundEnabled;

        // Safety & Disk
        SettingsRecycleBinCheckBox.IsChecked = SettingsService.Current.SendToRecycleBin;
        SettingsLowDiskAlertCheckBox.IsChecked = SettingsService.Current.LowDiskAlertEnabled;

        foreach (ComboBoxItem candidate in SettingsDiskThresholdComboBox.Items)
        {
            if (candidate.Tag is string dtag && int.TryParse(dtag, out int dval) && dval == SettingsService.Current.LowDiskAlertThresholdGb)
            {
                SettingsDiskThresholdComboBox.SelectedItem = candidate;
                break;
            }
        }

        // Updates & Release Engine
        SettingsCheckUpdatesCheckBox.IsChecked = SettingsService.Current.CheckUpdatesOnStartup;
        SettingsAutoDownloadCheckBox.IsChecked = SettingsService.Current.AutoDownloadUpdates;
        SettingsVersionText.Text = BuildInfo.VersionWithPatchDisplay;
        ManualCheckStatusText.Text = $"Official Release: {BuildInfo.VersionWithPatchDisplay}";
        SettingsLastCheckedText.Text = string.IsNullOrEmpty(SettingsService.Current.LastUpdateCheckTimestamp)
            ? "Last checked: Never"
            : $"Last checked: {SettingsService.Current.LastUpdateCheckTimestamp}";

        foreach (ComboBoxItem uc in SettingsUpdateChannelComboBox.Items)
        {
            if (uc.Tag is string uct && string.Equals(uct, SettingsService.Current.UpdateChannel, StringComparison.OrdinalIgnoreCase))
            {
                SettingsUpdateChannelComboBox.SelectedItem = uc;
                break;
            }
        }

        foreach (ComboBoxItem uf in SettingsUpdateFrequencyComboBox.Items)
        {
            if (uf.Tag is string uft && int.TryParse(uft, out int ufval) && ufval == SettingsService.Current.UpdateCheckFrequencyDays)
            {
                SettingsUpdateFrequencyComboBox.SelectedItem = uf;
                break;
            }
        }

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

        // AI & Online Intelligence
        SettingsEnableAiCheckBox.IsChecked = SettingsService.Current.EnableOnlineAiSafety;
        foreach (ComboBoxItem pItem in SettingsAiProviderComboBox.Items)
        {
            if (pItem.Tag is string pTag && string.Equals(pTag, SettingsService.Current.AiProvider, StringComparison.OrdinalIgnoreCase))
            {
                SettingsAiProviderComboBox.SelectedItem = pItem;
                break;
            }
        }
        SettingsAiApiKeyPasswordBox.Password = SettingsService.Current.AiApiKey;
        SettingsAiModelBox.Text = SettingsService.Current.AiModelName;
        SettingsAiOllamaEndpointBox.Text = string.IsNullOrWhiteSpace(SettingsService.Current.AiOllamaEndpoint)
            ? "http://localhost:11434"
            : SettingsService.Current.AiOllamaEndpoint;
        UpdateAiSettingsUiVisibility();
        SettingsAiTestStatusText.Text = $"Ready. Cached local AI reports: {OnlineFileIntelligenceService.GetCacheCount()} files.";

        ApplyMemorySettingsToWindow();
    }


    private void CheckAdminPrivileges()
    {
        _isAdmin = ElevationService.IsRunAsAdmin();
        if (_isAdmin)
        {
            AdminBadgeText.Text = "Elevated";
            AdminBadgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));
            AdminBadgeIcon.Text = "\uE73E";
            AdminBadgeIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            AdminBadgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#142B20"));
            AdminBadgeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3D10B981"));
            AdminElevationButton.ToolTip = "Administrator privileges are active. Full system cleanup and optimization enabled.";
            AddLog("Running with Administrator privileges (Full access to all system locations)", LogLevel.Success);
        }
        else
        {
            AdminBadgeText.Text = "Standard User";
            AdminBadgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FBBF24"));
            AdminBadgeIcon.Text = "\uE7EF";
            AdminBadgeIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
            AdminBadgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#281C0E"));
            AdminBadgeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4DF59E0B"));
            AdminElevationButton.ToolTip = "Running as Standard User. Click to relaunch as Administrator for full system access.";
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

        TrayService.CheckLowDiskSpaceAndNotify(telemetry);
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
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            if (MaximizeButton != null)
            {
                MaximizeButton.Content = "\uE923"; // Restore icon
                MaximizeButton.ToolTip = "Restore Window";
            }
            if (MasterShellBorder != null)
            {
                MasterShellBorder.Margin = new Thickness(0);
                MasterShellBorder.CornerRadius = new CornerRadius(0);
            }
        }
        else
        {
            if (MaximizeButton != null)
            {
                MaximizeButton.Content = "\uE922"; // Maximize icon
                MaximizeButton.ToolTip = "Maximize Window";
            }
            if (MasterShellBorder != null)
            {
                MasterShellBorder.Margin = new Thickness(12);
                MasterShellBorder.CornerRadius = new CornerRadius(20);
            }
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
            else if (MemoryModalOverlay.Visibility == Visibility.Visible)
            {
                CloseMemoryCleanerModal_Click(sender, e);
                e.Handled = true;
            }
            else if (SystemRepairModalOverlay.Visibility == Visibility.Visible)
            {
                CloseSystemRepairModal_Click(sender, e);
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

}
