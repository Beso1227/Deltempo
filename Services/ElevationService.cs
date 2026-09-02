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

    public static bool RelaunchAsAdmin()
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty,
                UseShellExecute = true,
                Verb = "runas"
            };

            if (string.IsNullOrEmpty(processInfo.FileName))
                return false;

            Process.Start(processInfo);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
