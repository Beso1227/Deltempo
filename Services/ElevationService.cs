using System.Diagnostics;
using System.Security.Principal;

namespace WinTempCleaner.Services;

public static class ElevationService
{
    public static bool IsAdministrator => IsRunAsAdmin();

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
            FileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "Deltempo.exe",
            Verb = "runas",
            Arguments = args ?? string.Empty
        };

        try
        {
            Process.Start(procInfo);
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }
}
