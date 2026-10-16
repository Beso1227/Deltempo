using System.Text;
using Microsoft.UI.Xaml;

namespace Deltempo.App.WinUI;

/// <summary>
/// Startup diagnostics for the Stage 1 shell.
///
/// Writes a small log next to the executable so a headless/CI run can prove the shell
/// started, the window chrome applied, and Core loaded. Deliberately read-only: it never
/// scans, cleans, or otherwise touches disk beyond writing this file.
///
/// Set the --self-test argument to log and then close automatically (used by CI and by
/// agents whose shell cannot keep an interactive window open).
/// </summary>
internal static class ShellDiagnostics
{
    private static readonly string LogPath =
        Path.Combine(AppContext.BaseDirectory, "shell-startup.log");

    public static void Write(string message)
    {
        try
        {
            File.AppendAllText(
                LogPath,
                $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}",
                Encoding.UTF8);
        }
        catch
        {
            // Diagnostics must never take the app down.
        }
    }

    public static bool IsSelfTest =>
        Environment.GetCommandLineArgs().Contains("--self-test", StringComparer.OrdinalIgnoreCase);

    public static void MaybeSelfTest(Window window)
    {
        if (!IsSelfTest)
        {
            return;
        }

        var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        _ = Task.Run(async () =>
        {
            // AppData analysis walks three trees recursively; give it room before closing.
            await Task.Delay(TimeSpan.FromSeconds(20)).ConfigureAwait(false);
            queue.TryEnqueue(() =>
            {
                Write("self-test close");
                window.Close();
            });
        });
    }
}