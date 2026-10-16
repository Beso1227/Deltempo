using Microsoft.UI.Xaml;

namespace Deltempo.App.WinUI;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();

        // Deltempo ships a dark-first design. The WPF app exposes a light-theme toggle
        // that never had brush overrides; until that design work happens, force dark so
        // the two UIs do not diverge.
        RequestedTheme = ApplicationTheme.Dark;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}