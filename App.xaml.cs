using System.Windows;

namespace WinTempCleaner;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Contains("--test"))
        {
            Task.Run(async () =>
            {
                bool success = await Tests.TestRunner.RunVerificationAsync();
                Environment.Exit(success ? 0 : 1);
            }).Wait();
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            MessageBox.Show($"An unexpected error occurred: {args.ExceptionObject}",
                "WinTemp Cleaner Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        };
    }
}
