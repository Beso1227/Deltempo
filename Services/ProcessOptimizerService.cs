using System.Diagnostics;
using System.Runtime.InteropServices;
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
    public string CategoryIcon { get; set; } = "⚙️";
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
                return $"{baseName} ({ProcessCount} processes)";
            }
            return !string.IsNullOrWhiteSpace(WindowTitle) ? $"{baseName} — {WindowTitle}" : baseName;
        }
    }
}

public static class ProcessOptimizerService
{
    private const int PROCESS_QUERY_INFORMATION = 0x0400;
    private const int PROCESS_SET_QUOTA = 0x0100;

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

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
        { "chrome", ("Google Chrome", "Web Browser", "🌐") },
        { "msedge", ("Microsoft Edge", "Web Browser", "🌐") },
        { "brave", ("Brave Browser", "Web Browser", "🌐") },
        { "firefox", ("Mozilla Firefox", "Web Browser", "🌐") },
        { "opera", ("Opera Browser", "Web Browser", "🌐") },
        { "opera_gx", ("Opera GX Gaming Browser", "Web Browser", "🌐") },
        { "discord", ("Discord", "Communication & Voice", "💬") },
        { "discordcanary", ("Discord Canary", "Communication & Voice", "💬") },
        { "discordptb", ("Discord PTB", "Communication & Voice", "💬") },
        { "slack", ("Slack", "Team Workspace", "💬") },
        { "teams", ("Microsoft Teams", "Video & Chat", "💬") },
        { "ms-teams", ("Microsoft Teams", "Video & Chat", "💬") },
        { "telegram", ("Telegram Desktop", "Messaging", "💬") },
        { "whatsapp", ("WhatsApp Desktop", "Messaging", "💬") },
        { "spotify", ("Spotify Desktop", "Music & Audio", "🎵") },
        { "steam", ("Steam Client", "Gaming Platform", "🎮") },
        { "steamwebhelper", ("Steam Web Helper", "Gaming Platform", "🎮") },
        { "epicgameslauncher", ("Epic Games Launcher", "Gaming Platform", "🎮") },
        { "battlenet", ("Battle.net Desktop", "Gaming Platform", "🎮") },
        { "agent", ("Battle.net Agent", "Gaming Platform", "🎮") },
        { "origin", ("EA Origin", "Gaming Platform", "🎮") },
        { "eadesktop", ("EA Desktop", "Gaming Platform", "🎮") },
        { "riotclientservices", ("Riot Client", "Gaming Platform", "🎮") },
        { "code", ("Visual Studio Code", "Developer Editor", "💻") },
        { "devenv", ("Visual Studio IDE", "Developer IDE", "💻") },
        { "rider64", ("JetBrains Rider", "Developer IDE", "💻") },
        { "idea64", ("IntelliJ IDEA", "Developer IDE", "💻") },
        { "pycharm64", ("PyCharm IDE", "Developer IDE", "💻") },
        { "webstorm64", ("WebStorm IDE", "Developer IDE", "💻") },
        { "clion64", ("CLion IDE", "Developer IDE", "💻") },
        { "goland64", ("GoLand IDE", "Developer IDE", "💻") },
        { "datagrip64", ("DataGrip IDE", "Database Tool", "💻") },
        { "photoshop", ("Adobe Photoshop", "Creative Design", "🎨") },
        { "premiere", ("Adobe Premiere Pro", "Video Editing", "🎬") },
        { "afterfx", ("Adobe After Effects", "Motion VFX", "🎬") },
        { "illustrator", ("Adobe Illustrator", "Vector Graphics", "🎨") },
        { "resolve", ("DaVinci Resolve", "Video Editing & Color", "🎬") },
        { "obs64", ("OBS Studio", "Broadcasting & Stream", "📹") },
        { "blender", ("Blender 3D", "3D Graphics & Animation", "🎨") },
        { "notion", ("Notion Desktop", "Productivity & Workspace", "📝") },
        { "docker desktop", ("Docker Desktop", "Containers & Virtualization", "🐳") },
        { "postman", ("Postman API", "Developer API Tool", "🛠️") },
        { "gitkraken", ("GitKraken", "Git Client", "🛠️") }
    };

    public static bool IsProtectedProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return true;
        string clean = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? processName[..^4] : processName;
        return ProtectedProcesses.Contains(clean);
    }

    public static async Task<List<ProcessMemoryInfo>> GetHeavyProcessesAsync(long minMemoryBytes = 60L * 1024 * 1024)
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
                    System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed process query: {ex.Message}");
                }
                finally
                {
                    p.Dispose();
                }
            }

            return groups.Values
                .Where(x => x.WorkingSetBytes >= minMemoryBytes)
                .OrderByDescending(x => x.WorkingSetBytes)
                .Take(35)
                .ToList();
        });
    }

    private static (string FriendlyName, string Category, string Icon) ResolveMetadata(Process p, string processName)
    {
        if (KnownApps.TryGetValue(processName, out var known))
        {
            return known;
        }

        // Attempt to inspect FileDescription from MainModule
        try
        {
            if (p.MainModule?.FileVersionInfo?.FileDescription is { Length: > 0 } desc)
            {
                return (desc, "Background Application", "⚙️");
            }
        }
        catch { }

        // Fallback: capitalized process name
        string prettyName = char.ToUpperInvariant(processName[0]) + processName[1..];
        return (prettyName, "Background Application", "⚙️");
    }

    public static bool TrimProcessMemory(int pid)
    {
        return TrimProcessMemory(new List<int> { pid });
    }

    public static bool TrimProcessMemory(IEnumerable<int> pids)
    {
        bool anySuccess = false;
        foreach (int pid in pids)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                if (p != null && !IsProtectedProcess(p.ProcessName))
                {
                    IntPtr hProc = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_SET_QUOTA, false, pid);
                    if (hProc != IntPtr.Zero)
                    {
                        try
                        {
                            if (EmptyWorkingSet(hProc) != 0)
                            {
                                anySuccess = true;
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
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed trim exception for PID {pid}: {ex.Message}");
            }
        }
        return anySuccess;
    }

    public static bool SafeTerminateProcess(int pid)
    {
        return SafeTerminateProcess(new List<int> { pid });
    }

    public static bool SafeTerminateProcess(IEnumerable<int> pids)
    {
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
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed kill exception for PID {pid}: {ex.Message}");
            }
        }
        return anySuccess;
    }
}

