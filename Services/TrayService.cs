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
    private static HwndSource? _hwndSource;
    private static bool _isInitialized;
    private static IntPtr _hIcon = IntPtr.Zero;

    private const int WM_USER = 0x0400;
    private const int WM_TRAYICON = WM_USER + 101;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;
    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;
    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;
    private const int NIF_INFO = 0x00000010;
    private const int NIIF_INFO = 0x00000001;

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

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractIconW(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static void Initialize(
        Window mainWindow,
        Action onCleanSafeNow,
        Action onOpenSettings,
        Func<Task<MemoryOptimizationResult>>? onOptimizeRam = null)
    {
        _mainWindow = mainWindow;
        _onCleanSafeNow = onCleanSafeNow;
        _onOpenSettings = onOpenSettings;
        _onOptimizeRam = onOptimizeRam;

        var helper = new WindowInteropHelper(_mainWindow);
        var hWnd = helper.Handle;

        _hwndSource = HwndSource.FromHwnd(hWnd);
        _hwndSource?.AddHook(WndProc);

        try
        {
            var iconUri = new Uri("pack://application:,,,/app_icon.png", UriKind.Absolute);
            var streamInfo = Application.GetResourceStream(iconUri);
            if (streamInfo != null)
            {
                using var stream = streamInfo.Stream;
                using var bmp = new System.Drawing.Bitmap(stream);
                _hIcon = bmp.GetHicon();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }

        if (_hIcon == IntPtr.Zero)
        {
            try
            {
                var icoUri = new Uri("pack://application:,,,/app.ico", UriKind.Absolute);
                var streamInfo = Application.GetResourceStream(icoUri);
                if (streamInfo != null)
                {
                    using var stream = streamInfo.Stream;
                    using var ico = new System.Drawing.Icon(stream, 16, 16);
                    using var bmp = ico.ToBitmap();
                    _hIcon = bmp.GetHicon();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        }

        if (_hIcon == IntPtr.Zero)
        {
            try
            {
                string exePath = Environment.ProcessPath ?? "";
                _hIcon = ExtractIconW(IntPtr.Zero, exePath, 0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        }

        var nid = CreateNotifyData(hWnd);
        nid.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
        nid.szTip = GetFormattedTooltip();
        Shell_NotifyIconW(NIM_ADD, ref nid);
        _isInitialized = true;
    }

    private static string GetFormattedTooltip()
    {
        try
        {
            var mem = MemoryOptimizerService.GetMemoryInfo();
            string tip = $"Deltempo King\nRAM: {mem.UsedPercent:0.0}% ({mem.FormattedUsed} / {mem.FormattedTotal})";
            return tip.Length > 120 ? tip.Substring(0, 120) : tip;
        }
        catch
        {
            return "Deltempo — Pure Precision Windows Cleaner";
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
            cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
            hWnd = hWnd,
            uID = 1001,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _hIcon
        };
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAYICON)
        {
            int eventId = lParam.ToInt32();
            if (eventId == WM_LBUTTONDBLCLK)
            {
                RestoreMainWindow();
                handled = true;
            }
            else if (eventId == WM_RBUTTONUP)
            {
                ShowLuxuryContextMenu(hwnd);
                handled = true;
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

            // 2. Open Window
            var openItem = CreateMenuItem("\uE80F", "Open Deltempo Dashboard", () => RestoreMainWindow(), new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8")));
            menu.Items.Add(openItem);

            // 3. Boost RAM
            var boostItem = CreateMenuItem("\uE768", "Boost RAM (Flush Working Sets)", async () =>
            {
                if (_onOptimizeRam != null)
                {
                    var res = await _onOptimizeRam();
                    ShowNotification("⚡ RAM Booster", $"Reclaimed {res.FormattedReclaimed} across {res.ProcessesOptimized} tasks in {res.ExecutionTimeMs}ms!");
                    UpdateTooltip();
                }
            }, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00E5FF")));
            menu.Items.Add(boostItem);

            // 4. Quick Clean
            var cleanItem = CreateMenuItem("\uE74D", "Clean 100% Safe Caches Now", () => _onCleanSafeNow?.Invoke(), new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")));
            menu.Items.Add(cleanItem);

            // 5. Settings
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

            // 6. Exit
            var exitItem = CreateMenuItem("\uE711", "Exit Deltempo", () =>
            {
                Dispose();
                Application.Current.Shutdown();
            }, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171")));
            menu.Items.Add(exitItem);

            SetForegroundWindow(hWnd);
            menu.IsOpen = true;
        });
    }

    private static MenuItem CreateMenuItem(string iconGlyph, string title, Action onClick, Brush iconBrush)
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

        item.Click += (s, e) => onClick();
        return item;
    }

    public static void MinimizeToTray()
    {
        if (_mainWindow == null) return;
        _mainWindow.Hide();
        if (SettingsService.Current.AutoCleanNotify)
        {
            ShowNotification("Deltempo Running in Background", "Standing guard to protect your disk space and memory. Double-click tray icon to restore.");
        }
    }

    public static void RestoreMainWindow()
    {
        if (_mainWindow == null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    public static void ShowNotification(string title, string message)
    {
        if (!_isInitialized || _mainWindow == null) return;
        var helper = new WindowInteropHelper(_mainWindow);
        var nid = CreateNotifyData(helper.Handle);
        nid.uFlags = NIF_INFO;
        nid.szInfoTitle = title;
        nid.szInfo = message;
        nid.dwInfoFlags = NIIF_INFO;
        nid.dwTimeoutOrVersion = 3000;
        Shell_NotifyIconW(NIM_MODIFY, ref nid);
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
