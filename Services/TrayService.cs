using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public static class TrayService
{
    private static Window? _mainWindow;
    private static Action? _onCleanSafeNow;
    private static Action? _onOpenSettings;
    private static Func<Task<MemoryOptimizationResult>>? _onOptimizeRam;
    private static Func<Task<MemoryOptimizationResult>>? _onPurgeStandby;
    private static Action? _onCheckUpdates;
    private static HwndSource? _hwndSource;
    private static bool _isInitialized;
    private static IntPtr _hIcon = IntPtr.Zero;
    private static int _wmTaskbarCreated;
    private static DateTime _lastHoverTime = DateTime.MinValue;

    private const int WM_USER = 0x0400;
    private const int WM_TRAYICON = WM_USER + 101;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_CONTEXTMENU = 0x007B;
    private const int WM_NULL = 0x0000;

    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;
    private const int NIM_SETVERSION = 0x00000004;

    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;
    private const int NIF_INFO = 0x00000010;

    private const int NIIF_INFO = 0x00000001;
    private const int NIIF_USER = 0x00000004;
    private const int NIIF_LARGE_ICON = 0x00000020;

    private const int SM_CXSMICON = 49;
    private const int SM_CYSMICON = 50;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int dwTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIconW(int dwMessage, ref NOTIFYICONDATA lpdata);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateIconFromResourceEx(
        byte[] pbIconBits,
        uint cbIconBits,
        bool fIcon,
        uint dwVersion,
        int cxDesired,
        int cyDesired,
        uint uFlags);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int ExtractIconExW(string lpszFile, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, int nIcons);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegisterWindowMessage(string lpString);

    public static void Initialize(
        Window mainWindow,
        Action onCleanSafeNow,
        Action onOpenSettings,
        Func<Task<MemoryOptimizationResult>>? onOptimizeRam = null,
        Func<Task<MemoryOptimizationResult>>? onPurgeStandby = null,
        Action? onCheckUpdates = null)
    {
        _mainWindow = mainWindow;
        _onCleanSafeNow = onCleanSafeNow;
        _onOpenSettings = onOpenSettings;
        _onOptimizeRam = onOptimizeRam;
        _onPurgeStandby = onPurgeStandby;
        _onCheckUpdates = onCheckUpdates;

        var helper = new WindowInteropHelper(_mainWindow);
        var hWnd = helper.Handle;

        _hwndSource = HwndSource.FromHwnd(hWnd);
        _hwndSource?.AddHook(WndProc);

        _wmTaskbarCreated = RegisterWindowMessage("TaskbarCreated");

        // Load crisp, true-alpha, DPI-native tray icon
        _hIcon = LoadCrispTrayIcon();

        CreateTrayIcon(hWnd);
        _isInitialized = true;
    }

    private static void CreateTrayIcon(IntPtr hWnd)
    {
        var nid = CreateNotifyData(hWnd);
        nid.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
        nid.szTip = GetFormattedTooltip();
        Shell_NotifyIconW(NIM_ADD, ref nid);
    }

    private static IntPtr LoadCrispTrayIcon()
    {
        // 1. Determine optimal icon size for current system scaling/DPI
        int cx = GetSystemMetrics(SM_CXSMICON);
        int cy = GetSystemMetrics(SM_CYSMICON);
        if (cx <= 0) cx = 16;
        if (cy <= 0) cy = 16;

        byte[]? icoBytes = null;

        // 2. Extract ICO bytes from application pack resource
        try
        {
            var iconUri = new Uri("pack://application:,,,/app.ico", UriKind.Absolute);
            var streamInfo = Application.GetResourceStream(iconUri);
            if (streamInfo != null)
            {
                using var ms = new MemoryStream();
                streamInfo.Stream.CopyTo(ms);
                icoBytes = ms.ToArray();
            }
        }
        catch { }

        // 3. Fallback: Load directly from file if running unbundled
        if (icoBytes == null || icoBytes.Length == 0)
        {
            try
            {
                string iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
                if (File.Exists(iconPath))
                {
                    icoBytes = File.ReadAllBytes(iconPath);
                }
            }
            catch { }
        }

        // 4. Parse ICO directory and extract the best matching 32-bit PNG/DIB frame
        if (icoBytes != null && icoBytes.Length > 22)
        {
            try
            {
                ushort count = BitConverter.ToUInt16(icoBytes, 4);
                int bestIdx = -1;
                int bestDiff = int.MaxValue;

                for (int i = 0; i < count; i++)
                {
                    int offset = 6 + i * 16;
                    int w = icoBytes[offset] == 0 ? 256 : icoBytes[offset];
                    int h = icoBytes[offset + 1] == 0 ? 256 : icoBytes[offset + 1];
                    int diff = Math.Abs(w - cx);
                    if (diff < bestDiff)
                    {
                        bestDiff = diff;
                        bestIdx = i;
                    }
                }

                if (bestIdx >= 0)
                {
                    int entryOffset = 6 + bestIdx * 16;
                    uint bytesInRes = BitConverter.ToUInt32(icoBytes, entryOffset + 8);
                    uint imageOffset = BitConverter.ToUInt32(icoBytes, entryOffset + 12);

                    if (imageOffset + bytesInRes <= icoBytes.Length)
                    {
                        byte[] frameBytes = new byte[bytesInRes];
                        Buffer.BlockCopy(icoBytes, (int)imageOffset, frameBytes, 0, (int)bytesInRes);

                        IntPtr hIcon = CreateIconFromResourceEx(frameBytes, (uint)frameBytes.Length, true, 0x00030000, cx, cy, 0);
                        if (hIcon != IntPtr.Zero)
                        {
                            return hIcon;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Frame extraction failed: {ex.Message}");
            }
        }

        // 5. Fallback: Extract small icon from process executable PE
        try
        {
            string exePath = Environment.ProcessPath ?? "";
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                ExtractIconExW(exePath, 0, out _, out IntPtr hSmall, 1);
                if (hSmall != IntPtr.Zero)
                {
                    return hSmall;
                }
            }
        }
        catch { }

        return IntPtr.Zero;
    }

    private static string GetFormattedTooltip()
    {
        try
        {
            var mem = MemoryOptimizerService.GetMemoryInfo();
            string tip = $"Deltempo Guardian\nRAM: {mem.UsedPercent:0.0}% ({mem.FormattedUsed} / {mem.FormattedTotal})\nStatus: Active & Protected";
            return tip.Length > 120 ? tip[..120] : tip;
        }
        catch
        {
            return "Deltempo - Active Guardian";
        }
    }

    public static void UpdateTooltip()
    {
        if (!_isInitialized || _mainWindow == null) return;
        var helper = new WindowInteropHelper(_mainWindow);
        var nid = CreateNotifyData(helper.Handle);
        nid.uFlags = NIF_TIP;
        nid.szTip = GetFormattedTooltip();
        Shell_NotifyIconW(NIM_MODIFY, ref nid);
    }

    private static NOTIFYICONDATA CreateNotifyData(IntPtr hWnd)
    {
        return new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = hWnd,
            uID = 1001,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _hIcon
        };
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Handle taskbar recreation (e.g. explorer.exe restart)
        if (_wmTaskbarCreated != 0 && msg == _wmTaskbarCreated)
        {
            CreateTrayIcon(hwnd);
            handled = true;
            return IntPtr.Zero;
        }

        if (msg == WM_TRAYICON)
        {
            int eventId = lParam.ToInt32();

            switch (eventId)
            {
                case WM_LBUTTONUP:
                case WM_LBUTTONDBLCLK:
                    RestoreMainWindow();
                    handled = true;
                    break;

                case WM_RBUTTONUP:
                case WM_CONTEXTMENU:
                    ShowLuxuryContextMenu(hwnd);
                    handled = true;
                    break;

                case WM_MOUSEMOVE:
                    // Throttled live tooltip update on hover
                    if ((DateTime.UtcNow - _lastHoverTime).TotalSeconds >= 3)
                    {
                        _lastHoverTime = DateTime.UtcNow;
                        UpdateTooltip();
                    }
                    break;
            }
        }

        return IntPtr.Zero;
    }

    private static void ShowLuxuryContextMenu(IntPtr hWnd)
    {
        if (_mainWindow == null) return;

        _mainWindow.Dispatcher.Invoke(() =>
        {
            var menu = new ContextMenu
            {
                Style = Application.Current.TryFindResource("LuxuryTrayContextMenu") as Style,
                Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint
            };

            var mem = MemoryOptimizerService.GetMemoryInfo();

            // 1. Header Telemetry Card
            var headerBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111724")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E2A3F")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(2, 2, 2, 6)
            };

            var headerGrid = new Grid();
            headerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            headerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            headerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titleGrid = new Grid();
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconBorder = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(5),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10242B")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00E5FF")),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 7, 0)
            };
            iconBorder.Child = new TextBlock
            {
                Text = "\uEA86",
                FontFamily = Application.Current.TryFindResource("IconFont") as FontFamily,
                FontSize = 10.5,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00E5FF")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(iconBorder, 0);
            titleGrid.Children.Add(iconBorder);

            var titleText = new TextBlock
            {
                Text = "Deltempo Guardian",
                FontFamily = Application.Current.TryFindResource("AppFont") as FontFamily,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC")),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleText, 1);
            titleGrid.Children.Add(titleText);

            var activeBadge = new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10241B")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5, 1, 5, 1)
            };
            activeBadge.Child = new TextBlock
            {
                Text = "ACTIVE",
                FontSize = 8.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"))
            };
            Grid.SetColumn(activeBadge, 2);
            titleGrid.Children.Add(activeBadge);

            Grid.SetRow(titleGrid, 0);
            headerGrid.Children.Add(titleGrid);

            var ramPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 4) };
            ramPanel.Children.Add(new TextBlock
            {
                Text = "RAM Pressure: ",
                FontSize = 10.5,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"))
            });
            ramPanel.Children.Add(new TextBlock
            {
                Text = $"{mem.UsedPercent:0.0}%",
                FontWeight = FontWeights.Bold,
                FontSize = 10.5,
                Foreground = mem.UsedPercent > 80
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00E5FF"))
            });
            ramPanel.Children.Add(new TextBlock
            {
                Text = $" ({mem.FormattedUsed} / {mem.FormattedTotal})",
                FontSize = 10,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")),
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetRow(ramPanel, 1);
            headerGrid.Children.Add(ramPanel);

            var pBarBorder = new Border
            {
                Height = 4,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B")),
                ClipToBounds = true
            };
            var pBar = new ProgressBar
            {
                Height = 4,
                Minimum = 0,
                Maximum = 100,
                Value = mem.UsedPercent,
                Foreground = (Brush)Application.Current.FindResource("BrandHeroGradientBrush"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            pBarBorder.Child = pBar;
            Grid.SetRow(pBarBorder, 2);
            headerGrid.Children.Add(pBarBorder);

            headerBorder.Child = headerGrid;
            menu.Items.Add(headerBorder);

            // 2. Open Dashboard
            var openItem = CreateMenuItem("\uE80F", "Open Deltempo Dashboard", () => RestoreMainWindow(), new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8")));
            menu.Items.Add(openItem);

            // 3. Boost RAM (Working Sets)
            var boostItem = CreateMenuItem("\uE768", "Boost RAM (Flush Working Sets)", async () =>
            {
                if (_onOptimizeRam != null)
                {
                    var res = await _onOptimizeRam();
                    ShowNotification("RAM Engine Optimization", $"Reclaimed {res.FormattedReclaimed} across {res.ProcessesOptimized} tasks in {res.ExecutionTimeMs}ms!");
                    UpdateTooltip();
                }
            }, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00E5FF")));
            menu.Items.Add(boostItem);

            // 4. Purge Standby Memory (Kernel)
            var purgeItem = CreateMenuItem("\uEA86", "Purge Standby Memory (Kernel)", async () =>
            {
                if (_onPurgeStandby != null)
                {
                    var res = await _onPurgeStandby();
                    ShowNotification("Standby List Purged", $"Successfully flushed cached standby pages ({res.FormattedReclaimed} reclaimed) in {res.ExecutionTimeMs}ms!");
                    UpdateTooltip();
                }
            }, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A78BFA")));
            menu.Items.Add(purgeItem);

            // 5. Quick Clean Caches
            var cleanItem = CreateMenuItem("\uE74D", "Clean 100% Safe Caches Now", () => _onCleanSafeNow?.Invoke(), new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")));
            menu.Items.Add(cleanItem);

            // 6. Check for Updates
            var updateItem = CreateMenuItem("\uE895", "Check for Updates...", () => _onCheckUpdates?.Invoke(), new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")));
            menu.Items.Add(updateItem);

            // 7. Settings
            var settingsItem = CreateMenuItem("\uE713", "Settings & Preferences", () =>
            {
                RestoreMainWindow();
                _onOpenSettings?.Invoke();
            }, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")));
            menu.Items.Add(settingsItem);

            // Separator
            var sep = new Separator
            {
                Style = Application.Current.TryFindResource("LuxuryTraySeparator") as Style
            };
            menu.Items.Add(sep);

            // 8. Exit
            var exitItem = CreateMenuItem("\uE711", "Exit Deltempo", () =>
            {
                Dispose();
                Application.Current.Shutdown();
            }, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171")));
            menu.Items.Add(exitItem);

            // Proper foreground and dismissal handling
            menu.Closed += (s, e) =>
            {
                PostMessage(hWnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);
            };

            SetForegroundWindow(hWnd);
            menu.IsOpen = true;
        });
    }

    private static MenuItem CreateMenuItem(string iconGlyph, string title, Action onClick, Brush iconBrush)
    {
        var item = CreateMenuItemBase(iconGlyph, title, iconBrush);
        item.Click += (s, e) =>
        {
            try
            {
                onClick();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Tray menu action error: {ex.Message}");
            }
        };
        return item;
    }

    private static MenuItem CreateMenuItem(string iconGlyph, string title, Func<Task> onClickAsync, Brush iconBrush)
    {
        var item = CreateMenuItemBase(iconGlyph, title, iconBrush);
        item.Click += async (s, e) =>
        {
            try
            {
                await onClickAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Tray menu async action error: {ex.Message}");
            }
        };
        return item;
    }

    private static MenuItem CreateMenuItemBase(string iconGlyph, string title, Brush iconBrush)
    {
        var item = new MenuItem
        {
            Style = Application.Current.TryFindResource("LuxuryTrayMenuItem") as Style,
            Header = new TextBlock
            {
                Text = title,
                FontSize = 11.5,
                FontFamily = Application.Current.TryFindResource("AppFont") as FontFamily,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC")),
                VerticalAlignment = VerticalAlignment.Center
            },
            Icon = new TextBlock
            {
                Text = iconGlyph,
                FontFamily = Application.Current.TryFindResource("IconFont") as FontFamily,
                FontSize = 12,
                Foreground = iconBrush,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };

        return item;
    }

    public static void MinimizeToTray()
    {
        if (_mainWindow == null) return;
        _mainWindow.Hide();
        if (SettingsService.Current.AutoCleanNotify)
        {
            ShowNotification("Deltempo Running in Background", "Standing guard to protect your disk space and memory. Click tray icon to restore.");
        }
    }

    public static void RestoreMainWindow()
    {
        if (_mainWindow == null) return;
        _mainWindow.Dispatcher.Invoke(() =>
        {
            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }
            _mainWindow.Show();
            _mainWindow.Activate();
            _mainWindow.Topmost = true;
            _mainWindow.Topmost = false;
            _mainWindow.Focus();
        });
    }

    public static void ShowNotification(string title, string message)
    {
        if (!_isInitialized || _mainWindow == null) return;
        var helper = new WindowInteropHelper(_mainWindow);
        var nid = CreateNotifyData(helper.Handle);
        nid.uFlags = NIF_INFO | NIF_ICON;
        nid.szInfoTitle = title.Length > 63 ? title[..63] : title;
        nid.szInfo = message.Length > 255 ? message[..255] : message;
        nid.dwInfoFlags = NIIF_INFO;
        nid.dwTimeoutOrVersion = 3000;
        Shell_NotifyIconW(NIM_MODIFY, ref nid);
    }

    private static DateTime _lastLowDiskAlertTime = DateTime.MinValue;

    public static void CheckLowDiskSpaceAndNotify(DriveTelemetryInfo telemetry)
    {
        if (!SettingsService.Current.LowDiskAlertEnabled) return;

        double freeGb = (double)telemetry.FreeBytes / (1024 * 1024 * 1024);
        if (freeGb < SettingsService.Current.LowDiskAlertThresholdGb)
        {
            if ((DateTime.UtcNow - _lastLowDiskAlertTime).TotalHours >= 2)
            {
                _lastLowDiskAlertTime = DateTime.UtcNow;
                ShowNotification(
                    "Low Disk Space Alert",
                    $"System drive {telemetry.DriveLetter} has only {freeGb:F1} GB free space remaining (Threshold: {SettingsService.Current.LowDiskAlertThresholdGb:F0} GB). Open Deltempo to clean caches."
                );
            }
        }
    }

    public static void Dispose()
    {
        if (!_isInitialized || _mainWindow == null) return;
        var helper = new WindowInteropHelper(_mainWindow);
        var nid = CreateNotifyData(helper.Handle);
        Shell_NotifyIconW(NIM_DELETE, ref nid);
        if (_hIcon != IntPtr.Zero)
        {
            DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }
        _isInitialized = false;
    }
}
