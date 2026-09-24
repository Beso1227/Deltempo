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
    ScheduledTask,
    EnvironmentPath
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
    public string? TargetPath { get; set; }
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
        LeftoverType.EnvironmentPath => "Environment PATH",
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
        LeftoverType.EnvironmentPath => "\uE756",
        _ => "\uE74D"
    };
}

public class AppTraceSnapshot
{
    public string AppName { get; set; } = string.Empty;
    public string InstallLocation { get; set; } = string.Empty;
    public HashSet<string> ExistingFiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> ExistingRegistryKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
}

public class RootScanResult
{
    public string AppName { get; set; } = string.Empty;
    public IReadOnlyList<LeftoverItem> Items { get; set; } = Array.Empty<LeftoverItem>();
    public long TotalSizeBytes => Items.Where(i => i.IsSelected).Sum(i => i.SizeBytes);
    public string FormattedTotalSize => TargetFolderInfo.FormatBytes(TotalSizeBytes);
    public int FileFolderCount => Items.Count(i => i.Type == LeftoverType.File || i.Type == LeftoverType.Directory);
    public int RegistryCount => Items.Count(i => i.Type == LeftoverType.RegistryKey || i.Type == LeftoverType.RegistryValue || i.Type == LeftoverType.EnvironmentPath);
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
    /// Captures a lightweight pre-uninstall snapshot of files and registry keys associated with the application.
    /// Used for differential residual comparison after official uninstaller execution.
    /// </summary>
    public static async Task<AppTraceSnapshot> CreateSnapshotAsync(InstalledAppItem app, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var snapshot = new AppTraceSnapshot
            {
                AppName = app.DisplayName,
                InstallLocation = app.InstallLocation
            };

            if (!string.IsNullOrWhiteSpace(app.InstallLocation) && Directory.Exists(app.InstallLocation))
            {
                try
                {
                    foreach (var file in Directory.EnumerateFiles(app.InstallLocation, "*", SearchOption.AllDirectories))
                    {
                        if (ct.IsCancellationRequested) break;
                        snapshot.ExistingFiles.Add(file);
                    }
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(app.RegistryKeyPath))
            {
                snapshot.ExistingRegistryKeys.Add(app.RegistryKeyPath);
            }

            return snapshot;
        }, ct);
    }

    /// <summary>
    /// Scans the system across 12 distinct vectors for root leftovers of the specified application.
    /// Supports pre-uninstall snapshot comparison to identify unremoved residuals with high precision.
    /// </summary>
    public static async Task<RootScanResult> ScanAppTracesAsync(InstalledAppItem app, AppTraceSnapshot? snapshot = null, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var items = new List<LeftoverItem>();
            if (string.IsNullOrWhiteSpace(app.DisplayName) || app.DisplayName.Length < 2)
            {
                return new RootScanResult { AppName = app.DisplayName, Items = items };
            }

            // 1. Scan File System Leftovers (Candidate roots + Snapshot diffs)
            ScanFileSystemLeftovers(app, items, snapshot);

            // 2. Scan Windows Registry Leftovers (Software, App Paths, Run)
            ScanRegistryLeftovers(app, items);

            // 3. Scan Shortcuts (Start Menu & Desktop)
            ScanShortcutLeftovers(app, items);

            // 4. Scan Services, Drivers & Scheduled Tasks
            ScanServiceAndTaskLeftovers(app, items);

            // 5. Scan Explorer Context Menu & Shell Extension Handlers
            ScanShellExtensionLeftovers(app, items);

            // 6. Scan Windows Firewall Rules
            ScanFirewallRules(app, items);

            // 7. Scan URL Protocol Schemes
            ScanProtocolSchemeLeftovers(app, items);

            // 8. Scan COM CLSID & TypeLib Registrations
            ScanComClsidLeftovers(app, items);

            // 9. Scan Environment PATH Variable Remnants
            ScanEnvironmentPathLeftovers(app, items);

            // 10. Scan Shared DLL Registrations
            ScanSharedDllLeftovers(app, items);

            // 11. Scan Windows Event Log Sources & Crash Dumps
            ScanEventLogAndCrashDumpLeftovers(app, items);

            // 12. Scan AppX / MSIX Modern Container Residues
            ScanAppxPackageStateLeftovers(app, items);

            // Deduplicate items by PathOrKey + SubKeyOrValueName + TargetPath + Type
            var distinctItems = items
                .GroupBy(i => $"{i.Type}_{i.PathOrKey}_{i.SubKeyOrValueName}_{i.TargetPath}", StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            return new RootScanResult
            {
                AppName = app.DisplayName,
                Items = distinctItems
            };
        }, ct);
    }

    private static void ScanFileSystemLeftovers(InstalledAppItem app, List<LeftoverItem> items, AppTraceSnapshot? snapshot)
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
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VirtualStore", "Program Files (x86)"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Downloaded Installations"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Downloaded Installations"),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Public")
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

        // Vector 1C: Snapshot differential comparison
        if (snapshot != null && snapshot.ExistingFiles.Count > 0)
        {
            foreach (var snapFile in snapshot.ExistingFiles)
            {
                if (File.Exists(snapFile) && !items.Any(i => string.Equals(i.PathOrKey, snapFile, StringComparison.OrdinalIgnoreCase)))
                {
                    long size = 0;
                    try { size = new FileInfo(snapFile).Length; } catch { }

                    items.Add(new LeftoverItem
                    {
                        Type = LeftoverType.File,
                        PathOrKey = snapFile,
                        Description = "Unremoved Residual (Pre-Uninstall Verified)",
                        SizeBytes = size,
                        Confidence = LeftoverConfidence.High,
                        IsSelected = true
                    });
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
        string cleanName = SanitizeIdentifier(app.DisplayName);

        // 1. Scan Windows Services and Kernel Drivers
        try
        {
            using var servicesKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
            if (servicesKey != null)
            {
                foreach (var svcName in servicesKey.GetSubKeyNames())
                {
                    if (ProtectedRegistryRootNames.Contains(svcName)) continue;

                    try
                    {
                        using var svc = servicesKey.OpenSubKey(svcName);
                        if (svc == null) continue;

                        string imagePath = svc.GetValue("ImagePath")?.ToString() ?? string.Empty;
                        string displayName = svc.GetValue("DisplayName")?.ToString() ?? string.Empty;
                        int svcType = svc.GetValue("Type") is int t ? t : 0;
                        bool isDriver = svcType == 1 || svcType == 2;

                        bool pathMatches = !string.IsNullOrWhiteSpace(app.InstallLocation) &&
                                           !string.IsNullOrWhiteSpace(imagePath) &&
                                           imagePath.Contains(app.InstallLocation, StringComparison.OrdinalIgnoreCase);

                        bool nameMatches = !string.IsNullOrWhiteSpace(cleanName) && cleanName.Length >= 4 &&
                                           (svcName.Contains(cleanName, StringComparison.OrdinalIgnoreCase) ||
                                            displayName.Contains(cleanName, StringComparison.OrdinalIgnoreCase));

                        if (pathMatches || (nameMatches && !File.Exists(imagePath.Trim('\"', '\''))))
                        {
                            string badge = isDriver ? "Kernel Driver" : "Windows Service";
                            items.Add(new LeftoverItem
                            {
                                Type = LeftoverType.Service,
                                PathOrKey = svcName,
                                Description = $"{badge}: {svcName} ({displayName})",
                                Confidence = pathMatches ? LeftoverConfidence.High : LeftoverConfidence.Medium,
                                IsSelected = true
                            });
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        // 2. Scan Scheduled Tasks
        try
        {
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string tasksDir = Path.Combine(winDir, "System32", "Tasks");
            if (Directory.Exists(tasksDir))
            {
                // Check direct folder in Tasks for publisher or app
                if (!string.IsNullOrWhiteSpace(app.Publisher) && app.Publisher.Length >= 3 && !ProtectedDirectoryNames.Contains(app.Publisher))
                {
                    string pubTaskDir = Path.Combine(tasksDir, app.Publisher);
                    if (Directory.Exists(pubTaskDir))
                    {
                        foreach (var taskFile in Directory.GetFiles(pubTaskDir, "*", SearchOption.AllDirectories))
                        {
                            string taskName = $"{app.Publisher}\\{Path.GetFileName(taskFile)}";
                            items.Add(new LeftoverItem
                            {
                                Type = LeftoverType.ScheduledTask,
                                PathOrKey = taskName,
                                Description = $"Scheduled Task: {taskName}",
                                Confidence = LeftoverConfidence.High,
                                IsSelected = true
                            });
                        }
                    }
                }

                // Check root tasks matching app name
                if (!string.IsNullOrWhiteSpace(cleanName) && cleanName.Length >= 4)
                {
                    foreach (var taskFile in Directory.GetFiles(tasksDir, "*", SearchOption.TopDirectoryOnly))
                    {
                        string fname = Path.GetFileName(taskFile);
                        if (fname.Contains(cleanName, StringComparison.OrdinalIgnoreCase))
                        {
                            items.Add(new LeftoverItem
                            {
                                Type = LeftoverType.ScheduledTask,
                                PathOrKey = fname,
                                Description = $"Scheduled Task: {fname}",
                                Confidence = LeftoverConfidence.High,
                                IsSelected = true
                            });
                        }
                    }
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

    private static void ScanComClsidLeftovers(InstalledAppItem app, List<LeftoverItem> items)
    {
        if (string.IsNullOrWhiteSpace(app.InstallLocation) && string.IsNullOrWhiteSpace(app.DisplayName)) return;

        string cleanName = SanitizeIdentifier(app.DisplayName);
        string expLocation = !string.IsNullOrWhiteSpace(app.InstallLocation)
            ? Environment.ExpandEnvironmentVariables(app.InstallLocation.Trim('\"', '\'')).TrimEnd('\\')
            : string.Empty;

        // Vector: HKCR\CLSID and HKLM\SOFTWARE\Classes\CLSID
        RegistryKey[] clsidRoots = [Registry.ClassesRoot, Registry.LocalMachine];
        string[] clsidPaths = [@"CLSID", @"SOFTWARE\Classes\CLSID"];

        for (int r = 0; r < clsidRoots.Length; r++)
        {
            try
            {
                using var clsidBase = clsidRoots[r].OpenSubKey(clsidPaths[r]);
                if (clsidBase == null) continue;

                string hiveName = clsidRoots[r] == Registry.ClassesRoot ? "HKCR" : "HKLM";

                foreach (var clsid in clsidBase.GetSubKeyNames())
                {
                    if (!clsid.StartsWith("{") || !clsid.EndsWith("}")) continue;

                    try
                    {
                        using var clsidKey = clsidBase.OpenSubKey(clsid);
                        if (clsidKey == null) continue;

                        string defaultVal = clsidKey.GetValue(null)?.ToString() ?? string.Empty;
                        bool nameMatches = !string.IsNullOrWhiteSpace(cleanName) && cleanName.Length >= 4 &&
                                           defaultVal.Contains(cleanName, StringComparison.OrdinalIgnoreCase);

                        string serverPath = string.Empty;
                        using (var inproc = clsidKey.OpenSubKey("InprocServer32"))
                        {
                            if (inproc != null)
                            {
                                serverPath = inproc.GetValue(null)?.ToString() ?? string.Empty;
                            }
                        }

                        if (string.IsNullOrWhiteSpace(serverPath))
                        {
                            using var local = clsidKey.OpenSubKey("LocalServer32");
                            if (local != null)
                            {
                                serverPath = local.GetValue(null)?.ToString() ?? string.Empty;
                            }
                        }

                        bool pathMatches = !string.IsNullOrWhiteSpace(expLocation) &&
                                           !string.IsNullOrWhiteSpace(serverPath) &&
                                           serverPath.Contains(expLocation, StringComparison.OrdinalIgnoreCase);

                        if (pathMatches || (nameMatches && !string.IsNullOrWhiteSpace(serverPath) && !File.Exists(serverPath.Trim('\"'))))
                        {
                            string desc = !string.IsNullOrWhiteSpace(defaultVal)
                                ? $"COM CLSID Server ({defaultVal})"
                                : $"COM CLSID Server ({clsid})";

                            items.Add(new LeftoverItem
                            {
                                Type = LeftoverType.RegistryKey,
                                PathOrKey = $@"{hiveName}\{clsidPaths[r]}\{clsid}",
                                Description = desc,
                                Confidence = pathMatches ? LeftoverConfidence.High : LeftoverConfidence.Medium,
                                IsSelected = true
                            });
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }

    private static void ScanEnvironmentPathLeftovers(InstalledAppItem app, List<LeftoverItem> items)
    {
        if (string.IsNullOrWhiteSpace(app.InstallLocation) && string.IsNullOrWhiteSpace(app.DisplayName)) return;

        string expLocation = !string.IsNullOrWhiteSpace(app.InstallLocation)
            ? Environment.ExpandEnvironmentVariables(app.InstallLocation.Trim('\"', '\'')).TrimEnd('\\')
            : string.Empty;

        // User PATH: HKCU\Environment -> Path
        // System PATH: HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment -> Path
        (RegistryKey rootKey, string subKey, string hiveName)[] pathKeys =
        [
            (Registry.CurrentUser, @"Environment", "HKCU"),
            (Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", "HKLM")
        ];

        foreach (var (root, sub, hive) in pathKeys)
        {
            try
            {
                using var key = root.OpenSubKey(sub);
                if (key == null) continue;

                string rawPath = key.GetValue("Path", null, RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(rawPath)) continue;

                var segments = rawPath.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var seg in segments)
                {
                    string trimmed = seg.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;

                    string expanded = Environment.ExpandEnvironmentVariables(trimmed).TrimEnd('\\');

                    // Skip protected system paths
                    if (ProtectedDirectoryNames.Any(p => string.Equals(Path.GetFileName(expanded), p, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    bool matchesLocation = !string.IsNullOrWhiteSpace(expLocation) &&
                                           (expanded.Equals(expLocation, StringComparison.OrdinalIgnoreCase) ||
                                            expanded.StartsWith(expLocation + "\\", StringComparison.OrdinalIgnoreCase));

                    bool matchesNameAndMissing = !string.IsNullOrWhiteSpace(app.DisplayName) &&
                                                 app.DisplayName.Length >= 4 &&
                                                 expanded.Contains(app.DisplayName, StringComparison.OrdinalIgnoreCase) &&
                                                 !Directory.Exists(expanded);

                    if (matchesLocation || matchesNameAndMissing)
                    {
                        items.Add(new LeftoverItem
                        {
                            Type = LeftoverType.EnvironmentPath,
                            PathOrKey = $@"{hive}\{sub}",
                            SubKeyOrValueName = "Path",
                            TargetPath = trimmed,
                            Description = $"Environment PATH Variable: {trimmed}",
                            Confidence = LeftoverConfidence.High,
                            IsSelected = true
                        });
                    }
                }
            }
            catch { }
        }
    }

    private static void ScanSharedDllLeftovers(InstalledAppItem app, List<LeftoverItem> items)
    {
        if (string.IsNullOrWhiteSpace(app.InstallLocation) && string.IsNullOrWhiteSpace(app.DisplayName)) return;

        string expLocation = !string.IsNullOrWhiteSpace(app.InstallLocation)
            ? Environment.ExpandEnvironmentVariables(app.InstallLocation.Trim('\"', '\'')).TrimEnd('\\')
            : string.Empty;

        string[] sharedDllKeys =
        [
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\SharedDLLs",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\SharedDLLs"
        ];

        foreach (var subKeyPath in sharedDllKeys)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(subKeyPath);
                if (key == null) continue;

                foreach (var valName in key.GetValueNames())
                {
                    bool matchesLocation = !string.IsNullOrWhiteSpace(expLocation) &&
                                           valName.Contains(expLocation, StringComparison.OrdinalIgnoreCase);

                    bool matchesName = !string.IsNullOrWhiteSpace(app.DisplayName) &&
                                       app.DisplayName.Length >= 4 &&
                                       valName.Contains(app.DisplayName, StringComparison.OrdinalIgnoreCase) &&
                                       !File.Exists(valName);

                    if (matchesLocation || matchesName)
                    {
                        items.Add(new LeftoverItem
                        {
                            Type = LeftoverType.RegistryValue,
                            PathOrKey = $@"HKLM\{subKeyPath}",
                            SubKeyOrValueName = valName,
                            Description = $"Shared DLL Registration: {Path.GetFileName(valName)}",
                            Confidence = LeftoverConfidence.High,
                            IsSelected = true
                        });
                    }
                }
            }
            catch { }
        }
    }

    private static void ScanEventLogAndCrashDumpLeftovers(InstalledAppItem app, List<LeftoverItem> items)
    {
        string cleanName = SanitizeIdentifier(app.DisplayName);
        string exeName = GetAppExeName(app);
        string exeBase = !string.IsNullOrWhiteSpace(exeName) ? Path.GetFileNameWithoutExtension(exeName) : string.Empty;

        // 1. Windows Event Log Application Sources
        try
        {
            const string eventLogBase = @"SYSTEM\CurrentControlSet\Services\EventLog\Application";
            using var evKey = Registry.LocalMachine.OpenSubKey(eventLogBase);
            if (evKey != null)
            {
                foreach (var srcName in evKey.GetSubKeyNames())
                {
                    if (ProtectedRegistryRootNames.Contains(srcName)) continue;

                    bool nameMatches = (!string.IsNullOrWhiteSpace(cleanName) && cleanName.Length >= 4 &&
                                       srcName.Contains(cleanName, StringComparison.OrdinalIgnoreCase)) ||
                                       (!string.IsNullOrWhiteSpace(exeBase) && string.Equals(srcName, exeBase, StringComparison.OrdinalIgnoreCase));

                    bool pathMatches = false;
                    try
                    {
                        using var srcSub = evKey.OpenSubKey(srcName);
                        string msgFile = srcSub?.GetValue("EventMessageFile")?.ToString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(app.InstallLocation) &&
                            !string.IsNullOrWhiteSpace(msgFile) &&
                            msgFile.Contains(app.InstallLocation, StringComparison.OrdinalIgnoreCase))
                        {
                            pathMatches = true;
                        }
                    }
                    catch { }

                    if (nameMatches || pathMatches)
                    {
                        items.Add(new LeftoverItem
                        {
                            Type = LeftoverType.RegistryKey,
                            PathOrKey = $@"HKLM\{eventLogBase}\{srcName}",
                            Description = $"Windows Event Log Application Source: {srcName}",
                            Confidence = pathMatches ? LeftoverConfidence.High : LeftoverConfidence.Medium,
                            IsSelected = true
                        });
                    }
                }
            }
        }
        catch { }

        // 2. Application Crash Dumps (%LOCALAPPDATA%\CrashDumps)
        try
        {
            string crashDumpDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps");
            if (Directory.Exists(crashDumpDir))
            {
                var dumpFiles = Directory.GetFiles(crashDumpDir, "*.dmp");
                foreach (var dmp in dumpFiles)
                {
                    string fname = Path.GetFileName(dmp);
                    bool matches = (!string.IsNullOrWhiteSpace(exeBase) && fname.StartsWith(exeBase, StringComparison.OrdinalIgnoreCase)) ||
                                   (!string.IsNullOrWhiteSpace(cleanName) && cleanName.Length >= 4 && fname.Contains(cleanName, StringComparison.OrdinalIgnoreCase));

                    if (matches)
                    {
                        long size = 0;
                        try { size = new FileInfo(dmp).Length; } catch { }

                        items.Add(new LeftoverItem
                        {
                            Type = LeftoverType.File,
                            PathOrKey = dmp,
                            Description = $"Application Crash Dump: {fname}",
                            SizeBytes = size,
                            Confidence = LeftoverConfidence.High,
                            IsSelected = true
                        });
                    }
                }
            }
        }
        catch { }

        // 3. Windows Error Reporting (WER) ReportArchive
        try
        {
            string[] werRoots =
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "WER", "ReportArchive"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "WER", "ReportArchive")
            ];

            foreach (var werRoot in werRoots)
            {
                if (!Directory.Exists(werRoot)) continue;

                foreach (var reportDir in Directory.GetDirectories(werRoot))
                {
                    string dirName = Path.GetFileName(reportDir);
                    bool matches = (!string.IsNullOrWhiteSpace(exeBase) && dirName.Contains(exeBase, StringComparison.OrdinalIgnoreCase)) ||
                                   (!string.IsNullOrWhiteSpace(cleanName) && cleanName.Length >= 4 && dirName.Contains(cleanName, StringComparison.OrdinalIgnoreCase));

                    if (matches && InstalledAppService.IsSafeToDeleteResidual(reportDir))
                    {
                        long size = CalculateDirectorySizeSafe(reportDir);
                        items.Add(new LeftoverItem
                        {
                            Type = LeftoverType.Directory,
                            PathOrKey = reportDir,
                            Description = $"WER Crash Report Archive: {dirName}",
                            SizeBytes = size,
                            Confidence = LeftoverConfidence.High,
                            IsSelected = true
                        });
                    }
                }
            }
        }
        catch { }
    }

    private static void ScanAppxPackageStateLeftovers(InstalledAppItem app, List<LeftoverItem> items)
    {
        string packagesRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
        if (!Directory.Exists(packagesRoot)) return;

        try
        {
            string targetFamily = string.Empty;
            if (!string.IsNullOrWhiteSpace(app.PackageFullName))
            {
                int firstUnderscore = app.PackageFullName.IndexOf('_');
                int lastUnderscore = app.PackageFullName.LastIndexOf('_');
                if (firstUnderscore > 0 && lastUnderscore > firstUnderscore)
                {
                    string namePrefix = app.PackageFullName.Substring(0, firstUnderscore);
                    string publisherId = app.PackageFullName.Substring(lastUnderscore + 1);
                    targetFamily = $"{namePrefix}_{publisherId}";
                }
            }

            string cleanName = SanitizeIdentifier(app.DisplayName).Replace(" ", "");

            foreach (var pkgDir in Directory.GetDirectories(packagesRoot))
            {
                string dirName = Path.GetFileName(pkgDir);

                bool matchesFamily = !string.IsNullOrWhiteSpace(targetFamily) &&
                                     dirName.StartsWith(targetFamily, StringComparison.OrdinalIgnoreCase);

                bool matchesName = app.IsWindowsStoreApp &&
                                   !string.IsNullOrWhiteSpace(cleanName) &&
                                   cleanName.Length >= 4 &&
                                   dirName.StartsWith(cleanName, StringComparison.OrdinalIgnoreCase);

                if ((matchesFamily || matchesName) && InstalledAppService.IsSafeToDeleteResidual(pkgDir))
                {
                    long size = CalculateDirectorySizeSafe(pkgDir);
                    items.Add(new LeftoverItem
                    {
                        Type = LeftoverType.Directory,
                        PathOrKey = pkgDir,
                        Description = $"Windows Store App Container State & Cache: {dirName}",
                        SizeBytes = size,
                        Confidence = LeftoverConfidence.High,
                        IsSelected = true
                    });
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
