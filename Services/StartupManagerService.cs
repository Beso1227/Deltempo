using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
    public string FriendlyName { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string ExePath { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty; // HKCU, HKLM, WOW64_HKLM, WOW64_HKCU, Startup Folder, Common Startup
    public string LocationDisplay => Location switch
    {
        "HKCU" => "Registry (Current User)",
        "HKLM" => "Registry (All Users)",
        "WOW64_HKLM" => "Registry (32-bit All Users)",
        "WOW64_HKCU" => "Registry (32-bit User)",
        "Startup Folder" => "Startup Folder (User)",
        "Common Startup" => "Startup Folder (All Users)",
        _ => Location
    };
    public bool IsEnabled { get; set; } = true;
    public bool IsFileMissing { get; set; } = false;
    public BootImpact Impact { get; set; } = BootImpact.Low;
    public string Publisher { get; set; } = "Unknown";
    public string DisplayTitle => !string.IsNullOrWhiteSpace(FriendlyName) ? FriendlyName : Name;
    public string ImpactText => IsFileMissing ? "Orphaned Entry" : Impact switch
    {
        BootImpact.High => "High Impact",
        BootImpact.Medium => "Medium Impact",
        _ => "Low Impact"
    };
    public string ImpactColor => IsFileMissing ? "#EF4444" : Impact switch
    {
        BootImpact.High => "#EF4444",
        BootImpact.Medium => "#F59E0B",
        _ => "#10B981"
    };
}

public static class StartupManagerService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Wow64RunKeyPath = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string RunDisabledKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run_Deltempo_Disabled";
    private const string StartupApprovedRunPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string StartupApprovedFolderPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

    public static async Task<List<StartupItem>> GetStartupItemsAsync()
    {
        return await Task.Run(() =>
        {
            var list = new List<StartupItem>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Current User Run Key (HKCU)
            AddRunKeyItems(Registry.CurrentUser, RunKeyPath, "HKCU", list, seen);

            // 2. Local Machine Run Key (HKLM 64-bit)
            AddRunKeyItems(Registry.LocalMachine, RunKeyPath, "HKLM", list, seen);

            // 3. WOW6432Node 32-bit Run Keys (HKLM & HKCU)
            AddRunKeyItems(Registry.LocalMachine, Wow64RunKeyPath, "WOW64_HKLM", list, seen);
            AddRunKeyItems(Registry.CurrentUser, Wow64RunKeyPath, "WOW64_HKCU", list, seen);

            // 4. Current User Disabled Key (Deltempo legacy backup)
            AddRunKeyItems(Registry.CurrentUser, RunDisabledKeyPath, "HKCU", list, seen, forceDisabled: true);
            AddRunKeyItems(Registry.LocalMachine, RunDisabledKeyPath, "HKLM", list, seen, forceDisabled: true);

            // 5. User Startup Folder
            AddStartupFolderItems(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Startup Folder", list, seen);

            // 6. Common Startup Folder (All Users)
            AddStartupFolderItems(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Common Startup", list, seen);

            return list
                .OrderByDescending(x => x.IsFileMissing)
                .ThenByDescending(x => x.Impact)
                .ThenBy(x => x.DisplayTitle)
                .ToList();
        });
    }

    private static void AddRunKeyItems(RegistryKey rootKey, string subKeyPath, string location, List<StartupItem> list, HashSet<string> seen, bool forceDisabled = false)
    {
        try
        {
            using var key = rootKey.OpenSubKey(subKeyPath, false);
            if (key == null) return;

            foreach (var valName in key.GetValueNames())
            {
                var cmd = key.GetValue(valName)?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(cmd)) continue;

                string dedupeKey = $"{location}_{valName}";
                if (!seen.Add(dedupeKey)) continue;

                string exePath = ExtractExecutablePath(cmd);
                bool fileExists = string.IsNullOrWhiteSpace(exePath) || File.Exists(exePath);
                var (publisher, friendlyName) = ResolveMetadata(valName, exePath, cmd);

                bool isEnabled = !forceDisabled;
                if (isEnabled)
                {
                    // Check Windows Task Manager StartupApproved\Run registry state
                    if (IsDisabledInStartupApproved(rootKey, StartupApprovedRunPath, valName))
                    {
                        isEnabled = false;
                    }
                }

                list.Add(new StartupItem
                {
                    Name = valName,
                    FriendlyName = friendlyName,
                    Command = cmd,
                    ExePath = exePath,
                    Location = location,
                    IsEnabled = isEnabled,
                    IsFileMissing = !fileExists,
                    Impact = CalculateImpact(exePath, cmd),
                    Publisher = publisher
                });
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Error enumerating {location}\\{subKeyPath}: {ex.Message}");
        }
    }

    private static void AddStartupFolderItems(string folderPath, string location, List<StartupItem> list, HashSet<string> seen)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath)) return;

            foreach (var file in Directory.GetFiles(folderPath))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                string name = Path.GetFileNameWithoutExtension(file);

                if (ext == ".lnk" || ext == ".bat" || ext == ".cmd" || ext == ".disabled")
                {
                    string dedupeKey = $"{location}_{name}";
                    if (!seen.Add(dedupeKey)) continue;

                    bool isDisabled = ext == ".disabled";
                    if (!isDisabled)
                    {
                        if (IsDisabledInStartupApproved(Registry.CurrentUser, StartupApprovedFolderPath, Path.GetFileName(file)) ||
                            IsDisabledInStartupApproved(Registry.LocalMachine, StartupApprovedFolderPath, Path.GetFileName(file)))
                        {
                            isDisabled = true;
                        }
                    }

                    string exePath = file;
                    if (ext == ".lnk")
                    {
                        string target = ResolveShortcutTarget(file);
                        if (!string.IsNullOrWhiteSpace(target)) exePath = target;
                    }

                    bool fileExists = File.Exists(file) && (ext != ".lnk" || string.IsNullOrWhiteSpace(exePath) || File.Exists(exePath));
                    var (publisher, friendlyName) = ResolveMetadata(name, exePath, file);

                    list.Add(new StartupItem
                    {
                        Name = name,
                        FriendlyName = friendlyName,
                        Command = file,
                        ExePath = exePath,
                        Location = location,
                        IsEnabled = !isDisabled,
                        IsFileMissing = !fileExists,
                        Impact = BootImpact.Medium,
                        Publisher = publisher
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Error enumerating {location}: {ex.Message}");
        }
    }

    private static bool IsDisabledInStartupApproved(RegistryKey rootKey, string subKeyPath, string valName)
    {
        try
        {
            using var key = rootKey.OpenSubKey(subKeyPath, false);
            if (key == null) return false;

            var val = key.GetValue(valName);
            if (val is byte[] bytes && bytes.Length > 0)
            {
                // In Windows 10/11:
                // 0x02 = Enabled (or 0x01)
                // 0x03 or higher = Disabled by Task Manager
                return bytes[0] >= 0x03;
            }
        }
        catch { }
        return false;
    }

    private static bool SetStartupApprovedState(RegistryKey rootKey, string subKeyPath, string valName, bool enable)
    {
        try
        {
            using var key = rootKey.CreateSubKey(subKeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree);
            if (key == null) return false;

            byte[] current = key.GetValue(valName) as byte[] ?? new byte[12];
            if (current.Length < 12) Array.Resize(ref current, 12);

            current[0] = (byte)(enable ? 0x02 : 0x03);
            key.SetValue(valName, current, RegistryValueKind.Binary);
            return true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Could not set StartupApproved state for {valName}: {ex.Message}");
            return false;
        }
    }

    public static bool ToggleStartupItem(StartupItem item, bool enable)
    {
        try
        {
            if (item.Location.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase) || 
                item.Location.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase) ||
                item.Location.Contains("WOW64", StringComparison.OrdinalIgnoreCase))
            {
                bool isHkcu = item.Location.Contains("HKCU", StringComparison.OrdinalIgnoreCase);
                RegistryKey rootKey = isHkcu ? Registry.CurrentUser : Registry.LocalMachine;
                string subKey = item.Location.Contains("WOW64") ? Wow64RunKeyPath : RunKeyPath;

                // 1. Set Windows Native Task Manager state in StartupApproved\Run
                SetStartupApprovedState(rootKey, StartupApprovedRunPath, item.Name, enable);

                // 2. Synchronize with Deltempo backup registry key
                if (!enable)
                {
                    using var runKey = rootKey.OpenSubKey(subKey, true);
                    using var disKey = rootKey.CreateSubKey(RunDisabledKeyPath);
                    if (runKey != null && disKey != null)
                    {
                        var val = runKey.GetValue(item.Name);
                        if (val != null)
                        {
                            disKey.SetValue(item.Name, val);
                        }
                    }
                }
                else
                {
                    using var disKey = rootKey.OpenSubKey(RunDisabledKeyPath, true);
                    using var runKey = rootKey.CreateSubKey(subKey);
                    if (disKey != null && runKey != null)
                    {
                        var val = disKey.GetValue(item.Name);
                        if (val != null)
                        {
                            runKey.SetValue(item.Name, val);
                            disKey.DeleteValue(item.Name, false);
                        }
                    }
                }

                item.IsEnabled = enable;
                return true;
            }
            else if (item.Location.Contains("Startup"))
            {
                // Update StartupApproved\StartupFolder
                SetStartupApprovedState(Registry.CurrentUser, StartupApprovedFolderPath, Path.GetFileName(item.Command), enable);

                if (!enable && File.Exists(item.Command) && !item.Command.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                {
                    string target = item.Command + ".disabled";
                    File.Move(item.Command, target, true);
                    item.Command = target;
                    item.IsEnabled = false;
                    return true;
                }
                else if (enable && item.Command.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase) && File.Exists(item.Command))
                {
                    string target = item.Command[..^9];
                    File.Move(item.Command, target, true);
                    item.Command = target;
                    item.IsEnabled = true;
                    return true;
                }

                item.IsEnabled = enable;
                return true;
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Failed to toggle startup item '{item.Name}': {ex.Message}");
        }

        return false;
    }

    public static string ExtractExecutablePath(string rawCommand)
    {
        if (string.IsNullOrWhiteSpace(rawCommand)) return string.Empty;
        rawCommand = rawCommand.Trim();

        if (rawCommand.StartsWith("\""))
        {
            int nextQuote = rawCommand.IndexOf('"', 1);
            if (nextQuote > 1)
            {
                return rawCommand.Substring(1, nextQuote - 1);
            }
        }

        int exeIdx = rawCommand.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIdx > 0)
        {
            string candidate = rawCommand.Substring(0, exeIdx + 4).Trim('"', ' ');
            if (File.Exists(candidate)) return candidate;
        }

        int spaceIdx = rawCommand.IndexOf(' ');
        if (spaceIdx > 0)
        {
            string candidate = rawCommand[..spaceIdx].Trim('"', ' ');
            if (File.Exists(candidate)) return candidate;
        }

        return rawCommand.Trim('"', ' ');
    }

    private static (string Publisher, string FriendlyName) ResolveMetadata(string valName, string exePath, string rawCmd)
    {
        string publisher = "Unknown Publisher";
        string friendlyName = valName;

        try
        {
            if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
            {
                var vi = FileVersionInfo.GetVersionInfo(exePath);
                if (!string.IsNullOrWhiteSpace(vi.CompanyName)) publisher = vi.CompanyName.Trim();
                if (!string.IsNullOrWhiteSpace(vi.FileDescription)) friendlyName = vi.FileDescription.Trim();
            }
        }
        catch { }

        if (publisher == "Unknown Publisher")
        {
            publisher = InferPublisher(valName, rawCmd);
        }

        return (publisher, friendlyName);
    }

    private static string ResolveShortcutTarget(string lnkPath)
    {
        try
        {
            // Lightweight Windows shortcut target resolution
            using var stream = File.OpenRead(lnkPath);
            using var reader = new BinaryReader(stream);
            if (stream.Length < 0x4C) return string.Empty;

            stream.Seek(0x14, SeekOrigin.Begin);
            uint flags = reader.ReadUInt32();
            if ((flags & 0x02) == 0) return string.Empty; // HasLinkInfo flag

            stream.Seek(0x4C, SeekOrigin.Begin);
            if ((flags & 0x01) != 0) // HasLinkTargetIDList
            {
                uint idListSize = reader.ReadUInt16();
                stream.Seek(idListSize, SeekOrigin.Current);
            }

            long linkInfoPos = stream.Position;
            uint linkInfoSize = reader.ReadUInt32();
            if (linkInfoSize >= 0x1C)
            {
                stream.Seek(linkInfoPos + 0x10, SeekOrigin.Begin);
                uint localBasePathOffset = reader.ReadUInt32();
                if (localBasePathOffset > 0)
                {
                    stream.Seek(linkInfoPos + localBasePathOffset, SeekOrigin.Begin);
                    var chars = new List<char>();
                    while (stream.Position < stream.Length)
                    {
                        byte b = reader.ReadByte();
                        if (b == 0) break;
                        chars.Add((char)b);
                    }
                    return new string(chars.ToArray());
                }
            }
        }
        catch { }
        return string.Empty;
    }

    private static BootImpact CalculateImpact(string exePath, string cmd)
    {
        var lower = (cmd + " " + exePath).ToLowerInvariant();

        if (lower.Contains("electron") || lower.Contains("discord") || lower.Contains("spotify") ||
            lower.Contains("steam") || lower.Contains("epic") || lower.Contains("docker") ||
            lower.Contains("teams") || lower.Contains("slack"))
        {
            return BootImpact.High;
        }

        if (lower.EndsWith(".bat") || lower.EndsWith(".cmd") || lower.EndsWith(".ps1") ||
            lower.Contains("update") || lower.Contains("helper") || lower.Contains("service") || lower.Contains("sync"))
        {
            return BootImpact.Medium;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
            {
                long len = new FileInfo(exePath).Length;
                if (len > 40L * 1024 * 1024) return BootImpact.High;
                if (len > 10L * 1024 * 1024) return BootImpact.Medium;
            }
        }
        catch { }

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
        if (lower.Contains("nvidia")) return "NVIDIA Corporation";
        if (lower.Contains("intel")) return "Intel Corporation";
        if (lower.Contains("amd")) return "Advanced Micro Devices, Inc.";
        if (lower.Contains("logitech")) return "Logitech";
        if (lower.Contains("razer")) return "Razer Inc.";
        if (lower.Contains("docker")) return "Docker Inc.";
        return "Application Developer";
    }
}
