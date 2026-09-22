using System;
using System.Diagnostics;
using System.IO;

namespace WinTempCleaner.Services;

public static class TaskSchedulerService
{
    private const string TaskName = "DeltempoWeeklyMaintenance";

    public static bool IsTaskScheduled()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/query /tn \"{TaskName}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return false;
            proc.WaitForExit(3000);
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static (bool Success, string Message) EnableWeeklyTask(string? customExePath = null)
    {
        try
        {
            string exePath = customExePath ?? GetCliExecutablePath();
            if (!File.Exists(exePath))
            {
                return (false, $"Deltempo executable not found at '{exePath}'");
            }

            string taskCommand = $"\\\"{exePath}\\\" smart-clean --yes";
            string args = $"/create /tn \"{TaskName}\" /tr \"{taskCommand}\" /sc weekly /d SUN /st 03:00 /f /rl HIGHEST";

            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return (false, "Failed to launch schtasks.exe");
            string err = proc.StandardError.ReadToEnd();
            proc.WaitForExit(5000);

            if (proc.ExitCode == 0)
            {
                return (true, "Successfully scheduled Deltempo silent weekly maintenance for every Sunday at 03:00 AM.");
            }

            return (false, $"schtasks failed (Exit code {proc.ExitCode}): {err.Trim()}");
        }
        catch (Exception ex)
        {
            return (false, $"Error creating scheduled task: {ex.Message}");
        }
    }

    public static (bool Success, string Message) DisableWeeklyTask()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/delete /tn \"{TaskName}\" /f",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return (false, "Failed to launch schtasks.exe");
            string err = proc.StandardError.ReadToEnd();
            proc.WaitForExit(5000);

            if (proc.ExitCode == 0)
            {
                return (true, "Deltempo scheduled maintenance task removed successfully.");
            }

            return (false, $"schtasks failed: {err.Trim()}");
        }
        catch (Exception ex)
        {
            return (false, $"Error removing scheduled task: {ex.Message}");
        }
    }

    private static string GetCliExecutablePath()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string cliPath = Path.Combine(baseDir, "deltempo_cli.exe");
        if (File.Exists(cliPath)) return cliPath;

        string guiPath = Path.Combine(baseDir, "Deltempo.exe");
        if (File.Exists(guiPath)) return guiPath;

        return Process.GetCurrentProcess().MainModule?.FileName ?? cliPath;
    }
}
