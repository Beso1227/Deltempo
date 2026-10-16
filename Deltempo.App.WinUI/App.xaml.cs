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
        _window = new MainWindow();
        _window.Activate();
    }
}