using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using WinTempCleaner.Services;

namespace WinTempCleaner;

public partial class App : System.Windows.Application
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    private const int ATTACH_PARENT_PROCESS = -1;
    private const int STD_OUTPUT_HANDLE = -11;
    private const int STD_ERROR_HANDLE = -12;

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Automatically ensure 'deltempo' is globally accessible in terminal & Win+R
        CliRegistrationService.EnsureCliRegistered();

        var args = e.Args;

        if (args.Length > 0)
        {
            SetupConsoleStream();
            int exitCode = await CliRunner.RunAsync(args);
            FreeConsole();
            Shutdown(exitCode);
            return;
        }

        // GUI Single-Instance enforcement to prevent duplicate tray icons
        if (!SingleInstanceManager.TryAcquire())
        {
            SingleInstanceManager.NotifyExistingInstance();
            Shutdown(0);
            return;
        }

        base.OnStartup(e);
    }

    private static void SetupConsoleStream()
    {
        try
        {
            if (AttachConsole(ATTACH_PARENT_PROCESS))
            {
                IntPtr stdOutHandle = GetStdHandle(STD_OUTPUT_HANDLE);
                if (stdOutHandle != IntPtr.Zero && stdOutHandle != new IntPtr(-1))
                {
                    var safeHandle = new Microsoft.Win32.SafeHandles.SafeFileHandle(stdOutHandle, ownsHandle: false);
                    var fs = new FileStream(safeHandle, FileAccess.Write);
                    var writer = new StreamWriter(fs, Encoding.UTF8) { AutoFlush = true };
                    Console.SetOut(writer);
                    Console.SetError(writer);
                    Console.OutputEncoding = Encoding.UTF8;
                }
            }
        }
        catch { }
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
