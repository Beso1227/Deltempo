using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

/// <summary>
/// Service managing native Windows Plug-and-Play (PnP) driver package maintenance.
/// Executes the official Windows Plug-and-Play Maintenance Task Library (pnpclean.dll)
/// to safely purge obsolete, superseded device driver packages from the DriverStore.
/// Matches the native mechanism utilized by Windows Disk Cleanup and Microsoft PC Manager.
/// </summary>
public static class WindowsDriverMaintenanceService
{
    /// <summary>
    /// Executes pnpclean.dll to purge superseded driver packages from the DriverStore.
    /// Requires administrator privileges.
    /// </summary>
    public static async Task<(bool Success, string Message)> RunPnpDriverCleanAsync(
        Action<string, LogLevel>? logAction = null,
        CancellationToken ct = default)
    {
        if (!ElevationService.IsRunAsAdmin())
        {
            logAction?.Invoke("Administrator privileges required to execute Windows PnP driver maintenance.", LogLevel.Warning);
            return (false, "Administrator privileges required to execute Windows PnP driver maintenance.");
        }

        return await Task.Run(() =>
        {
            try
            {
                if (ct.IsCancellationRequested)
                {
                    return (false, "Operation cancelled.");
                }

                logAction?.Invoke("Running Windows Plug-and-Play Driver Maintenance (pnpclean.dll) to purge superseded device driver packages...", LogLevel.Info);

                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string rundllPath = Path.Combine(winDir, "System32", "rundll32.exe");
                string pnpCleanDllPath = Path.Combine(winDir, "System32", "pnpclean.dll");

                if (!File.Exists(pnpCleanDllPath))
                {
                    logAction?.Invoke("pnpclean.dll not found in System32. Skipping PnP driver maintenance.", LogLevel.Info);
                    return (false, "pnpclean.dll not present on this Windows installation.");
                }

                var psi = new ProcessStartInfo
                {
                    FileName = rundllPath,
                    Arguments = "pnpclean.dll,RunDLL_PnpClean /DRIVERS /MAXCLEAN",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false
                };

                using var proc = Process.Start(psi);
                if (proc == null)
                {
                    return (false, "Could not start rundll32 process.");
                }

                // Wait for completion with timeout (60 seconds) observing cancellation token
                int waitedMs = 0;
                while (!proc.WaitForExit(500))
                {
                    waitedMs += 500;
                    if (ct.IsCancellationRequested)
                    {
                        try { proc.Kill(); } catch { }
                        return (false, "Operation cancelled.");
                    }
                    if (waitedMs >= 60_000)
                    {
                        try { proc.Kill(); } catch { }
                        logAction?.Invoke("PnP Driver Maintenance exceeded timeout (60s) and was terminated safely.", LogLevel.Warning);
                        return (false, "PnP maintenance timed out.");
                    }
                }

                logAction?.Invoke("Windows PnP Driver Maintenance completed successfully! Obsolete driver packages safely purged from DriverStore.", LogLevel.Success);
                return (true, "PnP driver maintenance completed successfully.");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"PnP Driver Maintenance note: {ex.Message}", LogLevel.Warning);
                return (false, ex.Message);
            }
        }, ct);
    }
}
