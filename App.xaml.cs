using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using WinTempCleaner.Services;

namespace WinTempCleaner;

public partial class App : System.Windows.Application
{
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    private const int SW_HIDE = 0;

    protected override async void OnStartup(StartupEventArgs e)
    {
        var args = e.Args;

        if (args.Length > 0)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
            }
            catch { }

            int exitCode = await CliRunner.RunAsync(args);
            Shutdown(exitCode);
            return;
        }

        // GUI Mode: Hide console window immediately if launched without terminal
        try
        {
            IntPtr consoleWnd = GetConsoleWindow();
            if (consoleWnd != IntPtr.Zero)
            {
                ShowWindow(consoleWnd, SW_HIDE);
            }
            FreeConsole();
        }
        catch { }

        // Automatically ensure 'deltempo' is globally accessible in terminal & Win+R
        CliRegistrationService.EnsureCliRegistered();

        // GUI Single-Instance enforcement to prevent duplicate tray icons
        if (!SingleInstanceManager.TryAcquire())
        {
            SingleInstanceManager.NotifyExistingInstance();
            Shutdown(0);
            return;
        }

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            TrayService.Dispose();
            SingleInstanceManager.Release();
        }
        catch { }

        base.OnExit(e);
    }
}
