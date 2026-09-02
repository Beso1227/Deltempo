using System.Diagnostics;
using System.Runtime.InteropServices;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public class ProcessMemoryInfo
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public long WorkingSetBytes { get; set; }
    public string FormattedMemory => TargetFolderInfo.FormatBytes(WorkingSetBytes);
    public bool IsSafeToClose { get; set; } = true;
    public string DisplayName => string.IsNullOrWhiteSpace(WindowTitle) ? ProcessName : $"{ProcessName} ({WindowTitle})";
}

public static class ProcessOptimizerService
{
    [DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    private static readonly HashSet<string> ProtectedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "idle", "registry", "smss", "csrss", "wininit", "services", "lsass",
        "svchost", "fontdrvhost", "winlogon", "dwm", "explorer", "sihost", "taskhostw",
        "ctfmon", "shellexperiencehost", "startmenuexperiencehost", "searchhost", "taskmgr",
        "deltempo", "wintempcleaner", "audiodg", "spoolsv"
    };

    public static bool IsProtectedProcess(string processName) => ProtectedProcesses.Contains(processName);

    public static async Task<List<ProcessMemoryInfo>> GetHeavyProcessesAsync(long minMemoryBytes = 80L * 1024 * 1024)
    {
        return await Task.Run(() =>
        {
            var list = new List<ProcessMemoryInfo>();
            var processes = Process.GetProcesses();

            foreach (var p in processes)
            {
                try
                {
                    if (ProtectedProcesses.Contains(p.ProcessName) || p.Id <= 4)
                        continue;

                    long ws = p.WorkingSet64;
                    if (ws >= minMemoryBytes)
                    {
                        list.Add(new ProcessMemoryInfo
                        {
                            ProcessId = p.Id,
                            ProcessName = p.ProcessName,
                            WindowTitle = p.MainWindowTitle,
                            WorkingSetBytes = ws,
                            IsSafeToClose = true
                        });
                    }
                }
                catch { }
                finally
                {
                    p.Dispose();
                }
            }

            return list.OrderByDescending(x => x.WorkingSetBytes).Take(40).ToList();
        });
    }

    public static bool TrimProcessMemory(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            if (p != null && !ProtectedProcesses.Contains(p.ProcessName) && p.Handle != IntPtr.Zero)
            {
                EmptyWorkingSet(p.Handle);
                return true;
            }
        }
        catch { }
        return false;
    }

    public static bool SafeTerminateProcess(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            if (p != null && !ProtectedProcesses.Contains(p.ProcessName))
            {
                p.Kill(true);
                return true;
            }
        }
        catch { }
        return false;
    }
}
