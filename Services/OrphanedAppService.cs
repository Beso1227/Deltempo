using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
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
        "Figma", "Cursor", "Windsurf"
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
                    System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
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
                    System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
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
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }

            try
            {
                using var hkcuKey = Registry.CurrentUser.OpenSubKey(rootKey);
                ExtractNamesFromKey(hkcuKey, activeKeywords);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
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
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
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
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }

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
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        }
    }

    public static List<TargetFolderInfo> ScanVerifiedOrphanedFolders()
    {
        var orphans = new List<TargetFolderInfo>();
        var activeApps = GetComprehensiveActiveAppKeywords();

        // 1. Program Files & Program Files (x86) & ProgramData
        string pf64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string localPrograms = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");

        ScanDir(pf64, activeApps, orphans, isProgramFiles: true);
        if (!string.Equals(pf64, pf86, StringComparison.OrdinalIgnoreCase))
        {
            ScanDir(pf86, activeApps, orphans, isProgramFiles: true);
        }
        ScanDir(progData, activeApps, orphans, isProgramFiles: true);
        ScanDir(localPrograms, activeApps, orphans, isProgramFiles: true);

        // 2. User AppData (Local & Roaming)
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        ScanDir(localAppData, activeApps, orphans, isProgramFiles: false);
        ScanDir(roamingAppData, activeApps, orphans, isProgramFiles: false);

        return orphans;
    }

    private static void ScanDir(string baseDir, HashSet<string> activeApps, List<TargetFolderInfo> orphans, bool isProgramFiles)
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
                var dirName = dir.Name;

                // Safety Rule 1: Protected system names and hardware vendors
                if (ProtectedSystemFolderNames.Contains(dirName)) continue;

                // Safety Rule 2: Prefixes
                if (dirName.StartsWith("Windows", StringComparison.OrdinalIgnoreCase) ||
                    dirName.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) ||
                    dirName.StartsWith("regid.", StringComparison.OrdinalIgnoreCase) ||
                    dirName.StartsWith("$", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Safety Rule 3: Reparse point (Junction/Symlink)
                if ((dir.Attributes & FileAttributes.ReparsePoint) != 0) continue;

                // Safety Rule 4: Match against active installed applications, running processes, services, shortcuts
                // Exact match first (highest confidence), then word-boundary substring.
                // Avoid spurious matches where a short process name like "Code" sits inside "DockerCodeCache".
                bool isActive = activeApps.Any(app =>
                    app.Equals(dirName, StringComparison.OrdinalIgnoreCase) ||
                    (app.Length >= 4 && dirName.Contains(app, StringComparison.OrdinalIgnoreCase)));

                if (isActive) continue;

                try
                {
                    // Safety Rule 5: Modification Age Shield (Must be older than 30 days)
                    if (DateTime.Now - dir.LastWriteTime < TimeSpan.FromDays(30))
                    {
                        continue;
                    }

                    var subEnumOptions = new EnumerationOptions
                    {
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = true,
                        AttributesToSkip = FileAttributes.ReparsePoint
                    };

                    // Safety Rule 6: Check if directory still has an uninstaller or multiple executables
                    bool hasUninstaller = false;
                    int exeCount = 0;
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

                        if (fileCount > 5000) break;
                    }

                    // If it has an uninstaller or multiple functional executables, it's still an installed app!
                    if (hasUninstaller || exeCount > 1) continue;

                    // Safety Rule 7: Threshold
                    // In Program Files & ProgramData, even empty or small residual directories are unwanted leftovers.
                    // In user AppData, require at least some size to avoid noise.
                    if (!isProgramFiles && size < 500 * 1024) continue;

                    orphans.Add(new TargetFolderInfo
                    {
                        Id = $"Orphan_{dirName.Replace(" ", "_")}",
                        Name = $"{dirName} (Residual Files)",
                        Category = isProgramFiles ? "Program Files Residuals" : "App Leftovers",
                        CategoryColor = "#F59E0B",
                        SafetyBadge = "🛡️ Verified Leftover (Undoable)",
                        SafetyBadgeColor = "#10B981",
                        Description = $"Residual files from uninstalled application in {dir.FullName}",
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
                    System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }
}
