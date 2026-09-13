using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public enum BootImpact
{
    Low,
    Medium,
    High
}

public class StartupItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public string Name { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string ExePath { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty; // HKCU, HKLM, WOW64_HKLM, WOW64_HKCU, Startup Folder, Common Startup, Task Scheduler
    public string LocationDisplay => Location switch
    {
        "HKCU" => "Registry (Current User)",
        "HKLM" => "Registry (All Users)",
        "WOW64_HKLM" => "Registry (32-bit All Users)",
        "WOW64_HKCU" => "Registry (32-bit User)",
        "Startup Folder" => "Startup Folder (User)",
        "Common Startup" => "Startup Folder (All Users)",
        "Task Scheduler" => "Task Scheduler (Logon Task)",
        _ => Location
    };

    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set { if (_isEnabled != value) { _isEnabled = value; OnPropertyChanged(); } }
    }

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
    public bool IsProtected => StartupManagerService.IsProtectedStartupItem(this);
    public System.Windows.Visibility ProtectedBadgeVisibility => IsProtected ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    public bool CanCleanOrphaned => IsFileMissing && !IsProtected;
    public System.Windows.Visibility OrphanedCleanVisibility => CanCleanOrphaned ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    public bool CanReveal => !string.IsNullOrWhiteSpace(ExePath) && (File.Exists(ExePath) || Directory.Exists(Path.GetDirectoryName(ExePath) ?? ""));
    public System.Windows.Visibility RevealVisibility => CanReveal ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    public bool CanToggle => !IsProtected;

    // AI & Catalog Intelligence
    private string _description = string.Empty;
    public string Description
    {
        get => _description;
        set { if (_description != value) { _description = value; OnPropertyChanged(); } }
    }

    private StartupDisableVerdict _disableVerdict = StartupDisableVerdict.SafeToDisable;
    public StartupDisableVerdict DisableVerdict
    {
        get => _disableVerdict;
        set
        {
            if (_disableVerdict != value)
            {
                _disableVerdict = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisableVerdictDisplay));
                OnPropertyChanged(nameof(DisableBadgeColor));
                OnPropertyChanged(nameof(DisableBadgeBackground));
                OnPropertyChanged(nameof(FallbackGlyph));
            }
        }
    }

    private string _disableImpact = string.Empty;
    public string DisableImpact
    {
        get => _disableImpact;
        set { if (_disableImpact != value) { _disableImpact = value; OnPropertyChanged(); } }
    }

    private string _recommendation = string.Empty;
    public string Recommendation
    {
        get => _recommendation;
        set { if (_recommendation != value) { _recommendation = value; OnPropertyChanged(); } }
    }

    private bool _isAiEnriched;
    public bool IsAiEnriched
    {
        get => _isAiEnriched;
        set { if (_isAiEnriched != value) { _isAiEnriched = value; OnPropertyChanged(); } }
    }

    private string _aiProviderUsed = string.Empty;
    public string AiProviderUsed
    {
        get => _aiProviderUsed;
        set { if (_aiProviderUsed != value) { _aiProviderUsed = value; OnPropertyChanged(); } }
    }

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ExpandedVisibility));
            }
        }
    }
    public System.Windows.Visibility ExpandedVisibility => IsExpanded ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public string DisableVerdictDisplay => DisableVerdict switch
    {
        StartupDisableVerdict.DoNotDisable => "Essential / Keep",
        StartupDisableVerdict.Caution => "Caution / Sync",
        _ => "Safe to Disable"
    };

    public string DisableBadgeColor => DisableVerdict switch
    {
        StartupDisableVerdict.DoNotDisable => "#EF4444",
        StartupDisableVerdict.Caution => "#F59E0B",
        _ => "#10B981"
    };

    public string DisableBadgeBackground => DisableVerdict switch
    {
        StartupDisableVerdict.DoNotDisable => "#2A0E0E",
        StartupDisableVerdict.Caution => "#2A1E0D",
        _ => "#0D2818"
    };

    private System.Windows.Media.ImageSource? _appIcon;
    public System.Windows.Media.ImageSource? AppIcon
    {
        get => _appIcon;
        set
        {
            if (_appIcon != value)
            {
                _appIcon = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAppIcon));
            }
        }
    }
    public bool HasAppIcon => AppIcon != null;

    public string FallbackGlyph => DisableVerdict switch
    {
        StartupDisableVerdict.DoNotDisable => "\uE72E", // Shield
        StartupDisableVerdict.Caution => "\uE753",      // Cloud / Sync
        _ => "\uE71D"                                   // App Box
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

            // 7. Windows Task Scheduler Logon Tasks (Third-party background bloat)
            AddTaskSchedulerLogonItems(list, seen);

            return list
                .OrderByDescending(x => x.IsFileMissing)
                .ThenByDescending(x => x.Impact)
                .ThenBy(x => x.DisplayTitle)
                .ToList();
        });
    }

    public static void PopulateStartupIntelligence(StartupItem item)
    {
        var cached = StartupIntelligenceService.GetCachedReport(item);
        if (cached != null)
        {
            item.Description = cached.WhatIsIt;
            item.DisableVerdict = cached.Verdict;
            item.DisableImpact = cached.ImpactIfDisabled;
            item.Recommendation = cached.Recommendation;
            item.IsAiEnriched = cached.IsAiGenerated;
            item.AiProviderUsed = cached.ProviderUsed;
        }
        else
        {
            var entry = StartupIntelligenceCatalog.FindCatalogEntry(item.Name, item.ExePath, item.Command) ??
                        StartupIntelligenceCatalog.ClassifyUnknownStartup(item.Name, item.ExePath, item.Command, item.Publisher);

            item.Description = entry.WhatIsIt;
            item.DisableVerdict = entry.Verdict;
            item.DisableImpact = entry.ImpactIfDisabled;
            item.Recommendation = entry.Recommendation;
            item.IsAiEnriched = false;
            item.AiProviderUsed = "Built-in Catalog";
        }

        if (item.AppIcon == null)
        {
            item.AppIcon = AppIconService.GetAppIcon(item.ExePath, Path.GetDirectoryName(item.ExePath) ?? "", item.DisplayTitle);
        }
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

                var item = new StartupItem
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
                };
                PopulateStartupIntelligence(item);
                list.Add(item);
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

                    var item = new StartupItem
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
                    };
                    PopulateStartupIntelligence(item);
                    list.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Error enumerating {location}: {ex.Message}");
        }
    }

    private static void AddTaskSchedulerLogonItems(List<StartupItem> list, HashSet<string> seen)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = "/query /fo csv /v",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using var proc = Process.Start(psi);
            if (proc == null) return;

            string csvOutput = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(4000);

            if (string.IsNullOrWhiteSpace(csvOutput)) return;

            using var reader = new StringReader(csvOutput);
            string? headerLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine)) return;

            var headers = ParseCsvLine(headerLine);
            int taskNameIdx = headers.FindIndex(h => h.Equals("TaskName", StringComparison.OrdinalIgnoreCase));
            int triggerIdx = headers.FindIndex(h => h.Equals("Schedule Type", StringComparison.OrdinalIgnoreCase) || h.Equals("Task Type", StringComparison.OrdinalIgnoreCase));
            int taskToRunIdx = headers.FindIndex(h => h.Equals("Task To Run", StringComparison.OrdinalIgnoreCase));
            int statusIdx = headers.FindIndex(h => h.Equals("Status", StringComparison.OrdinalIgnoreCase));

            if (taskNameIdx < 0 || taskToRunIdx < 0) return;

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var cols = ParseCsvLine(line);
                if (cols.Count <= Math.Max(taskNameIdx, taskToRunIdx)) continue;

                string rawTaskName = cols[taskNameIdx].Trim();
                if (string.IsNullOrWhiteSpace(rawTaskName)) continue;

                // Exclude Microsoft core system tasks
                if (rawTaskName.StartsWith(@"\Microsoft\Windows", StringComparison.OrdinalIgnoreCase) ||
                    rawTaskName.StartsWith(@"\Microsoft\XblGameSave", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string trigger = triggerIdx >= 0 && triggerIdx < cols.Count ? cols[triggerIdx] : "";
                bool isLogonOrStartup = trigger.Contains("logon", StringComparison.OrdinalIgnoreCase) ||
                                        trigger.Contains("boot", StringComparison.OrdinalIgnoreCase) ||
                                        trigger.Contains("startup", StringComparison.OrdinalIgnoreCase);

                if (!isLogonOrStartup) continue;

                string cmd = cols[taskToRunIdx].Trim();
                if (string.IsNullOrWhiteSpace(cmd) || cmd.Equals("N/A", StringComparison.OrdinalIgnoreCase)) continue;

                string cleanName = rawTaskName.TrimStart('\\');
                string dedupeKey = $"TaskScheduler_{cleanName}";
                if (!seen.Add(dedupeKey)) continue;

                string exePath = ExtractExecutablePath(cmd);
                bool fileExists = string.IsNullOrWhiteSpace(exePath) || File.Exists(exePath);
                var (publisher, friendlyName) = ResolveMetadata(cleanName, exePath, cmd);

                string status = statusIdx >= 0 && statusIdx < cols.Count ? cols[statusIdx] : "Ready";
                bool isEnabled = !status.Equals("Disabled", StringComparison.OrdinalIgnoreCase);

                var item = new StartupItem
                {
                    Name = cleanName,
                    FriendlyName = friendlyName,
                    Command = cmd,
                    ExePath = exePath,
                    Location = "Task Scheduler",
                    IsEnabled = isEnabled,
                    IsFileMissing = !fileExists,
                    Impact = CalculateImpact(exePath, cmd),
                    Publisher = publisher
                };
                PopulateStartupIntelligence(item);
                list.Add(item);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Error enumerating Task Scheduler startup tasks: {ex.Message}");
        }
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString().Trim());
        return result;
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

    private static readonly HashSet<string> ProtectedStartupNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "SecurityHealth",
        "WindowsDefender",
        "MSPresenceing",
        "ctfmon",
        "cmd",
        "explorer"
    };

    public static bool IsProtectedStartupItem(StartupItem item)
    {
        if (ProtectedStartupNames.Contains(item.Name)) return true;

        var cmdLower = (item.Command + " " + item.ExePath).ToLowerInvariant();
        if (cmdLower.Contains("securityhealthsystray") ||
            cmdLower.Contains("smartscreen") ||
            cmdLower.Contains("msmpeng") ||
            cmdLower.Contains("windefend"))
        {
            return true;
        }

        return false;
    }

    public static bool ToggleStartupItem(StartupItem item, bool enable)
    {
        try
        {
            // Protect essential OS security and boot components from being disabled
            if (!enable && IsProtectedStartupItem(item))
            {
                Trace.WriteLine($"[Deltempo] Prevented disabling protected startup item: {item.Name}");
                return false;
            }

            if (item.Location.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase) ||
                item.Location.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase) ||
                item.Location.Contains("WOW64", StringComparison.OrdinalIgnoreCase))
            {
                bool isHkcu = item.Location.Contains("HKCU", StringComparison.OrdinalIgnoreCase);
                RegistryKey rootKey = isHkcu ? Registry.CurrentUser : Registry.LocalMachine;
                string subKey = item.Location.Contains("WOW64") ? Wow64RunKeyPath : RunKeyPath;

                // Save original values for rollback
                string backupValue = string.Empty;
                bool hadBackup = false;
                try
                {
                    using var backupKey = rootKey.OpenSubKey(enable ? RunDisabledKeyPath : subKey, false);
                    if (backupKey != null)
                    {
                        var val = backupKey.GetValue(item.Name);
                        if (val != null)
                        {
                            backupValue = val.ToString() ?? string.Empty;
                            hadBackup = true;
                        }
                    }
                }
                catch { }

                // 1. Set Windows Native Task Manager state in StartupApproved\Run
                if (!SetStartupApprovedState(rootKey, StartupApprovedRunPath, item.Name, enable))
                {
                    Trace.WriteLine($"[Deltempo] Could not set StartupApproved state for {item.Name}");
                    return false;
                }

                // 2. Synchronize with Deltempo backup registry key
                bool syncOk = false;
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
                            syncOk = true;
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
                            syncOk = true;
                        }
                    }
                }

                // Rollback if sync failed
                if (!syncOk)
                {
                    Trace.WriteLine($"[Deltempo] Registry sync failed for {item.Name}, rolling back StartupApproved state.");
                    SetStartupApprovedState(rootKey, StartupApprovedRunPath, item.Name, !enable);

                    // Restore backup value if available
                    if (hadBackup)
                    {
                        try
                        {
                            string restoreKey = enable ? RunDisabledKeyPath : subKey;
                            using var rk = rootKey.OpenSubKey(restoreKey, true);
                            rk?.SetValue(item.Name, backupValue);
                        }
                        catch { }
                    }
                    return false;
                }

                // Post-change verification: confirm StartupApproved state actually changed
                bool verifiedState = IsDisabledInStartupApproved(rootKey, StartupApprovedRunPath, item.Name) != enable;
                if (!verifiedState)
                {
                    Trace.WriteLine($"[Deltempo] Post-change verification failed for {item.Name}: StartupApproved state did not update. Rolling back.");
                    SetStartupApprovedState(rootKey, StartupApprovedRunPath, item.Name, !enable);
                    return false;
                }

                item.IsEnabled = enable;
                return true;
            }
            else if (item.Location.Contains("Startup"))
            {
                // Save original filename for rollback
                string originalPath = item.Command;
                string targetPath = item.Command;
                bool fileRenamed = false;

                // Update StartupApproved\StartupFolder
                SetStartupApprovedState(Registry.CurrentUser, StartupApprovedFolderPath, Path.GetFileName(item.Command), enable);

                if (!enable && File.Exists(item.Command) && !item.Command.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                {
                    targetPath = item.Command + ".disabled";
                    File.Move(item.Command, targetPath, true);
                    fileRenamed = true;
                }
                else if (enable && item.Command.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase) && File.Exists(item.Command))
                {
                    targetPath = item.Command[..^9];
                    File.Move(item.Command, targetPath, true);
                    fileRenamed = true;
                }

                // Rollback if file rename succeeded but StartupApproved write may have failed
                if (!fileRenamed)
                {
                    SetStartupApprovedState(Registry.CurrentUser, StartupApprovedFolderPath, Path.GetFileName(originalPath), !enable);
                }

                item.Command = targetPath;
                item.IsEnabled = enable;
                return true;
            }
            else if (item.Location == "Task Scheduler")
            {
                string action = enable ? "/enable" : "/disable";
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/change /tn \"{item.Name}\" {action}",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(3000);
                if (p?.ExitCode == 0)
                {
                    item.IsEnabled = enable;
                    return true;
                }
                return false;
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Failed to toggle startup item '{item.Name}': {ex.Message}");
        }

        return false;
    }

    public static bool RemoveOrphanedStartupItem(StartupItem item)
    {
        try
        {
            if (IsProtectedStartupItem(item)) return false;

            if (item.Location.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase) ||
                item.Location.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase) ||
                item.Location.Contains("WOW64", StringComparison.OrdinalIgnoreCase))
            {
                bool isHkcu = item.Location.Contains("HKCU", StringComparison.OrdinalIgnoreCase);
                RegistryKey rootKey = isHkcu ? Registry.CurrentUser : Registry.LocalMachine;
                string subKey = item.Location.Contains("WOW64") ? Wow64RunKeyPath : RunKeyPath;

                try
                {
                    using var key = rootKey.OpenSubKey(subKey, true);
                    key?.DeleteValue(item.Name, false);
                }
                catch { }

                try
                {
                    using var disKey = rootKey.OpenSubKey(RunDisabledKeyPath, true);
                    disKey?.DeleteValue(item.Name, false);
                }
                catch { }

                try
                {
                    using var approvedKey = rootKey.OpenSubKey(StartupApprovedRunPath, true);
                    approvedKey?.DeleteValue(item.Name, false);
                }
                catch { }

                return true;
            }
            else if (item.Location.Contains("Startup"))
            {
                if (File.Exists(item.Command))
                {
                    File.Delete(item.Command);
                }
                return true;
            }
            else if (item.Location == "Task Scheduler")
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/delete /tn \"{item.Name}\" /f",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(3000);
                return p?.ExitCode == 0;
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Failed to remove orphaned item {item.Name}: {ex.Message}");
        }
        return false;
    }

    public static double CalculateEstimatedBootDelaySeconds(IEnumerable<StartupItem> items)
    {
        double totalSeconds = 0;
        foreach (var item in items.Where(x => x.IsEnabled && !x.IsFileMissing))
        {
            totalSeconds += item.Impact switch
            {
                BootImpact.High => 2.8,
                BootImpact.Medium => 1.1,
                _ => 0.3
            };
        }
        return Math.Round(totalSeconds, 1);
    }

    public static List<StartupItem> OptimizeBoot(IEnumerable<StartupItem> items)
    {
        var disabledList = new List<StartupItem>();
        foreach (var item in items)
        {
            if (!item.IsEnabled || item.IsProtected || item.IsFileMissing) continue;

            if (item.Impact == BootImpact.High ||
                (item.Impact == BootImpact.Medium && (item.Name.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
                                                      item.Command.Contains("update", StringComparison.OrdinalIgnoreCase))))
            {
                if (ToggleStartupItem(item, false))
                {
                    disabledList.Add(item);
                }
            }
        }
        return disabledList;
    }

    public static string ExtractExecutablePath(string rawCommand)
    {
        if (string.IsNullOrWhiteSpace(rawCommand)) return string.Empty;
        rawCommand = rawCommand.Trim();

        if (rawCommand.StartsWith('"'))
        {
            int nextQuote = rawCommand.IndexOf('"', 1);
            if (nextQuote > 1)
            {
                return rawCommand[1..nextQuote];
            }
        }

        int exeIdx = rawCommand.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIdx > 0)
        {
            string candidate = rawCommand[..(exeIdx + 4)].Trim('"', ' ');
            if (File.Exists(candidate)) return candidate;
        }

        int spaceIdx = rawCommand.IndexOf(' ');
        if (spaceIdx > 0)
        {
            string candidate = rawCommand[..spaceIdx].Trim('"', ' ');
            if (File.Exists(candidate)) return candidate;
        }

        return rawCommand;
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

    /// <summary>
    /// Binary parser for Shell Link (.lnk) files to extract local basePath without external COM dependencies.
    /// </summary>
    private static string ResolveShortcutTarget(string lnkPath)
    {
        try
        {
            using var stream = File.OpenRead(lnkPath);
            using var reader = new BinaryReader(stream);

            if (stream.Length < 0x4C) return string.Empty;

            uint headerSize = reader.ReadUInt32();
            if (headerSize != 0x4C) return string.Empty;

            byte[] clsid = reader.ReadBytes(16);
            uint flags = reader.ReadUInt32();

            stream.Seek(0x4C, SeekOrigin.Begin);

            // HasLinkTargetIDList flag = 0x01
            if ((flags & 0x01) != 0)
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
                    List<char> chars = [];
                    while (stream.Position < stream.Length)
                    {
                        byte b = reader.ReadByte();
                        if (b == 0) break;
                        chars.Add((char)b);
                    }
                    return new string([.. chars]);
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
