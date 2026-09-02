using Microsoft.Win32;
using System.IO;

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

    public static async Task<List<StartupItem>> GetStartupItemsAsync()
    {
        return await Task.Run(() =>
        {
            var list = new List<StartupItem>();

            // 1. Current User Run Key
            try
            {
                using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                if (runKey != null)
                {
                    foreach (var valName in runKey.GetValueNames())
                    {
                        var cmd = runKey.GetValue(valName)?.ToString() ?? "";
                        list.Add(new StartupItem
                        {
                            Name = valName,
                            Command = cmd,
                            Location = "HKCU",
                            IsEnabled = true,
                            Impact = CalculateImpact(cmd),
                            Publisher = InferPublisher(valName, cmd)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }

            // 2. Current User Disabled Key (Deltempo backup)
            try
            {
                using var disKey = Registry.CurrentUser.OpenSubKey(RunDisabledKeyPath);
                if (disKey != null)
                {
                    foreach (var valName in disKey.GetValueNames())
                    {
                        var cmd = disKey.GetValue(valName)?.ToString() ?? "";
                        list.Add(new StartupItem
                        {
                            Name = valName,
                            Command = cmd,
                            Location = "HKCU",
                            IsEnabled = false,
                            Impact = CalculateImpact(cmd),
                            Publisher = InferPublisher(valName, cmd)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }

            // 3. User Startup Folder
            try
            {
                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                if (Directory.Exists(startupFolder))
                {
                    foreach (var file in Directory.GetFiles(startupFolder))
                    {
                        string ext = Path.GetExtension(file).ToLowerInvariant();
                        if (ext == ".lnk" || ext == ".bat" || ext == ".cmd")
                        {
                            string name = Path.GetFileNameWithoutExtension(file);
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
                            string name = Path.GetFileNameWithoutExtension(file);
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }

            return list;
        });
    }

    public static bool ToggleStartupItem(StartupItem item, bool enable)
    {
        try
        {
            if (item.Location == "HKCU")
            {
                if (!enable)
                {
                    // Move from Run to Run_Deltempo_Disabled
                    using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                    using var disKey = Registry.CurrentUser.CreateSubKey(RunDisabledKeyPath);

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
                    // Move back from Run_Deltempo_Disabled to Run
                    using var disKey = Registry.CurrentUser.OpenSubKey(RunDisabledKeyPath, true);
                    using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath);

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
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
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
