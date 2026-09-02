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
        catch { }

        // If no args passed in console, print help
        if (args.Length == 0)
        {
            args = new[] { "help" };
        }

        return await CliRunner.RunAsync(args);
    }
}
