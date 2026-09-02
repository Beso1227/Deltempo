using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public static class OrphanedAppService
{
    // Comprehensive whitelist of major vendors, runtimes, drivers, and system components
    private static readonly HashSet<string> KnownVendorWhitelist = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft", "Windows", "Packages", "Temp", "Programs", "IconCache.db",
        "VirtualStore", "Adobe", "Google", "Intel", "NVIDIA", "AMD", "Apple",
        "ConnectedDevicesPlatform", "D3DSCache", "Publishers", "PlaceholderTileLogoFolder",
        "Spotify", "Discord", "Slack", "Telegram Desktop", "Code", "GitHubDesktop",
        "BraveSoftware", "Mozilla", "Steam", "EpicGamesLauncher", "Epic Games",
        "JetBrains", "Unity", "Docker", "Zoom", "Notion", "Figma", "Cursor", "Windsurf",
        "dotnet", "pip", "npm", "yarn", "NuGet", "Gradle", "Android", "vcpkg", "Git"
    };

    public static HashSet<string> GetComprehensiveActiveAppKeywords()
    {
        var activeKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Add Known Vendors
        foreach (var vendor in KnownVendorWhitelist)
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

        // 3. Scan Start Menu Shortcuts (.lnk files)
        string[] startMenuPaths =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\Start Menu\Programs")
        };

        foreach (var smPath in startMenuPaths)
        {
            if (Directory.Exists(smPath))
            {
                try
                {
                    foreach (var file in Directory.EnumerateFiles(smPath, "*.lnk", SearchOption.AllDirectories))
                    {
                        var name = Path.GetFileNameWithoutExtension(file);
                        activeKeywords.Add(name);
                        foreach (var token in name.Split(' ', '-', '_', '.'))
                        {
                            if (token.Length >= 3) activeKeywords.Add(token);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
                }
            }
        }

        // 4. Scan Program Files & AppData Programs
        string[] programRoots =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs")
        };

        foreach (var pRoot in programRoots)
        {
            if (Directory.Exists(pRoot))
            {
                try
                {
                    foreach (var dir in Directory.EnumerateDirectories(pRoot))
                    {
                        var dirName = Path.GetFileName(dir);
                        activeKeywords.Add(dirName);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
                }
            }
        }

        // 5. Scan Registry Uninstall Entries
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

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        ScanDir(localAppData, activeApps, orphans);
        ScanDir(roamingAppData, activeApps, orphans);

        return orphans;
    }

    private static void ScanDir(string baseDir, HashSet<string> activeApps, List<TargetFolderInfo> orphans)
    {
        if (!Directory.Exists(baseDir)) return;

        try
        {
            foreach (var dirPath in Directory.EnumerateDirectories(baseDir))
            {
                var dirName = Path.GetFileName(dirPath);
                if (KnownVendorWhitelist.Contains(dirName)) continue;

                // Check against comprehensive active keywords
                bool isActive = activeApps.Any(app =>
                    app.Equals(dirName, StringComparison.OrdinalIgnoreCase) ||
                    dirName.Contains(app, StringComparison.OrdinalIgnoreCase) ||
                    app.Contains(dirName, StringComparison.OrdinalIgnoreCase));

                if (!isActive)
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(dirPath);

                        // If modified recently (< 30 days), skip as it might be an active tool
                        if (DateTime.Now - dirInfo.LastWriteTime < TimeSpan.FromDays(30))
                        {
                            continue;
                        }

                        // Check if it has any running or installed executables
                        var hasExe = Directory.EnumerateFiles(dirPath, "*.exe", SearchOption.AllDirectories).Any();
                        if (hasExe) continue;

                        long size = 0;
                        int fileCount = 0;
                        var enumOptions = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true };

                        foreach (var f in dirInfo.EnumerateFiles("*", enumOptions))
                        {
                            size += f.Length;
                            fileCount++;
                            if (fileCount > 5000) break;
                        }

                        if (size > 2 * 1024 * 1024) // Only list meaningful dead folders (> 2 MB)
                        {
                            orphans.Add(new TargetFolderInfo
                            {
                                Id = $"Orphan_{dirName}",
                                Name = $"{dirName} (Ghost Folder)",
                                Category = "Orphaned Apps",
                                CategoryColor = "#F59E0B",
                                SafetyBadge = "🟡 Verified Leftover",
                                SafetyBadgeColor = "#F59E0B",
                                Description = $"Dead leftover from uninstalled software in {dirPath}",
                                FolderPath = dirPath,
                                IconGlyph = "\uE74D",
                                SizeBytes = size,
                                FileCount = fileCount,
                                IsOrphanedAppFolder = true,
                                RequiresAdmin = false,
                                HasAccess = true,
                                IsSelected = false // Unchecked by default for user safety
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
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
