using System.Text;
using WinTempCleaner.Services;

namespace WinTempCleaner.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }

        // System integration (PATH, App Paths registry, PowerShell profiles) is strictly
        // opt-in: only the explicit `deltempo register` command mutates the host. No other
        // invocation writes outside the application's own data directories.
        if (args.Length > 0 && args[0].Equals("register", StringComparison.OrdinalIgnoreCase))
        {
            return CliRegistrationService.HandleRegisterCommand(args);
        }

        // If no args passed in console, print help
        if (args.Length == 0)
        {
            args = new[] { "help" };
        }

        return await CliRunner.RunAsync(args);
    }
}
