using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Deltempo.App.WinUI;

public partial class App : Application
{
    /// <summary>
    /// Preview-only mutex. Deliberately distinct from the shipped app's
    /// "Global\Deltempo_App_SingleInstance_Mutex_v2" so both can run at once.
    /// </summary>
    private const string SingleInstanceMutexName = "Global\\Deltempo_WinUI_Preview_SingleInstance_Mutex_v1";

    private Window? _window;

    public App()
    {
        InitializeComponent();

        // Dark-first design. The WPF app exposes a light-theme toggle that never had
        // brush overrides; until that design work happens, force dark so the two
        // UIs do not diverge.
        RequestedTheme = ApplicationTheme.Dark;

        // A WinUI unhandled exception kills the process with no console output.
        // Log it before it goes so a crash is diagnosable after the fact.
        UnhandledException += (_, e) =>
        {
            ShellDiagnostics.Write($"FATAL unhandled: {e.Exception}");
            e.Handled = false;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
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
}
