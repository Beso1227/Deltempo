using System.Drawing;
using System.IO;
using System.Windows;
using Forms = System.Windows.Forms;

namespace WinTempCleaner.Services;

public static class TrayService
{
    private static Forms.NotifyIcon? _notifyIcon;
    private static Window? _mainWindow;
    private static Action? _onCleanSafeNow;
    private static Action? _onOpenSettings;

    public static void Initialize(Window mainWindow, Action onCleanSafeNow, Action onOpenSettings)
    {
        _mainWindow = mainWindow;
        _onCleanSafeNow = onCleanSafeNow;
        _onOpenSettings = onOpenSettings;

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "Deltempo — Windows & User Profile Guardian",
            Visible = true
        };

        try
        {
            if (File.Exists("app.ico"))
            {
                _notifyIcon.Icon = new Icon("app.ico");
            }
            else
            {
                _notifyIcon.Icon = SystemIcons.Application;
            }
        }
        catch
        {
            _notifyIcon.Icon = SystemIcons.Application;
        }

        var contextMenu = new Forms.ContextMenuStrip();

        var openItem = new Forms.ToolStripMenuItem("👑 Open Deltempo");
        openItem.Font = new System.Drawing.Font(openItem.Font, System.Drawing.FontStyle.Bold);
        openItem.Click += (s, e) => RestoreMainWindow();

        var cleanItem = new Forms.ToolStripMenuItem("⚡ Clean Safe Caches Now");
        cleanItem.Click += (s, e) => _onCleanSafeNow?.Invoke();

        var settingsItem = new Forms.ToolStripMenuItem("⚙️ Settings");
        settingsItem.Click += (s, e) =>
        {
            RestoreMainWindow();
            _onOpenSettings?.Invoke();
        };

        var exitItem = new Forms.ToolStripMenuItem("❌ Exit Deltempo");
        exitItem.Click += (s, e) =>
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            System.Windows.Application.Current.Shutdown();
        };

        contextMenu.Items.Add(openItem);
        contextMenu.Items.Add(new Forms.ToolStripSeparator());
        contextMenu.Items.Add(cleanItem);
        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(new Forms.ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (s, e) => RestoreMainWindow();
    }

    public static void MinimizeToTray()
    {
        if (_mainWindow == null) return;
        _mainWindow.Hide();
        if (_notifyIcon != null && SettingsService.Current.AutoCleanNotify)
        {
            _notifyIcon.ShowBalloonTip(
                2500,
                "Deltempo Running in Background",
                "Deltempo is actively guarding your disk space. Double-click to open.",
                Forms.ToolTipIcon.Info);
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
        if (_notifyIcon != null && SettingsService.Current.AutoCleanNotify)
        {
            _notifyIcon.ShowBalloonTip(3000, title, message, Forms.ToolTipIcon.Info);
        }
    }

    public static void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
