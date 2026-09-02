using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
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

    private const uint MF_STRING = 0x00000000;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint TPM_RETURNCMD = 0x0100;
    private const uint TPM_NONOTIFY = 0x0080;

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint TrackPopupMenuEx(IntPtr hmenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractIconW(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

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
        catch { }

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
                    _hIcon = ico.Handle;
                }
            }
            catch { }
        }

        if (_hIcon == IntPtr.Zero)
        {
            try
            {
                string exePath = Environment.ProcessPath ?? "";
                _hIcon = ExtractIconW(IntPtr.Zero, exePath, 0);
            }
            catch { }
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
                ShowContextMenu(hwnd);
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    private static async void ShowContextMenu(IntPtr hWnd)
    {
        IntPtr hMenu = CreatePopupMenu();
        if (hMenu == IntPtr.Zero) return;

        var mem = MemoryOptimizerService.GetMemoryInfo();
        AppendMenuW(hMenu, MF_STRING, 1, $"👑 Open Deltempo (RAM: {mem.UsedPercent:0.0}%)");
        AppendMenuW(hMenu, MF_SEPARATOR, 0, string.Empty);
        AppendMenuW(hMenu, MF_STRING, 2, "⚡ Boost RAM (Flush Cache & Working Sets)");
        AppendMenuW(hMenu, MF_STRING, 3, "🧹 Clean Safe Caches Now");
        AppendMenuW(hMenu, MF_STRING, 4, "⚙️ Settings");
        AppendMenuW(hMenu, MF_SEPARATOR, 0, string.Empty);
        AppendMenuW(hMenu, MF_STRING, 5, "❌ Exit Deltempo");

        GetCursorPos(out POINT pt);
        SetForegroundWindow(hWnd);

        uint cmd = TrackPopupMenuEx(hMenu, TPM_RETURNCMD | TPM_NONOTIFY, pt.X, pt.Y, hWnd, IntPtr.Zero);
        DestroyMenu(hMenu);

        switch (cmd)
        {
            case 1:
                RestoreMainWindow();
                break;
            case 2:
                if (_onOptimizeRam != null)
                {
                    var res = await _onOptimizeRam();
                    ShowNotification("⚡ RAM Booster", $"Reclaimed {res.FormattedReclaimed} across {res.ProcessesOptimized} tasks in {res.ExecutionTimeMs}ms!");
                    UpdateTooltip();
                }
                break;
            case 3:
                _onCleanSafeNow?.Invoke();
                break;
            case 4:
                RestoreMainWindow();
                _onOpenSettings?.Invoke();
                break;
            case 5:
                Dispose();
                Application.Current.Shutdown();
                break;
        }
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
