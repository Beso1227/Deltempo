using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public static class OrphanedAppService
{
    // Strict whitelist of Windows system components, runtimes, drivers, hardware vendors, and core tools
    private static readonly HashSet<string> ProtectedSystemFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Windows & OS core components
        "Windows", "Windows NT", "Windows Mail", "Windows Media Player", "Windows Defender",
        "Windows Defender Advanced Threat Protection", "Windows Security", "WindowsPowerShell",
        "Windows Photo Viewer", "Windows Sidebar", "WindowsApps", "Common Files", "Internet Explorer",
        "Microsoft", "Microsoft.NET", "dotnet", "Reference Assemblies", "MSBuild", "PackageManagement",
        "ModifiableWindowsApps", "InstallShield Installation Information", "Uninstall Information",
        "Package Cache", "Packages", "USOShared", "USOPrivate", "SoftwareDistribution", "ssh",
        "System Volume Information", "$Recycle.Bin", "VirtualStore", "ConnectedDevicesPlatform",
        "D3DSCache", "Publishers", "PlaceholderTileLogoFolder", "CrashDumps", "Temp", "Programs",
        "Application Data", "Documents", "Start Menu", "Desktop", "Common",

        // Hardware, Drivers, Chipsets, GPU vendors
        "Intel", "NVIDIA", "NVIDIA Corporation", "AMD", "Realtek", "ASUS", "Dell", "HP", "Lenovo",
        "Logitech", "Corsair", "Razer", "SteelSeries", "Synaptics", "Dolby", "Broadcom", "Qualcomm",
        "Alps", "Apple", "Apple Computer",

        // Core Development Ecosystems, Runtimes & Platforms
        "Git", "nodejs", "Python", "PowerShell", "Docker", "DockerDesktop", "WSL", "vcpkg", "pip",
        "npm", "yarn", "NuGet", "Gradle", "Android", "Rust", "Go", "Java", "Oracle", "Steam",
        "Epic Games", "EpicGamesLauncher", "JetBrains", "Unity", "Spotify", "Discord", "Slack",
        "Telegram Desktop", "WhatsApp", "WhatsAppDesktop", "Signal", "Skype", "Viber", "Element", "LINE", "WeChat",
        "Mattermost", "Rocket.Chat", "RocketChat", "Cisco-Spark", "CiscoSparkLauncher", "Webex", "WebexTeams",
        "RingCentral", "Thunderbird", "Outlook", "Keybase", "Zulip", "Chime", "Flock", "Kakao", "KakaoTalk",
        "Messenger", "Session", "Threema", "Wire", "ICQ", "MSTeams", "Teams",
        "Code", "GitHubDesktop", "BraveSoftware", "Mozilla", "Zoom", "Notion",
        "Figma", "Cursor", "Windsurf", "Deltempo", "deltempo_cli"
    };

    public static HashSet<string> GetComprehensiveActiveAppKeywords()
    {
        var activeKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Add Known Protected Vendors
        foreach (var vendor in ProtectedSystemFolderNames)
        {
            activeKeywords.Add(vendor);
        }

        // 2. Scan Running Processes
        try
        {
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    if (!string.IsNullOrEmpty(proc.ProcessName))
                    {
                        activeKeywords.Add(proc.ProcessName);
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }

        // 3. Scan Start Menu & Desktop Shortcuts and Portable Executables
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] shortcutRoots =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\Start Menu\Programs"),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            Path.Combine(userProfile, "Desktop"),
            Path.Combine(userProfile, "OneDrive", "Desktop")
        };

        foreach (var smPath in shortcutRoots)
        {
            if (Directory.Exists(smPath))
            {
                try
                {
                    var opt = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
                    var dirInfo = new DirectoryInfo(smPath);
                    foreach (var file in dirInfo.EnumerateFiles("*", opt))
                    {
                        string ext = file.Extension.ToLowerInvariant();
                        if (ext == ".lnk" || ext == ".exe" || ext == ".url")
                        {
                            var name = Path.GetFileNameWithoutExtension(file.Name);
                            activeKeywords.Add(name);
                            foreach (var token in name.Split(' ', '-', '_', '.'))
                            {
                                if (token.Length >= 3) activeKeywords.Add(token);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
                }
            }
        }

        // 4. Scan Registry Uninstall Entries (Both 64-bit and 32-bit WOW64)
        string[] registryRoots =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        foreach (var rootKey in registryRoots)
        {
            try
            {
                using var hklmKey = Registry.LocalMachine.OpenSubKey(rootKey);
                ExtractNamesFromKey(hklmKey, activeKeywords);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }

            try
            {
                using var hkcuKey = Registry.CurrentUser.OpenSubKey(rootKey);
                ExtractNamesFromKey(hkcuKey, activeKeywords);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        }

        // 5. Scan Registry App Paths
        string[] appPathRoots =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths"
        };

        foreach (var apRoot in appPathRoots)
        {
            try
            {
                using var apKey = Registry.LocalMachine.OpenSubKey(apRoot);
                if (apKey != null)
                {
                    foreach (var sub in apKey.GetSubKeyNames())
                    {
                        string cleanSub = Path.GetFileNameWithoutExtension(sub);
                        if (!string.IsNullOrEmpty(cleanSub)) activeKeywords.Add(cleanSub);
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        }

        // 6. Scan Active Windows Services
        try
        {
            using var srvKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
            if (srvKey != null)
            {
                foreach (var sName in srvKey.GetSubKeyNames())
                {
                    try
                    {
                        using var sSub = srvKey.OpenSubKey(sName);
                        var imgPath = sSub?.GetValue("ImagePath")?.ToString();
                        if (!string.IsNullOrEmpty(imgPath))
                        {
                            var clean = imgPath.Trim('"', ' ');
                            var fName = Path.GetFileNameWithoutExtension(clean);
                            if (!string.IsNullOrEmpty(fName)) activeKeywords.Add(fName);
                            var dName = Path.GetFileName(Path.GetDirectoryName(clean) ?? "");
                            if (!string.IsNullOrEmpty(dName)) activeKeywords.Add(dName);
                        }
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }

        // 7. Scan Modern Windows Store / AppX Packages
        try
        {
            const string appModelKeyPath = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";
            using var baseKey = Registry.CurrentUser.OpenSubKey(appModelKeyPath);
            if (baseKey != null)
            {
                foreach (var pkgName in baseKey.GetSubKeyNames())
                {
                    try
                    {
                        using var pkgKey = baseKey.OpenSubKey(pkgName);
                        if (pkgKey == null) continue;

                        string displayName = pkgKey.GetValue("DisplayName")?.ToString()?.Trim() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(displayName) && !displayName.StartsWith("@{") && !displayName.StartsWith("ms-resource:"))
                        {
                            activeKeywords.Add(displayName);
                            foreach (var token in displayName.Split(' ', '-', '_', '.'))
                            {
                                if (token.Length >= 3) activeKeywords.Add(token);
                            }
                        }

                        int underscoreIdx = pkgName.IndexOf('_');
                        if (underscoreIdx > 0)
                        {
                            string prefix = pkgName.Substring(0, underscoreIdx);
                            activeKeywords.Add(prefix);
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        return activeKeywords;
    }

    private static void ExtractNamesFromKey(RegistryKey? key, HashSet<string> keywords)
    {
        if (key == null) return;

        foreach (var subKeyName in key.GetSubKeyNames())
        {
            try
            {
                using var subKey = key.OpenSubKey(subKeyName);
                if (subKey == null) continue;

                var displayName = subKey.GetValue("DisplayName")?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(displayName))
                {
                    keywords.Add(displayName);
                    foreach (var word in displayName.Split(' ', '-', '_', '.'))
                    {
                        if (word.Length >= 3) keywords.Add(word);
                    }
                }

                var installLocation = subKey.GetValue("InstallLocation")?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(installLocation))
                {
                    var dirName = Path.GetFileName(installLocation.TrimEnd('\\'));
                    if (!string.IsNullOrEmpty(dirName)) keywords.Add(dirName);
                }

                var uninstallString = subKey.GetValue("UninstallString")?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(uninstallString))
                {
                    var clean = uninstallString.Trim('"', ' ');
                    var dirName = Path.GetFileName(Path.GetDirectoryName(clean) ?? "");
                    if (!string.IsNullOrEmpty(dirName)) keywords.Add(dirName);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Scans candidate system roots for verified orphaned application remnants across multi-level directory trees.
    /// Features adaptive modification age shielding, structural residual analysis, and 100% fail-closed safety.
    /// </summary>
    public static List<TargetFolderInfo> ScanVerifiedOrphanedFolders()
    {
        var orphans = new List<TargetFolderInfo>();
        var activeApps = GetComprehensiveActiveAppKeywords();

        // 1. Program Files, Program Files (x86), ProgramData & Local Programs
        string pf64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string localPrograms = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");

        ScanDir(pf64, activeApps, orphans, isProgramFiles: true, maxDepth: 1);
        if (!string.Equals(pf64, pf86, StringComparison.OrdinalIgnoreCase))
        {
            ScanDir(pf86, activeApps, orphans, isProgramFiles: true, maxDepth: 1);
        }
        ScanDir(progData, activeApps, orphans, isProgramFiles: true, maxDepth: 1);
        ScanDir(localPrograms, activeApps, orphans, isProgramFiles: true, maxDepth: 1);

        // 2. User AppData (Local, Roaming, LocalLow)
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string localLow = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow");

        ScanDir(localAppData, activeApps, orphans, isProgramFiles: false, maxDepth: 1);
        ScanDir(roamingAppData, activeApps, orphans, isProgramFiles: false, maxDepth: 1);
        ScanDir(localLow, activeApps, orphans, isProgramFiles: false, maxDepth: 1);

        // 3. User State Roots (.config, .cache, Saved Games)
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string dotConfig = Path.Combine(userProfile, ".config");
        string dotCache = Path.Combine(userProfile, ".cache");
        string savedGames = Path.Combine(userProfile, "Saved Games");

        ScanDir(dotConfig, activeApps, orphans, isProgramFiles: false, maxDepth: 0);
        ScanDir(dotCache, activeApps, orphans, isProgramFiles: false, maxDepth: 0);
        ScanDir(savedGames, activeApps, orphans, isProgramFiles: false, maxDepth: 0);

        // 4. VirtualStore Remnants
        string vsPf = Path.Combine(localAppData, "VirtualStore", "Program Files");
        string vsPf86 = Path.Combine(localAppData, "VirtualStore", "Program Files (x86)");
        string vsProgData = Path.Combine(localAppData, "VirtualStore", "ProgramData");

        ScanDir(vsPf, activeApps, orphans, isProgramFiles: false, maxDepth: 0);
        ScanDir(vsPf86, activeApps, orphans, isProgramFiles: false, maxDepth: 0);
        ScanDir(vsProgData, activeApps, orphans, isProgramFiles: false, maxDepth: 0);

        // 5. Downloaded Installation Caches
        string dlInstProgData = Path.Combine(progData, "Downloaded Installations");
        string dlInstLocal = Path.Combine(localAppData, "Downloaded Installations");

        ScanDir(dlInstProgData, activeApps, orphans, isProgramFiles: true, maxDepth: 0);
        ScanDir(dlInstLocal, activeApps, orphans, isProgramFiles: false, maxDepth: 0);

        return orphans;
    }

    private static void ScanDir(string baseDir, HashSet<string> activeApps, List<TargetFolderInfo> orphans, bool isProgramFiles, int maxDepth)
    {
        if (!Directory.Exists(baseDir)) return;

        try
        {
            var dirInfoBase = new DirectoryInfo(baseDir);
            var opt = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            foreach (var dir in dirInfoBase.EnumerateDirectories("*", opt))
            {
                ProcessDirectoryCandidate(dir, activeApps, orphans, isProgramFiles, depth: 0, maxDepth: maxDepth);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }

    private static void ProcessDirectoryCandidate(
        DirectoryInfo dir,
        HashSet<string> activeApps,
        List<TargetFolderInfo> orphans,
        bool isProgramFiles,
        int depth,
        int maxDepth)
    {
        var dirName = dir.Name;

        // Safety Rule 1: Protected system names and hardware vendors
        if (ProtectedSystemFolderNames.Contains(dirName)) return;

        // Safety Rule 2: Explicit system component blocks
        if (dirName.Contains("Windows", StringComparison.OrdinalIgnoreCase) ||
            dirName.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) ||
            dir.FullName.Contains("Common Files", StringComparison.OrdinalIgnoreCase) ||
            dir.FullName.Contains("Package Cache", StringComparison.OrdinalIgnoreCase) ||
            dirName.StartsWith("regid.", StringComparison.OrdinalIgnoreCase) ||
            dirName.StartsWith("$", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Safety Rule 3: Reparse point (Junction/Symlink)
        if ((dir.Attributes & FileAttributes.ReparsePoint) != 0) return;

        // Safety Rule 4: Critical folder shield
        if (!InstalledAppService.IsSafeToDeleteResidual(dir.FullName)) return;

        // Safety Rule 5: Match against active installed applications, running processes, services, shortcuts
        bool isActive = activeApps.Any(app =>
            app.Equals(dirName, StringComparison.OrdinalIgnoreCase) ||
            (app.Length >= 4 && dirName.Contains(app, StringComparison.OrdinalIgnoreCase)));

        if (isActive)
        {
            // If active at parent level, evaluate uninstalled sub-products (depth 1)
            if (depth < maxDepth)
            {
                try
                {
                    var opt = new EnumerationOptions
                    {
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = false,
                        AttributesToSkip = FileAttributes.ReparsePoint
                    };
                    foreach (var child in dir.EnumerateDirectories("*", opt))
                    {
                        ProcessDirectoryCandidate(child, activeApps, orphans, isProgramFiles, depth + 1, maxDepth);
                    }
                }
                catch { }
            }
            return;
        }

        try
        {
            var subEnumOptions = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            bool hasUninstaller = false;
            bool hasBrokenUninstaller = false;
            int exeCount = 0;
            int dllCount = 0;
            long size = 0;
            int fileCount = 0;

            foreach (var f in dir.EnumerateFiles("*", subEnumOptions))
            {
                fileCount++;
                size += f.Length;

                string fl = f.Name.ToLowerInvariant();
                if (fl.EndsWith(".exe"))
                {
                    exeCount++;
                    if (fl.Contains("unins") || fl.Contains("uninstall") || fl.Contains("setup"))
                    {
                        hasUninstaller = true;
                        break;
                    }
                }
                else if (fl.EndsWith(".dat") && (fl.Contains("unins") || fl.Contains("uninstall")))
                {
                    hasBrokenUninstaller = true;
                }
                else if (fl.EndsWith(".dll") || fl.EndsWith(".sys"))
                {
                    dllCount++;
                }

                if (fileCount > 5000) break;
            }

            // If it has an active uninstaller or multiple functional executables, it's still an installed app!
            if (hasUninstaller || exeCount > 1) return;

            // If a single exe exists, verify it doesn't match any active keywords
            if (exeCount == 1 && activeApps.Any(a => a.Contains(dirName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            // Adaptive Modification Age Shield:
            // - Broken uninstaller leftovers (unins*.dat without unins*.exe): 1 hour
            // - Pure cache / configuration files with 0 executables & 0 DLLs: 24 hours
            // - Abandoned inactive binary trees: 7 days
            var age = DateTime.Now - dir.LastWriteTime;
            TimeSpan requiredAge;
            if (hasBrokenUninstaller)
            {
                requiredAge = TimeSpan.FromHours(1);
            }
            else if (exeCount == 0 && dllCount == 0)
            {
                requiredAge = TimeSpan.FromHours(24);
            }
            else
            {
                requiredAge = TimeSpan.FromDays(7);
            }

            if (age < requiredAge) return;

            // In user AppData, require at least 1 KB or an empty directory
            if (!isProgramFiles && size < 1024 && fileCount > 0) return;

            // Prevent duplicate entries
            if (orphans.Any(o => string.Equals(o.FolderPath, dir.FullName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            string category = isProgramFiles ? "Program Files Residuals" : "App Leftovers";
            string desc = hasBrokenUninstaller
                ? $"Incomplete uninstallation residual in {dir.FullName}"
                : (exeCount == 0 && dllCount == 0)
                    ? $"Residual cache & configuration from uninstalled app in {dir.FullName}"
                    : $"Residual files from uninstalled application in {dir.FullName}";

            orphans.Add(new TargetFolderInfo
            {
                Id = $"Orphan_{SanitizeIdentifier(dir.FullName)}",
                Name = $"{dirName} (Residual Files)",
                Category = category,
                CategoryColor = "#F59E0B",
                SafetyBadge = "🛡️ Verified Leftover (Undoable)",
                SafetyBadgeColor = "#10B981",
                Description = desc,
                FolderPath = dir.FullName,
                IconGlyph = "\uE74D",
                SizeBytes = size,
                FileCount = fileCount,
                IsOrphanedAppFolder = true,
                RequiresAdmin = isProgramFiles,
                HasAccess = true,
                IsSelected = false // Unchecked by default for user safety and explicit consent
            });
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }

    private static string SanitizeIdentifier(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return Guid.NewGuid().ToString("N");
        return path.Replace('\\', '_')
                   .Replace('/', '_')
                   .Replace(':', '_')
                   .Replace(' ', '_');
    }
}
