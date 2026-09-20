using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public enum LeftoverType
{
    File,
    Directory,
    RegistryKey,
    RegistryValue,
    Shortcut,
    Service,
    ScheduledTask
}

public enum LeftoverConfidence
{
    High,    // Guaranteed match (inside install directory, exact Software key, exact shortcut)
    Medium,  // High probability (matching AppData / ProgramData / Registry folder)
    Low      // Requires manual confirmation
}

public class LeftoverItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public LeftoverType Type { get; set; }
    public string PathOrKey { get; set; } = string.Empty;
    public string SubKeyOrValueName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string FormattedSize => SizeBytes > 0 ? TargetFolderInfo.FormatBytes(SizeBytes) : string.Empty;
    public LeftoverConfidence Confidence { get; set; } = LeftoverConfidence.High;
    public bool IsSelected { get; set; } = true;

    public string TypeBadge => Type switch
    {
        LeftoverType.File => "File",
        LeftoverType.Directory => "Folder",
        LeftoverType.RegistryKey => "Registry Key",
        LeftoverType.RegistryValue => "Registry Value",
        LeftoverType.Shortcut => "Shortcut",
        LeftoverType.Service => "Service",
        LeftoverType.ScheduledTask => "Task",
        _ => "Item"
    };

    public string IconGlyph => Type switch
    {
        LeftoverType.File => "\uE8A5",
        LeftoverType.Directory => "\uE8B7",
        LeftoverType.RegistryKey => "\uE74C",
        LeftoverType.RegistryValue => "\uE8A5",
        LeftoverType.Shortcut => "\uE71B",
        LeftoverType.Service => "\uE9F5",
        LeftoverType.ScheduledTask => "\uE823",
        _ => "\uE74D"
    };
}

public class RootScanResult
{
    public string AppName { get; set; } = string.Empty;
    public IReadOnlyList<LeftoverItem> Items { get; set; } = Array.Empty<LeftoverItem>();
    public long TotalSizeBytes => Items.Where(i => i.IsSelected).Sum(i => i.SizeBytes);
    public string FormattedTotalSize => TargetFolderInfo.FormatBytes(TotalSizeBytes);
    public int FileFolderCount => Items.Count(i => i.Type == LeftoverType.File || i.Type == LeftoverType.Directory);
    public int RegistryCount => Items.Count(i => i.Type == LeftoverType.RegistryKey || i.Type == LeftoverType.RegistryValue);
    public int ShortcutCount => Items.Count(i => i.Type == LeftoverType.Shortcut);
    public int SystemCount => Items.Count(i => i.Type == LeftoverType.Service || i.Type == LeftoverType.ScheduledTask);
}

public static class RootLeftoverScannerService
{
    private static readonly HashSet<string> ProtectedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows", "System32", "SysWOW64", "Program Files", "Program Files (x86)",
        "Users", "Default", "Public", "Microsoft", "AppData", "Local", "Roaming",
        "LocalLow", "ProgramData", "Common Files", "Windows Defender", "SoftwareDistribution",
        "Documents", "Desktop", "Downloads", "Pictures", "Music", "Videos", "Saved Games",
        "dotnet", "Microsoft.NET", "WindowsApps", "Windows Defender Advanced Threat Protection",
        "Microsoft Visual Studio", "Package Cache", "Microsoft SDKs", "Windows Kits"
    };

    private static readonly HashSet<string> ProtectedRegistryRootNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft", "Windows", "Classes", "Clients", "Policies", "RegisteredApplications",
        "Security", "System", "SYSTEM", "HARDWARE", "SAM", ".NETFramework", ".NET",
        "Microsoft .NET Framework", "Windows Defender", "Windows Mail", "Windows Media Player"
    };

    /// <summary>
    /// Scans the system across file system, registry, shortcuts, and services for root leftovers of the specified application.
    /// </summary>
    public static async Task<RootScanResult> ScanAppTracesAsync(InstalledAppItem app, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var items = new List<LeftoverItem>();
            if (string.IsNullOrWhiteSpace(app.DisplayName) || app.DisplayName.Length < 2)
            {
                return new RootScanResult { AppName = app.DisplayName, Items = items };
            }

            // 1. Scan File System Leftovers
            ScanFileSystemLeftovers(app, items);

            // 2. Scan Windows Registry Leftovers
            ScanRegistryLeftovers(app, items);

            // 3. Scan Shortcuts (Start Menu & Desktop)
            ScanShortcutLeftovers(app, items);

            // 4. Scan Services & Scheduled Tasks
            ScanServiceAndTaskLeftovers(app, items);

            // 5. Scan Explorer Context Menu & Shell Extension Handlers
            ScanShellExtensionLeftovers(app, items);

            // 6. Scan Windows Firewall Rules
            ScanFirewallRules(app, items);

            // 7. Scan URL Protocol Schemes
            ScanProtocolSchemeLeftovers(app, items);

            // Deduplicate items by PathOrKey + Type
            var distinctItems = items
                .GroupBy(i => $"{i.Type}_{i.PathOrKey}_{i.SubKeyOrValueName}", StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            return new RootScanResult
            {
                AppName = app.DisplayName,
                Items = distinctItems
            };
        }, ct);
    }

    private static void ScanFileSystemLeftovers(InstalledAppItem app, List<LeftoverItem> items)
    {
        // Vector 1A: Check explicit InstallLocation
        if (!string.IsNullOrWhiteSpace(app.InstallLocation) &&
            Directory.Exists(app.InstallLocation) &&
            InstalledAppService.IsSafeToDeleteResidual(app.InstallLocation))
        {
            long size = CalculateDirectorySizeSafe(app.InstallLocation);
            items.Add(new LeftoverItem
            {
                Type = LeftoverType.Directory,
                PathOrKey = app.InstallLocation,
                Description = "Main Installation Directory",
                SizeBytes = size,
                Confidence = LeftoverConfidence.High,
                IsSelected = true
            });
        }

        // Vector 1B: Search candidate roots
        var candidateRoots = new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VirtualStore", "Program Files"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VirtualStore", "Program Files (x86)")
        };

        string cleanName = SanitizeIdentifier(app.DisplayName);
        string cleanPublisher = SanitizeIdentifier(app.Publisher);

        foreach (var root in candidateRoots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;

            // Pattern: Root\<App>
            CheckAndAddDirectory(Path.Combine(root, app.DisplayName), "Application Cache/Data Folder", LeftoverConfidence.Medium, items);
            if (!string.Equals(cleanName, app.DisplayName, StringComparison.OrdinalIgnoreCase))
            {
                CheckAndAddDirectory(Path.Combine(root, cleanName), "Application Data Folder", LeftoverConfidence.Medium, items);
            }

            // Pattern: Root\<Publisher>\<App>
            if (!string.IsNullOrWhiteSpace(app.Publisher) &&
                app.Publisher.Length >= 3 &&
                !ProtectedDirectoryNames.Contains(app.Publisher))
            {
                CheckAndAddDirectory(Path.Combine(root, app.Publisher, app.DisplayName), "Publisher Application Data Folder", LeftoverConfidence.High, items);
                if (!string.Equals(cleanPublisher, app.Publisher, StringComparison.OrdinalIgnoreCase))
                {
                    CheckAndAddDirectory(Path.Combine(root, cleanPublisher, cleanName), "Publisher Application Folder", LeftoverConfidence.High, items);
                }
            }
        }
    }

    private static void CheckAndAddDirectory(string dirPath, string desc, LeftoverConfidence confidence, List<LeftoverItem> items)
    {
        try
        {
            if (Directory.Exists(dirPath) && InstalledAppService.IsSafeToDeleteResidual(dirPath))
            {
                // Double check it's not already added
                if (!items.Any(i => string.Equals(i.PathOrKey, dirPath, StringComparison.OrdinalIgnoreCase)))
                {
                    long size = CalculateDirectorySizeSafe(dirPath);
                    items.Add(new LeftoverItem
                    {
                        Type = LeftoverType.Directory,
                        PathOrKey = dirPath,
                        Description = desc,
                        SizeBytes = size,
                        Confidence = confidence,
                        IsSelected = confidence != LeftoverConfidence.Low
                    });
                }
            }
        }
        catch { }
    }

    private static void ScanRegistryLeftovers(InstalledAppItem app, List<LeftoverItem> items)
    {
        string cleanName = SanitizeIdentifier(app.DisplayName);
        string cleanPublisher = SanitizeIdentifier(app.Publisher);

        // Vector 2A: Software Keys in HKCU & HKLM
        CheckSoftwareKey(Registry.CurrentUser, @"Software", app.DisplayName, app.Publisher, items, "HKCU");
        CheckSoftwareKey(Registry.LocalMachine, @"SOFTWARE", app.DisplayName, app.Publisher, items, "HKLM");
        CheckSoftwareKey(Registry.LocalMachine, @"SOFTWARE\WOW6432Node", app.DisplayName, app.Publisher, items, "HKLM (32-bit)");

        // Vector 2B: App Paths
        string exeName = GetAppExeName(app);
        if (!string.IsNullOrWhiteSpace(exeName))
        {
            string appPathKey = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{exeName}";
            CheckSpecificRegistryKey(Registry.LocalMachine, appPathKey, "Application Execution Path Registration", LeftoverConfidence.High, items, "HKLM");
            CheckSpecificRegistryKey(Registry.CurrentUser, appPathKey, "User App Execution Path Registration", LeftoverConfidence.High, items, "HKCU");
        }

        // Vector 2C: Startup Run Entries
        ScanRegistryRunKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", app, items, "HKCU");
        ScanRegistryRunKey(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", app, items, "HKLM");

        // Vector 2D: Uninstall Keys (if orphaned or left behind)
        if (!string.IsNullOrWhiteSpace(app.RegistryKeyPath))
        {
            items.Add(new LeftoverItem
            {
                Type = LeftoverType.RegistryKey,
                PathOrKey = app.RegistryKeyPath,
                Description = "Windows Uninstall Entry",
                Confidence = LeftoverConfidence.High,
                IsSelected = true
            });
        }
    }

    private static void CheckSoftwareKey(RegistryKey rootKey, string baseSubKey, string appName, string publisher, List<LeftoverItem> items, string hiveName)
    {
        try
        {
            using var baseKey = rootKey.OpenSubKey(baseSubKey);
            if (baseKey == null) return;

            // Direct App subkey: Base\<App>
            if (!ProtectedRegistryRootNames.Contains(appName))
            {
                using var directAppKey = baseKey.OpenSubKey(appName);
                if (directAppKey != null)
                {
                    items.Add(new LeftoverItem
                    {
                        Type = LeftoverType.RegistryKey,
                        PathOrKey = $@"{hiveName}\{baseSubKey}\{appName}",
                        Description = $"{hiveName} Application Configuration Tree",
                        Confidence = LeftoverConfidence.Medium,
                        IsSelected = true
                    });
                }
            }

            // Publisher subkey: Base\<Publisher>\<App>
            if (!string.IsNullOrWhiteSpace(publisher) &&
                publisher.Length >= 3 &&
                !ProtectedRegistryRootNames.Contains(publisher))
            {
                using var pubKey = baseKey.OpenSubKey(publisher);
                if (pubKey != null)
                {
                    using var pubAppKey = pubKey.OpenSubKey(appName);
                    if (pubAppKey != null)
                    {
                        items.Add(new LeftoverItem
                        {
                            Type = LeftoverType.RegistryKey,
                            PathOrKey = $@"{hiveName}\{baseSubKey}\{publisher}\{appName}",
                            Description = $"{hiveName} Publisher Application Tree",
                            Confidence = LeftoverConfidence.High,
                            IsSelected = true
                        });
                    }
                }
            }
        }
        catch { }
    }

    private static void CheckSpecificRegistryKey(RegistryKey rootKey, string subKeyPath, string desc, LeftoverConfidence confidence, List<LeftoverItem> items, string hiveName)
    {
        try
        {
            using var subKey = rootKey.OpenSubKey(subKeyPath);
            if (subKey != null)
            {
                items.Add(new LeftoverItem
                {
                    Type = LeftoverType.RegistryKey,
                    PathOrKey = $@"{hiveName}\{subKeyPath}",
                    Description = desc,
                    Confidence = confidence,
                    IsSelected = true
                });
            }
        }
        catch { }
    }

    private static void ScanRegistryRunKey(RegistryKey rootKey, string runSubKeyPath, InstalledAppItem app, List<LeftoverItem> items, string hiveName)
    {
        try
        {
            using var runKey = rootKey.OpenSubKey(runSubKeyPath);
            if (runKey == null) return;

            foreach (var valName in runKey.GetValueNames())
            {
                string valData = runKey.GetValue(valName)?.ToString() ?? string.Empty;

                bool matchesName = string.Equals(valName, app.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                                  valName.Contains(app.DisplayName, StringComparison.OrdinalIgnoreCase);

                bool matchesPath = !string.IsNullOrWhiteSpace(app.InstallLocation) &&
                                   valData.Contains(app.InstallLocation, StringComparison.OrdinalIgnoreCase);

                if (matchesName || matchesPath)
                {
                    items.Add(new LeftoverItem
                    {
                        Type = LeftoverType.RegistryValue,
                        PathOrKey = $@"{hiveName}\{runSubKeyPath}",
                        SubKeyOrValueName = valName,
                        Description = $"Startup Boot Entry: {valName}",
                        Confidence = matchesPath ? LeftoverConfidence.High : LeftoverConfidence.Medium,
                        IsSelected = true
                    });
                }
            }
        }
        catch { }
    }

    private static void ScanShortcutLeftovers(InstalledAppItem app, List<LeftoverItem> items)
    {
        var shortcutFolders = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
        };

        foreach (var folder in shortcutFolders)
        {
            if (!Directory.Exists(folder)) continue;

            try
            {
                // Check direct folder match in Start Menu (e.g. Programs\<App> or Programs\<Publisher>)
                var appDir = Path.Combine(folder, app.DisplayName);
                if (Directory.Exists(appDir) && InstalledAppService.IsSafeToDeleteResidual(appDir))
                {
                    items.Add(new LeftoverItem
                    {
                        Type = LeftoverType.Shortcut,
                        PathOrKey = appDir,
                        Description = "Start Menu Programs Folder",
                        Confidence = LeftoverConfidence.High,
                        IsSelected = true
                    });
                }

                // Check .lnk files matching app name
                var lnkFiles = Directory.GetFiles(folder, "*.lnk", SearchOption.AllDirectories);
                foreach (var lnk in lnkFiles)
                {
                    string lnkName = Path.GetFileNameWithoutExtension(lnk);
                    if (lnkName.Contains(app.DisplayName, StringComparison.OrdinalIgnoreCase))
                    {
                        items.Add(new LeftoverItem
                        {
                            Type = LeftoverType.Shortcut,
                            PathOrKey = lnk,
                            Description = "Application Shortcut Link",
                            Confidence = LeftoverConfidence.High,
                            IsSelected = true
                        });
                    }
                }
            }
            catch { }
        }
    }

    private static void ScanServiceAndTaskLeftovers(InstalledAppItem app, List<LeftoverItem> items)
    {
        // Scan Windows Services matching install path or app name
        try
        {
            using var servicesKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
            if (servicesKey != null && !string.IsNullOrWhiteSpace(app.InstallLocation))
            {
                foreach (var svcName in servicesKey.GetSubKeyNames())
                {
                    try
                    {
                        using var svc = servicesKey.OpenSubKey(svcName);
                        if (svc == null) continue;

                        string imagePath = svc.GetValue("ImagePath")?.ToString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(imagePath) &&
                            imagePath.Contains(app.InstallLocation, StringComparison.OrdinalIgnoreCase))
                        {
                            items.Add(new LeftoverItem
                            {
                                Type = LeftoverType.Service,
                                PathOrKey = svcName,
                                Description = $"Windows Service: {svcName}",
                                Confidence = LeftoverConfidence.High,
                                IsSelected = true
                            });
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    private static void ScanShellExtensionLeftovers(InstalledAppItem app, List<LeftoverItem> items)
    {
        string[] shellKeys =
        {
            @"*\shellex\ContextMenuHandlers",
            @"Directory\shellex\ContextMenuHandlers",
            @"Directory\Background\shellex\ContextMenuHandlers",
            @"Drive\shellex\ContextMenuHandlers",
            @"AllFilesystemObjects\shellex\ContextMenuHandlers"
        };

        foreach (var relKey in shellKeys)
        {
            try
            {
                using var baseKey = Registry.ClassesRoot.OpenSubKey(relKey);
                if (baseKey == null) continue;

                foreach (var handlerName in baseKey.GetSubKeyNames())
                {
                    bool matchesName = handlerName.Contains(app.DisplayName, StringComparison.OrdinalIgnoreCase);
                    string clsid = string.Empty;

                    using var subKey = baseKey.OpenSubKey(handlerName);
                    if (subKey != null)
                    {
                        clsid = subKey.GetValue(null)?.ToString() ?? string.Empty;
                    }

                    bool matchesDll = false;
                    if (!string.IsNullOrWhiteSpace(clsid) && clsid.StartsWith("{") && !string.IsNullOrWhiteSpace(app.InstallLocation))
                    {
                        try
                        {
                            using var clsidKey = Registry.ClassesRoot.OpenSubKey($@"CLSID\{clsid}\InprocServer32");
                            string dllPath = clsidKey?.GetValue(null)?.ToString() ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(dllPath) && dllPath.Contains(app.InstallLocation, StringComparison.OrdinalIgnoreCase))
                            {
                                matchesDll = true;
                                items.Add(new LeftoverItem
                                {
                                    Type = LeftoverType.RegistryKey,
                                    PathOrKey = $@"HKCR\CLSID\{clsid}",
                                    Description = $"Shell Extension InProc COM Server: {handlerName}",
                                    Confidence = LeftoverConfidence.High,
                                    IsSelected = true
                                });
                            }
                        }
                        catch { }
                    }

                    if (matchesName || matchesDll)
                    {
                        items.Add(new LeftoverItem
                        {
                            Type = LeftoverType.RegistryKey,
                            PathOrKey = $@"HKCR\{relKey}\{handlerName}",
                            Description = $"Explorer Context Menu Handler: {handlerName}",
                            Confidence = LeftoverConfidence.High,
                            IsSelected = true
                        });
                    }
                }
            }
            catch { }
        }
    }

    private static void ScanFirewallRules(InstalledAppItem app, List<LeftoverItem> items)
    {
        if (string.IsNullOrWhiteSpace(app.InstallLocation) && string.IsNullOrWhiteSpace(app.DisplayName)) return;

        try
        {
            using var fwKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\FirewallRules");
            if (fwKey == null) return;

            foreach (var ruleName in fwKey.GetValueNames())
            {
                string ruleData = fwKey.GetValue(ruleName)?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(ruleData)) continue;

                bool matchesPath = !string.IsNullOrWhiteSpace(app.InstallLocation) &&
                                   ruleData.Contains(app.InstallLocation, StringComparison.OrdinalIgnoreCase);

                bool matchesName = !string.IsNullOrWhiteSpace(app.DisplayName) &&
                                   app.DisplayName.Length >= 4 &&
                                   ruleData.Contains($"Name={app.DisplayName}|", StringComparison.OrdinalIgnoreCase);

                if (matchesPath || matchesName)
                {
                    items.Add(new LeftoverItem
                    {
                        Type = LeftoverType.RegistryValue,
                        PathOrKey = @"HKLM\SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\FirewallRules",
                        SubKeyOrValueName = ruleName,
                        Description = $"Windows Firewall Rule: {ruleName}",
                        Confidence = matchesPath ? LeftoverConfidence.High : LeftoverConfidence.Medium,
                        IsSelected = true
                    });
                }
            }
        }
        catch { }
    }

    private static void ScanProtocolSchemeLeftovers(InstalledAppItem app, List<LeftoverItem> items)
    {
        if (string.IsNullOrWhiteSpace(app.InstallLocation) && string.IsNullOrWhiteSpace(app.DisplayName)) return;

        try
        {
            using var crKey = Registry.ClassesRoot;
            if (crKey == null) return;

            // Check potential protocol schemes matching app name
            string scheme = app.DisplayName.ToLowerInvariant().Replace(" ", "");
            if (scheme.Length >= 3 && !ProtectedRegistryRootNames.Contains(scheme))
            {
                using var schemeKey = crKey.OpenSubKey(scheme);
                if (schemeKey != null && schemeKey.GetValue("URL Protocol") != null)
                {
                    using var cmdKey = schemeKey.OpenSubKey(@"shell\open\command");
                    string cmdVal = cmdKey?.GetValue(null)?.ToString() ?? string.Empty;

                    bool matches = (!string.IsNullOrWhiteSpace(app.InstallLocation) && cmdVal.Contains(app.InstallLocation, StringComparison.OrdinalIgnoreCase)) ||
                                   cmdVal.Contains(app.DisplayName, StringComparison.OrdinalIgnoreCase);

                    if (matches)
                    {
                        items.Add(new LeftoverItem
                        {
                            Type = LeftoverType.RegistryKey,
                            PathOrKey = $@"HKCR\{scheme}",
                            Description = $"Custom URL Protocol Scheme: {scheme}://",
                            Confidence = LeftoverConfidence.High,
                            IsSelected = true
                        });
                    }
                }
            }
        }
        catch { }
    }

    public static long CalculateDirectorySizeSafe(string dirPath)
    {
        if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath)) return 0;

        try
        {
            var dirInfo = new DirectoryInfo(dirPath);
            return dirInfo.EnumerateFiles("*", SearchOption.AllDirectories)
                          .Sum(fi => fi.Length);
        }
        catch
        {
            return 0;
        }
    }

    private static string SanitizeIdentifier(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var clean = input.Replace("(", "").Replace(")", "").Replace("[", "").Replace("]", "").Trim();
        return clean;
    }

    private static string GetAppExeName(InstalledAppItem app)
    {
        if (!string.IsNullOrWhiteSpace(app.DisplayIcon))
        {
            string clean = app.DisplayIcon.Trim('\"', '\'');
            int commaIdx = clean.IndexOf(',');
            if (commaIdx > 0) clean = clean.Substring(0, commaIdx);
            if (clean.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFileName(clean);
            }
        }

        if (!string.IsNullOrWhiteSpace(app.UninstallString))
        {
            string clean = app.UninstallString.Trim('\"', '\'');
            int spaceIdx = clean.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (spaceIdx > 0)
            {
                string exePath = clean.Substring(0, spaceIdx + 4).Trim('\"');
                return Path.GetFileName(exePath);
            }
        }

        return string.Empty;
    }
}
