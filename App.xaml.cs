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

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

    private const int ATTACH_PARENT_PROCESS = -1;
    private const int STD_OUTPUT_HANDLE = -11;
    private const int STD_ERROR_HANDLE = -12;

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            SetCurrentProcessExplicitAppUserModelID("Deltempo.Guardian.WindowsCleaner");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] SetAppUserModelID suppressed: {ex.Message}");
        }

        base.OnStartup(e);

        DispatcherUnhandledException += (s, args) =>
        {
            try
            {
                var logFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Deltempo", "crash.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logFile)!);
                File.AppendAllText(logFile, $"[{DateTime.Now}] Crash: {args.Exception}\n");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        };

        // Automatically ensure 'deltempo' is globally accessible in terminal & Win+R
        CliRegistrationService.EnsureCliRegistered();

        var args = e.Args;

        if (args.Length > 0)
        {
            SetupConsoleStream();
            int exitCode = CliRunner.RunAsync(args).GetAwaiter().GetResult();
            try { Console.Out.Flush(); } catch { }
            Environment.Exit(exitCode);
            return;
        }

        // GUI Single-Instance enforcement to prevent duplicate tray icons
        if (!SingleInstanceManager.TryAcquire())
        {
            SingleInstanceManager.NotifyExistingInstance();
            Shutdown(0);
            return;
        }

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private static void SetupConsoleStream()
    {
        try
        {
            AttachConsole(ATTACH_PARENT_PROCESS);

            IntPtr stdOutHandle = GetStdHandle(STD_OUTPUT_HANDLE);
            if (stdOutHandle != IntPtr.Zero && stdOutHandle != new IntPtr(-1))
            {
                var safeHandle = new Microsoft.Win32.SafeHandles.SafeFileHandle(stdOutHandle, ownsHandle: false);
                var fs = new FileStream(safeHandle, FileAccess.Write);
                var writer = new StreamWriter(fs, new UTF8Encoding(false)) { AutoFlush = true };
                Console.SetOut(writer);
                Console.SetError(writer);
                Console.OutputEncoding = Encoding.UTF8;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            TrayService.Dispose();
            SingleInstanceManager.Release();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }

        base.OnExit(e);
    }
}
