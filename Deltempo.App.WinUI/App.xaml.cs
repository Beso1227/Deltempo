using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Deltempo.App.WinUI;

public partial class App : Application
{
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

        _window = new MainWindow();
        _window.Activate();
    }
}