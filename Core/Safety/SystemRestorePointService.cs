using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace WinTempCleaner.Core.Safety;

public enum RestorePointType
{
    ApplicationInstall = 0,
    ApplicationUninstall = 1,
    DeviceDriverInstall = 10,
    ModifySettings = 12,
    CancelledOperation = 13
}

/// <summary>
/// Native Windows System Restore Point creation engine.
/// Creates atomic recovery checkpoints before uninstallation or major system modifications.
/// </summary>
public static class SystemRestorePointService
{
    private const int BEGIN_SYSTEM_CHANGE = 100;
    private const int END_SYSTEM_CHANGE = 101;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RESTOREPOINTINFO
    {
        public int dwEventType;
        public int dwRestorePtType;
        public long llSequenceNumber;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szDescription;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STATEMGRSTATUS
    {
        public int nStatus;
        public long llSequenceNumber;
    }

    [DllImport("srclient.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SRSetRestorePointW(ref RESTOREPOINTINFO pRestorePointInfo, ref STATEMGRSTATUS pSMgrStatus);

    /// <summary>
    /// Asynchronously creates a Windows System Restore Point.
    /// Returns (true, sequenceNumber, message) if successful.
    /// </summary>
    public static async Task<(bool Success, long SequenceNumber, string Message)> CreateRestorePointAsync(
        string description,
        RestorePointType type = RestorePointType.ApplicationUninstall)
    {
        return await Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                description = "Deltempo Pre-Uninstall Checkpoint";
            }

            if (description.Length > 250)
            {
                description = description.Substring(0, 250);
            }

            // 1. Attempt Native P/Invoke via srclient.dll
            try
            {
                var rpi = new RESTOREPOINTINFO
                {
                    dwEventType = BEGIN_SYSTEM_CHANGE,
                    dwRestorePtType = (int)type,
                    llSequenceNumber = 0,
                    szDescription = description
                };

                var status = new STATEMGRSTATUS();

                if (SRSetRestorePointW(ref rpi, ref status))
                {
                    if (status.nStatus == 0 /* ERROR_SUCCESS */)
                    {
                        return (true, status.llSequenceNumber, $"Created native restore point #{status.llSequenceNumber}: '{description}'.");
                    }
                }
            }
            catch (DllNotFoundException)
            {
                // Fall through to WMI
            }
            catch (EntryPointNotFoundException)
            {
                // Fall through to WMI
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[SystemRestore] srclient.dll failed: {ex.Message}");
            }

            // 2. Fallback via PowerShell Checkpoint-Computer
            try
            {
                string script = $"Checkpoint-Computer -Description '{description.Replace("'", "''")}' -RestorePointType APPLICATION_UNINSTALL -ErrorAction Stop";
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    bool exited = proc.WaitForExit(30000);
                    if (exited && proc.ExitCode == 0)
                    {
                        return (true, 0, $"Created restore point via PowerShell: '{description}'.");
                    }
                    string err = proc.StandardError.ReadToEnd();
                    return (false, 0, $"System Restore checkpoint skipped or not enabled on system: {err.Trim()}");
                }
            }
            catch (Exception ex)
            {
                return (false, 0, $"System Restore unavailable: {ex.Message}");
            }

            return (false, 0, "System Restore is disabled on this system or requires elevated Administrator privileges.");
        });
    }
}
