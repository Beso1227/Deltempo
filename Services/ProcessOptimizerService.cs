using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public class ProcessMemoryInfo
{
    public int ProcessId { get; set; }
    public List<int> ProcessIds { get; set; } = new();
    public string ProcessName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public string CategoryDescription { get; set; } = "Background Application";
    public string CategoryIcon { get; set; } = "\uE713";
    public long WorkingSetBytes { get; set; }
    public string FormattedMemory => TargetFolderInfo.FormatBytes(WorkingSetBytes);
    public bool IsSafeToClose { get; set; } = true;
    public int ProcessCount => ProcessIds.Count > 0 ? ProcessIds.Count : 1;

    public string DisplayName
    {
        get
        {
            string baseName = !string.IsNullOrWhiteSpace(FriendlyName) ? FriendlyName : ProcessName;
            if (ProcessCount > 1)
            {
                return $"{baseName} ({ProcessCount} instances)";
            }
            return !string.IsNullOrWhiteSpace(WindowTitle) ? $"{baseName} — {WindowTitle}" : baseName;
        }
    }
}

public static class ProcessOptimizerService
{
    private const int PROCESS_QUERY_INFORMATION = 0x0400;
    private const int PROCESS_SET_QUOTA = 0x0100;
    private const int PROCESS_TERMINATE = 0x0001;

    private const string SeDebugName = "SeDebugPrivilege";
    private const string SeIncreaseQuotaName = "SeIncreaseQuotaPrivilege";
    private const int PrivilegeAttributeEnabled = 2;
    private const int TokenAdjustPrivileges = 0x0020;
    private const int TokenQuery = 0x0008;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct TokenPrivileges
    {
        public int Count;
        public long Luid;
        public int Attr;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, ref long lpLuid);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges, ref TokenPrivileges NewState, int BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private static bool SetIncreasePrivilege(string privilegeName)
    {
        try
        {
            if (OpenProcessToken(Process.GetCurrentProcess().Handle, TokenAdjustPrivileges | TokenQuery, out IntPtr tokenHandle))
            {
                try
                {
                    var tp = new TokenPrivileges { Count = 1, Attr = PrivilegeAttributeEnabled };
                    if (LookupPrivilegeValue(null, privilegeName, ref tp.Luid))
                    {
                        return AdjustTokenPrivileges(tokenHandle, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
                    }
                }
                finally
                {
                    CloseHandle(tokenHandle);
                }
            }
        }
        catch { }
        return false;
    }

    // 65+ Core Windows System, Kernel, Security & Infrastructure Whitelist
    private static readonly HashSet<string> ProtectedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "idle", "system idle process", "registry", "memory compression",
        "smss", "csrss", "wininit", "services", "lsass", "svchost", "fontdrvhost",
        "winlogon", "dwm", "explorer", "sihost", "taskhostw", "ctfmon",
        "shellexperiencehost", "startmenuexperiencehost", "searchhost", "taskmgr",
        "deltempo", "wintempcleaner", "audiodg", "spoolsv", "conhost", "runtimebroker",
        "searchindexer", "wmiprvse", "dllhost", "smartscreen", "msmpeng", "nissrv",
        "securityhealthservice", "securityhealthsystray", "mpdefendercoreprocess", "mpdefendercoreservice",
        "sense", "secure system", "vmmem", "vmmemwsl", "dasHost", "lsiso", "ngcsvc",
        "compattelrunner", "deviceassociationframeworkproviderhost", "tiworker", "trustedinstaller",
        "wlanext", "wudfhost", "sedsvc", "mpsvc"
    };

    // Known Friendly Names and Categories
    private static readonly Dictionary<string, (string FriendlyName, string Category, string Icon)> KnownApps = new(StringComparer.OrdinalIgnoreCase)
    {
        { "chrome", ("Google Chrome", "Web Browser", "\uE774") },
        { "msedge", ("Microsoft Edge", "Web Browser", "\uE774") },
        { "brave", ("Brave Browser", "Web Browser", "\uE774") },
        { "firefox", ("Mozilla Firefox", "Web Browser", "\uE774") },
        { "opera", ("Opera Browser", "Web Browser", "\uE774") },
        { "opera_gx", ("Opera GX Gaming Browser", "Web Browser", "\uE774") },
        { "arc", ("Arc Browser", "Web Browser", "\uE774") },
        { "vivaldi", ("Vivaldi Browser", "Web Browser", "\uE774") },
        { "discord", ("Discord", "Communication & Voice", "\uE8BD") },
        { "discordcanary", ("Discord Canary", "Communication & Voice", "\uE8BD") },
        { "discordptb", ("Discord PTB", "Communication & Voice", "\uE8BD") },
        { "slack", ("Slack", "Team Workspace", "\uE8BD") },
        { "teams", ("Microsoft Teams", "Video & Chat", "\uE8BD") },
        { "ms-teams", ("Microsoft Teams", "Video & Chat", "\uE8BD") },
        { "telegram", ("Telegram Desktop", "Messaging", "\uE8BD") },
        { "whatsapp", ("WhatsApp Desktop", "Messaging", "\uE8BD") },
        { "spotify", ("Spotify Desktop", "Music & Audio", "\uE8D6") },
        { "steam", ("Steam Client", "Gaming Platform", "\uE7FC") },
        { "steamwebhelper", ("Steam Web Helper", "Gaming Platform", "\uE7FC") },
        { "epicgameslauncher", ("Epic Games Launcher", "Gaming Platform", "\uE7FC") },
        { "battlenet", ("Battle.net Desktop", "Gaming Platform", "\uE7FC") },
        { "agent", ("Battle.net Agent", "Gaming Platform", "\uE7FC") },
        { "origin", ("EA Origin", "Gaming Platform", "\uE7FC") },
        { "eadesktop", ("EA Desktop", "Gaming Platform", "\uE7FC") },
        { "riotclientservices", ("Riot Client", "Gaming Platform", "\uE7FC") },
        { "code", ("Visual Studio Code", "Developer Editor", "\uE943") },
        { "devenv", ("Visual Studio IDE", "Developer IDE", "\uE943") },
        { "rider64", ("JetBrains Rider", "Developer IDE", "\uE943") },
        { "idea64", ("IntelliJ IDEA", "Developer IDE", "\uE943") },
        { "pycharm64", ("PyCharm IDE", "Developer IDE", "\uE943") },
        { "webstorm64", ("WebStorm IDE", "Developer IDE", "\uE943") },
        { "clion64", ("CLion IDE", "Developer IDE", "\uE943") },
        { "goland64", ("GoLand IDE", "Developer IDE", "\uE943") },
        { "datagrip64", ("DataGrip IDE", "Database Tool", "\uE943") },
        { "photoshop", ("Adobe Photoshop", "Creative Design", "\uE790") },
        { "premiere", ("Adobe Premiere Pro", "Video Editing", "\uE8B2") },
        { "afterfx", ("Adobe After Effects", "Motion VFX", "\uE8B2") },
        { "illustrator", ("Adobe Illustrator", "Vector Graphics", "\uE790") },
        { "resolve", ("DaVinci Resolve", "Video Editing & Color", "\uE8B2") },
        { "obs64", ("OBS Studio", "Broadcasting & Stream", "\uE714") },
        { "blender", ("Blender 3D", "3D Graphics & Animation", "\uE790") },
        { "notion", ("Notion Desktop", "Productivity & Workspace", "\uE70F") },
        { "docker desktop", ("Docker Desktop", "Containers & Virtualization", "\uE753") },
        { "com.docker.backend", ("Docker Backend Engine", "Containers & Virtualization", "\uE753") },
        { "postman", ("Postman API", "Developer API Tool", "\uE90F") },
        { "gitkraken", ("GitKraken", "Git Client", "\uE90F") },
        { "onedrive", ("Microsoft OneDrive", "Cloud Storage Sync", "\uE753") },
        { "dropbox", ("Dropbox", "Cloud Storage Sync", "\uE753") }
    };

    public static bool IsProtectedProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return true;
        string clean = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? processName[..^4] : processName;
        return ProtectedProcesses.Contains(clean);
    }

    public static async Task<List<ProcessMemoryInfo>> GetHeavyProcessesAsync(long minMemoryBytes = 20L * 1024 * 1024)
    {
        return await Task.Run(() =>
        {
            var groups = new Dictionary<string, ProcessMemoryInfo>(StringComparer.OrdinalIgnoreCase);
            var processes = Process.GetProcesses();

            foreach (var p in processes)
            {
                try
                {
                    string pName = p.ProcessName;
                    if (IsProtectedProcess(pName) || p.Id <= 4)
                        continue;

                    long ws = 0;
                    try
                    {
                        ws = p.WorkingSet64;
                    }
                    catch
                    {
                        continue;
                    }

                    if (ws <= 0) continue;

                    string windowTitle = string.Empty;
                    try
                    {
                        windowTitle = p.MainWindowTitle;
                    }
                    catch { }

                    string key = pName.ToLowerInvariant();

                    if (!groups.TryGetValue(key, out var info))
                    {
                        var (friendly, category, icon) = ResolveMetadata(p, pName);
                        info = new ProcessMemoryInfo
                        {
                            ProcessId = p.Id,
                            ProcessIds = new List<int> { p.Id },
                            ProcessName = pName,
                            WindowTitle = windowTitle,
                            FriendlyName = friendly,
                            CategoryDescription = category,
                            CategoryIcon = icon,
                            WorkingSetBytes = ws,
                            IsSafeToClose = true
                        };
                        groups[key] = info;
                    }
                    else
                    {
                        info.ProcessIds.Add(p.Id);
                        info.WorkingSetBytes += ws;
                        if (string.IsNullOrWhiteSpace(info.WindowTitle) && !string.IsNullOrWhiteSpace(windowTitle))
                        {
                            info.WindowTitle = windowTitle;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[Deltempo] Suppressed process query: {ex.Message}");
                }
                finally
                {
                    p.Dispose();
                }
            }

            return groups.Values
                .Where(x => x.WorkingSetBytes >= minMemoryBytes)
                .OrderByDescending(x => x.WorkingSetBytes)
                .Take(50)
                .ToList();
        });
    }

    private static (string FriendlyName, string Category, string Icon) ResolveMetadata(Process p, string processName)
    {
        if (KnownApps.TryGetValue(processName, out var known))
        {
            return known;
        }

        try
        {
            if (p.MainModule?.FileVersionInfo?.FileDescription is { Length: > 0 } desc)
            {
                return (desc, "Background Application", "\uE713");
            }
        }
        catch { }

        string prettyName = string.IsNullOrWhiteSpace(processName) ? "Unknown" : char.ToUpperInvariant(processName[0]) + processName[1..];
        return (prettyName, "Background Application", "\uE713");
    }

    public static bool TrimProcessMemory(int pid)
    {
        return TrimProcessMemory(new List<int> { pid });
    }

    public static bool TrimProcessMemory(IEnumerable<int> pids)
    {
        var (success, _) = TrimProcessMemoryEx(pids);
        return success;
    }

    public static (bool Success, long FreedBytes) TrimProcessMemoryEx(IEnumerable<int> pids)
    {
        SetIncreasePrivilege(SeDebugName);
        SetIncreasePrivilege(SeIncreaseQuotaName);

        bool anySuccess = false;
        long totalFreed = 0;

        foreach (int pid in pids)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                if (p != null && !IsProtectedProcess(p.ProcessName))
                {
                    long before = 0;
                    try { before = p.WorkingSet64; } catch { }

                    IntPtr hProc = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_SET_QUOTA, false, pid);
                    if (hProc != IntPtr.Zero)
                    {
                        try
                        {
                            if (EmptyWorkingSet(hProc) != 0)
                            {
                                anySuccess = true;
                                try
                                {
                                    p.Refresh();
                                    long after = p.WorkingSet64;
                                    if (before > after) totalFreed += (before - after);
                                }
                                catch { }
                            }
                        }
                        finally
                        {
                            CloseHandle(hProc);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Deltempo] Suppressed trim exception for PID {pid}: {ex.Message}");
            }
        }

        return (anySuccess, totalFreed);
    }

    public static bool SafeTerminateProcess(int pid)
    {
        return SafeTerminateProcess(new List<int> { pid });
    }

    public static bool SafeTerminateProcess(IEnumerable<int> pids)
    {
        SetIncreasePrivilege(SeDebugName);

        bool anySuccess = false;
        foreach (int pid in pids)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                if (p != null && !IsProtectedProcess(p.ProcessName))
                {
                    p.Kill(true);
                    anySuccess = true;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Deltempo] Suppressed kill exception for PID {pid}: {ex.Message}");
            }
        }
        return anySuccess;
    }
}
