using System.Diagnostics;
using System.Security.Principal;

namespace WinTempCleaner.Services;

public static class ElevationService
{
    public static bool IsRunAsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public static void RestartAsAdmin(string? args = null)
    {
        var procInfo = new ProcessStartInfo
        {
            UseShellExecute = true,
            WorkingDirectory = Environment.CurrentDirectory,
            FileName = Process.GetCurrentProcess().MainModule?.FileName ?? Environment.ProcessPath ?? "Deltempo.exe",
            Verb = "runas",
            Arguments = args ?? string.Empty
        };

        try
        {
            Process.Start(procInfo);
            Environment.Exit(0);
        }
        catch { }
    }
}
