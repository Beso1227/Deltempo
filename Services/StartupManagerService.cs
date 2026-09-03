using Microsoft.Win32;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WinTempCleaner.Services;

public enum BootImpact
{
    Low,
    Medium,
    High
}

public class StartupItem
{
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty; // HKCU, HKLM, Folder
    public bool IsEnabled { get; set; } = true;
    public BootImpact Impact { get; set; } = BootImpact.Low;
    public string Publisher { get; set; } = "Unknown";
    public string ImpactText => Impact switch
    {
        BootImpact.High => "High Impact",
        BootImpact.Medium => "Medium Impact",
        _ => "Low Impact"
    };
    public string ImpactColor => Impact switch
    {
        BootImpact.High => "#EF4444",
        BootImpact.Medium => "#F59E0B",
        _ => "#10B981"
    };
}

public static class StartupManagerService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunDisabledKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run_Deltempo_Disabled";
    private const string RunOnceKeyPath = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";

    public static async Task<List<StartupItem>> GetStartupItemsAsync()
    {
        return await Task.Run(() =>
        {
            var list = new List<StartupItem>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Current User Run Key (HKCU)
            AddRunKeyItems(Registry.CurrentUser, RunKeyPath, "HKCU", true, list, seen);
            // 2. Local Machine Run Key (HKLM) — requires admin to read some values
            AddRunKeyItems(Registry.LocalMachine, RunKeyPath, "HKLM", true, list, seen);
            // 3. RunOnce keys (both HKCU and HKLM) — one-shot, usually empty
            AddRunKeyItems(Registry.CurrentUser, RunOnceKeyPath, "HKCU", false, list, seen);
            AddRunKeyItems(Registry.LocalMachine, RunOnceKeyPath, "HKLM", false, list, seen);
            // 4. Current User Disabled Key (Deltempo backup)
            AddRunKeyItems(Registry.CurrentUser, RunDisabledKeyPath, "HKCU", false, list, seen);
            // 5. User Startup Folder
            AddStartupFolderItems(list, seen);

            return list;
        });
    }

    private static void AddRunKeyItems(RegistryKey rootKey, string subKeyPath, string location, bool enabledDefault, List<StartupItem> list, HashSet<string> seen)
    {
        try
        {
            using var key = rootKey.OpenSubKey(subKeyPath);
            if (key == null) return;

            foreach (var valName in key.GetValueNames())
            {
                var cmd = key.GetValue(valName)?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(cmd)) continue;
                if (!seen.Add(valName)) continue;

                list.Add(new StartupItem
                {
                    Name = valName,
                    Command = cmd,
                    Location = location,
                    IsEnabled = enabledDefault,
                    Impact = CalculateImpact(cmd),
                    Publisher = InferPublisher(valName, cmd)
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception enumerating {location}\\{subKeyPath}: {ex.Message}");
        }
    }

    private static void AddStartupFolderItems(List<StartupItem> list, HashSet<string> seen)
    {
        try
        {
            string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (!Directory.Exists(startupFolder)) return;

            foreach (var file in Directory.GetFiles(startupFolder))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                string name = Path.GetFileNameWithoutExtension(file);
                if (!seen.Add(name)) continue;

                if (ext == ".lnk" || ext == ".bat" || ext == ".cmd")
                {
                    list.Add(new StartupItem
                    {
                        Name = name,
                        Command = file,
                        Location = "Startup Folder",
                        IsEnabled = true,
                        Impact = BootImpact.Medium,
                        Publisher = "Startup Folder Link"
                    });
                }
                else if (ext == ".disabled")
                {
                    list.Add(new StartupItem
                    {
                        Name = name,
                        Command = file,
                        Location = "Startup Folder",
                        IsEnabled = false,
                        Impact = BootImpact.Medium,
                        Publisher = "Startup Folder Link"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception enumerating Startup folder: {ex.Message}");
        }
    }

    public static bool ToggleStartupItem(StartupItem item, bool enable)
    {
        bool alreadyDisabled = !item.IsEnabled;
        try
        {
            if (item.Location == "HKCU" || item.Location == "HKLM")
            {
                bool isHkcu = item.Location == "HKCU";
                RegistryKey rootKey = isHkcu ? Registry.CurrentUser : Registry.LocalMachine;
                string subKey = isHkcu ? RunKeyPath : RunKeyPath;
                string disSubKey = isHkcu ? RunDisabledKeyPath : RunDisabledKeyPath;

                if (!enable)
                {
                    if (alreadyDisabled) return false; // already disabled

                    using var runKey = rootKey.OpenSubKey(subKey, true);
                    using var disKey = rootKey.CreateSubKey(disSubKey);

                    if (runKey != null && disKey != null)
                    {
                        var val = runKey.GetValue(item.Name);
                        if (val != null)
                        {
                            disKey.SetValue(item.Name, val);
                            runKey.DeleteValue(item.Name, false);
                            item.IsEnabled = false;
                            return true;
                        }
                    }
                }
                else
                {
                    if (!alreadyDisabled) return false; // already enabled

                    using var disKey = rootKey.OpenSubKey(disSubKey, true);
                    using var runKey = rootKey.CreateSubKey(subKey);

                    if (disKey != null && runKey != null)
                    {
                        var val = disKey.GetValue(item.Name);
                        if (val != null)
                        {
                            runKey.SetValue(item.Name, val);
                            disKey.DeleteValue(item.Name, false);
                            item.IsEnabled = true;
                            return true;
                        }
                    }
                }
            }
            else if (item.Location == "Startup Folder")
            {
                if (!enable && File.Exists(item.Command))
                {
                    string target = item.Command + ".disabled";
                    File.Move(item.Command, target, true);
                    item.Command = target;
                    item.IsEnabled = false;
                    return true;
                }
                else if (enable && item.Command.EndsWith(".disabled") && File.Exists(item.Command))
                {
                    string target = item.Command[..^9]; // remove .disabled
                    File.Move(item.Command, target, true);
                    item.Command = target;
                    item.IsEnabled = true;
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception toggling startup item '{item.Name}': {ex.Message}");
        }

        return false;
    }

    private static BootImpact CalculateImpact(string cmd)
    {
        var lower = cmd.ToLowerInvariant();
        if (lower.Contains("electron") || lower.Contains("discord") || lower.Contains("spotify") || lower.Contains("steam") || lower.Contains("epic"))
            return BootImpact.High;
        if (lower.Contains("update") || lower.Contains("helper") || lower.Contains("service"))
            return BootImpact.Medium;
        return BootImpact.Low;
    }

    private static string InferPublisher(string name, string cmd)
    {
        var lower = (name + " " + cmd).ToLowerInvariant();
        if (lower.Contains("microsoft")) return "Microsoft Corporation";
        if (lower.Contains("discord")) return "Discord Inc.";
        if (lower.Contains("spotify")) return "Spotify AB";
        if (lower.Contains("valve") || lower.Contains("steam")) return "Valve Corporation";
        if (lower.Contains("google") || lower.Contains("chrome")) return "Google LLC";
        if (lower.Contains("adobe")) return "Adobe Systems";
        return "Application Developer";
    }
}
