using System.Runtime.InteropServices;
using System.Windows;
using WinTempCleaner.Services;

namespace WinTempCleaner;

public partial class App : System.Windows.Application
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    private const int ATTACH_PARENT_PROCESS = -1;

    protected override async void OnStartup(StartupEventArgs e)
    {
        var args = e.Args;

        if (args.Length > 0)
        {
            AttachConsole(ATTACH_PARENT_PROCESS);
            int exitCode = await CliRunner.RunAsync(args);
            FreeConsole();
            Shutdown(exitCode);
            return;
        }

        base.OnStartup(e);
    }
}
