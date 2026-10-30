using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System.IO;

namespace Deltempo.App.WinUI;

public partial class App : Application
{
    /// <summary>
    /// The WinUI head's single-instance mutex. Deliberately distinct from the
    /// shipped WPF head's "Global\Deltempo_App_SingleInstance_Mutex_v2" (see
    /// docs/THREAT_MODEL.md section 2.9): a user running the GitHub WPF build
    /// and the Store WinUI build side by side must not have either launch
    /// silently defer to the other. Each head single-instances against itself;
    /// the v2 WPF name predates the Store plan and ships as-is.
    /// </summary>
    private const string SingleInstanceMutexName = "Global\\Deltempo_WinUI_SingleInstance_Mutex_v1";

    private Window? _window;

    /// <summary>
    /// The preview writes its own rolled log instead of sharing
    /// %LOCALAPPDATA%\Deltempo\logs\deltempo.log with the WPF app: both shells can
    /// legitimately run at once (they own two different single-instance mutexes),
    /// and two processes must not race the same listener's roll chain.
    /// </summary>
    private const string LogFileName = "deltempo-winui.log";

    public App()
    {
        InitializeComponent();

        // Dark-first design. The WPF app exposes a light-theme toggle that never had
        // brush overrides; until that design work happens, force dark so the two
        // UIs do not diverge.
        RequestedTheme = ApplicationTheme.Dark;

        // A WinUI unhandled exception kills the process with no console output.
        // Log it before it goes so a crash is diagnosable after the fact. WPF wrote
        // the crash line itself; here UnhandledException is the only hook WinUI has,
        // and the rolling file log carries the core's Trace output in release builds.
        UnhandledException += (_, e) =>
        {
            ShellDiagnostics.Write($"FATAL unhandled: {e.Exception}");
            try
            {
                var crashLog = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Deltempo", "crash.log");
                Directory.CreateDirectory(Path.GetDirectoryName(crashLog)!);
                File.AppendAllText(crashLog, $"[{DateTime.Now}] WinUI crash: {e.Exception}\n");
            }
            catch
            {
                // The crash log cannot be reached; the shell-startup.log line above
                // is the remaining record and must not be replaced by a throw.
            }
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Same order as the WPF App.OnStartup: diagnostics first, so core traces
        // emitted during the rest of startup are already captured. Local file only,
        // size-rolled (2 MB, three backups) - the RollingFileTraceListener in Core
        // does the rolling; this shell just picks the file.
        AttachDiagnosticLog();

        // Same contract as the shipped app: one instance owns the machine, a second
        // launch surfaces the first instead of opening a twin window. The preview
        // uses its own mutex name so it can run beside the WPF app while both are
        // installed.
        if (!WinTempCleaner.Services.SingleInstanceManager.TryAcquire(SingleInstanceMutexName))
        {
            WinTempCleaner.Services.SingleInstanceManager.NotifyExistingInstance("Deltempo.WinUI");
            Exit();
            return;
        }

        // Shared services (CleanerService, LargeFileHunterService) raise
        // INotifyPropertyChanged from worker threads and used to marshal through the WPF
        // dispatcher, which does not exist here. Without this the notification crosses
        // into WinRT from the wrong thread and throws RPC_E_WRONG_THREAD, which silently
        // cost the Cleaner workspace every scan result.
        if (DispatcherQueue.GetForCurrentThread() is { } queue)
        {
            WinTempCleaner.Services.UiDispatcher.Install(action =>
            {
                if (queue.HasThreadAccess)
                {
                    action();
                    return;
                }

                using var done = new ManualResetEventSlim(false);
                if (!queue.TryEnqueue(() =>
                {
                    try
                    {
                        action();
                    }
                    finally
                    {
                        done.Set();
                    }
                }))
                {
                    // The queue is already shutting down. Dropping the update would
                    // leave stale state behind, so run it inline and take the
                    // wrong-thread risk rather than losing it.
                    action();
                    return;
                }

                done.Wait();
            });
        }

        // Settings the shell itself acts on at launch. Both were previously applied
        // only when the Settings panel happened to be constructed, so a saved
        // preference was dead until the user visited that screen.
        WinTempCleaner.Services.SoundService.IsSoundEnabled = WinTempCleaner.Services.SettingsService.Current.SoundEnabled;
        WinTempCleaner.Services.AutoCleanService.Start();

        _window = new MainWindow();
        _window.Closed += (_, _) => WinTempCleaner.Services.SingleInstanceManager.Release();
        _window.Activate();
    }

    /// <summary>
    /// WPF's InitializeDiagnostics, verbatim except for the file name. Never throws:
    /// an unwritable log location degrades to the shell-startup.log record only.
    /// </summary>
    private static void AttachDiagnosticLog()
    {
        try
        {
            string logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Deltempo", "logs");

            bool attached = WinTempCleaner.Services.DiagnosticLogFileService.TryAttach(
                Path.Combine(logDir, LogFileName));

            ShellDiagnostics.Write(
                $"diagnostics file log: {(attached ? $"attached {LogFileName}" : "unavailable")}");
        }
        catch
        {
        }
    }
}
