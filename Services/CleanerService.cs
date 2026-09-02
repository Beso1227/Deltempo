using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public class CleanerService
{
    #region Native Windows Shell & Kernel APIs

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteFileW(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveDirectoryW(string lpPathName);

    private const uint SHERB_NOCONFIRMATION = 0x00000001;
    private const uint SHERB_NOPROGRESSUI = 0x00000002;
    private const uint SHERB_NOSOUND = 0x00000004;

    #endregion

    public static List<TargetFolderInfo> GetDefaultTargets()
    {
        var isAdmin = ElevationService.IsRunAsAdmin();
        var userTemp = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var winTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
        var winPrefetch = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");
        var winUpdateDownload = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download");
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        var werPath = Path.Combine(programData, "Microsoft", "Windows", "WER");
        var explorerThumbnails = Path.Combine(localAppData, "Microsoft", "Windows", "Explorer");

        var targets = new List<TargetFolderInfo>
        {
            // 1. User Temp
            new TargetFolderInfo
            {
                Id = "UserTemp",
                Name = "User Temp & Scratchpad",
                Category = "User Cache",
                CategoryColor = "#3B82F6",
                SafetyBadge = "🟢 100% Safe Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Application cache, temporary setup extracts, downloads (%TEMP%)",
                FolderPath = userTemp,
                IconGlyph = "\uE8B7",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            },

            // 2. Windows System Temp
            new TargetFolderInfo
            {
                Id = "WinTemp",
                Name = "Windows System Temp",
                Category = "System & GPU",
                CategoryColor = "#6366F1",
                SafetyBadge = "🟢 100% Safe Cache",
                SafetyBadgeColor = "#10B981",
                Description = "OS diagnostic traces, system update scratchpad (C:\\Windows\\Temp)",
                FolderPath = winTemp,
                IconGlyph = "\uE770",
                RequiresAdmin = true,
                HasAccess = isAdmin,
                IsSelected = true
            },

            // 3. Windows Prefetch
            new TargetFolderInfo
            {
                Id = "WinPrefetch",
                Name = "Windows Prefetch Cache",
                Category = "System & GPU",
                CategoryColor = "#8B5CF6",
                SafetyBadge = "🟢 100% Safe Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Stale execution traces & cached startup headers (C:\\Windows\\Prefetch)",
                FolderPath = winPrefetch,
                IconGlyph = "\uE945",
                RequiresAdmin = true,
                HasAccess = isAdmin,
                IsSelected = true
            },

            // 4. Windows Update Delivery Cache
            new TargetFolderInfo
            {
                Id = "WinUpdateCache",
                Name = "Windows Update Cache",
                Category = "System & GPU",
                CategoryColor = "#EC4899",
                SafetyBadge = "🟢 100% Safe Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Downloaded update installers & delivery cache (SoftwareDistribution\\Download)",
                FolderPath = winUpdateDownload,
                IconGlyph = "\uE896",
                RequiresAdmin = true,
                HasAccess = isAdmin,
                IsSelected = true
            },

            // 5. Windows Delivery Optimization (WUDO)
            new TargetFolderInfo
            {
                Id = "WinDeliveryOpt",
                Name = "Windows Delivery Optimization",
                Category = "System & OS",
                CategoryColor = "#3B82F6",
                SafetyBadge = "🟢 100% Safe Cache",
                SafetyBadgeColor = "#10B981",
                Description = "P2P Windows update delivery chunks and background bits cache (DeliveryOptimization)",
                FolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "ServiceProfiles", "NetworkService", "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization", "Cache"),
                IconGlyph = "\uE774",
                RequiresAdmin = true,
                HasAccess = isAdmin,
                IsSelected = true
            },

            // 6. GPU & DirectX Shaders
            new TargetFolderInfo
            {
                Id = "GpuShaderCaches",
                Name = "DirectX & GPU Shader Caches",
                Category = "System & GPU",
                CategoryColor = "#8B5CF6",
                SafetyBadge = "🟢 100% Safe Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Compiled graphics shaders from NVIDIA, AMD, D3DSCache & Intel",
                FolderPath = Path.Combine(localAppData, "D3DSCache"),
                IconGlyph = "\uE790",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            },

            // 7. Gaming Launchers & Shaders
            new TargetFolderInfo
            {
                Id = "GamingLaunchers",
                Name = "Game Launchers & Shaders",
                Category = "Gaming & Media",
                CategoryColor = "#EC4899",
                SafetyBadge = "🟢 100% Safe Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Steam download chunks & shadercache, Epic Games webcache, Battle.net & EA App caches",
                FolderPath = "Gaming Launchers Pool",
                IconGlyph = "\uE7FC",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            },

            // 8. Media & Creator Render Scratchpads
            new TargetFolderInfo
            {
                Id = "MediaCreatorCaches",
                Name = "Media & Creator Render Caches",
                Category = "Creator & Media",
                CategoryColor = "#F59E0B",
                SafetyBadge = "🟢 100% Safe Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Adobe Premiere / After Effects Media Cache & Peak files, DaVinci Resolve proxy scratch, OBS logs",
                FolderPath = "Media Creator Caches Pool",
                IconGlyph = "\uE714",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            },

            // 9. Desktop & Electron Apps Cache Sweeper
            new TargetFolderInfo
            {
                Id = "AppCacheSweeper",
                Name = "Desktop Apps Cache Sweeper",
                Category = "User Cache",
                CategoryColor = "#10B981",
                SafetyBadge = "🟢 100% Safe Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Disposable GPU & Code Cache in Discord, Spotify, Slack, VS Code, Teams, Notion",
                FolderPath = "App Caches Pool",
                IconGlyph = "\uE715",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            },

            // 10. Web Browser Caches
            new TargetFolderInfo
            {
                Id = "BrowserCaches",
                Name = "Web Browsers Cache Pool",
                Category = "User Cache",
                CategoryColor = "#F97316",
                SafetyBadge = "🟢 100% Safe Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Chrome, Edge, Brave, Firefox web cache (cookies and logins preserved)",
                FolderPath = "Browser Web Caches",
                IconGlyph = "\uE774",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            },

            // 11. Developer & Package Caches
            new TargetFolderInfo
            {
                Id = "DevPackageCaches",
                Name = "Developer & Package Caches",
                Category = "Dev Caches",
                CategoryColor = "#06B6D4",
                SafetyBadge = "🟢 100% Safe Cache",
                SafetyBadgeColor = "#10B981",
                Description = "pip, npm, .gradle, yarn, .cache, and nuget package download caches",
                FolderPath = Path.Combine(localAppData, "pip", "cache"),
                IconGlyph = "\uE7B8",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            },

            // 12. Mobile Sync & Dev Daemons
            new TargetFolderInfo
            {
                Id = "MobileDevResiduals",
                Name = "Mobile Sync & Dev Daemons",
                Category = "Dev & Mobile",
                CategoryColor = "#06B6D4",
                SafetyBadge = "🟢 100% Safe Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Apple iTunes temp sync cache, Android Studio emulator cache, Gradle & Cargo caches",
                FolderPath = "Mobile & Dev Residuals Pool",
                IconGlyph = "\uE8EA",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            },

            // 13. CBS & Servicing Diagnostic Logs
            new TargetFolderInfo
            {
                Id = "WinServicingLogs",
                Name = "Windows Servicing & CBS Logs",
                Category = "Diagnostics",
                CategoryColor = "#8B5CF6",
                SafetyBadge = "🟢 100% Safe Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Stale Component-Based Servicing logs, DISM deployment logs & setup traces (CbsPersist)",
                FolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Logs", "CBS"),
                IconGlyph = "\uE7C3",
                RequiresAdmin = true,
                HasAccess = isAdmin,
                IsSelected = true
            },

            // 14. Error Reports & Crash Dumps
            new TargetFolderInfo
            {
                Id = "CrashDumps",
                Name = "Error Reports & Crash Dumps",
                Category = "Diagnostics",
                CategoryColor = "#F59E0B",
                SafetyBadge = "🟢 100% Safe Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Windows Error Reporting logs & process memory dumps (WER / Dumps)",
                FolderPath = werPath,
                IconGlyph = "\uE7BA",
                RequiresAdmin = true,
                HasAccess = isAdmin,
                IsSelected = true
            },

            // 15. Explorer Thumbnails
            new TargetFolderInfo
            {
                Id = "Thumbnails",
                Name = "Explorer Thumbnail Cache",
                Category = "Diagnostics",
                CategoryColor = "#06B6D4",
                SafetyBadge = "🟢 100% Safe Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Cached image & video thumbnail databases (thumbcache_*.db)",
                FolderPath = explorerThumbnails,
                IconGlyph = "\uE8B9",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            },

            // 16. Recycle Bin
            new TargetFolderInfo
            {
                Id = "RecycleBin",
                Name = "Windows Recycle Bin",
                Category = "Storage",
                CategoryColor = "#EF4444",
                SafetyBadge = "🟢 100% Safe Cache",
                SafetyBadgeColor = "#10B981",
                Description = "All physical drive Recycle Bins via Windows Shell API (SHEmptyRecycleBin)",
                FolderPath = "Recycle Bin (All Drives)",
                IconGlyph = "\uE74D",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true,
                IsSpecialShellTarget = true
            }
        };

        // 17. Orphaned Uninstalled AppData Leftovers
        try
        {
            var orphans = OrphanedAppService.ScanVerifiedOrphanedFolders();
            foreach (var o in orphans)
            {
                targets.Add(o);
            }
        }
        catch { }

        return targets;
    }

    public async Task ScanFolderAsync(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        folder.IsScanning = true;
        folder.StatusMessage = "Scanning...";

        await Task.Run(() =>
        {
            if (folder.IsSpecialShellTarget && folder.Id == "RecycleBin")
            {
                ScanRecycleBin(folder, logAction);
                folder.IsScanning = false;
                return;
            }

            if (folder.Id == "GpuShaderCaches")
            {
                ScanGpuShaderPools(folder, logAction, ct);
                folder.IsScanning = false;
                return;
            }

            if (folder.Id == "GamingLaunchers")
            {
                ScanGamingLauncherPools(folder, logAction, ct);
                folder.IsScanning = false;
                return;
            }

            if (folder.Id == "MediaCreatorCaches")
            {
                ScanMediaCreatorPools(folder, logAction, ct);
                folder.IsScanning = false;
                return;
            }

            if (folder.Id == "MobileDevResiduals")
            {
                ScanMobileDevPools(folder, logAction, ct);
                folder.IsScanning = false;
                return;
            }

            if (folder.Id == "WinServicingLogs")
            {
                ScanWinServicingLogs(folder, logAction, ct);
                folder.IsScanning = false;
                return;
            }

            if (folder.Id == "AppCacheSweeper")
            {
                ScanAppCachePools(folder, logAction, ct);
                folder.IsScanning = false;
                return;
            }

            if (folder.Id == "BrowserCaches")
            {
                ScanBrowserCachePools(folder, logAction, ct);
                folder.IsScanning = false;
                return;
            }

            if (folder.Id == "DevPackageCaches")
            {
                ScanDevPackageCaches(folder, logAction, ct);
                folder.IsScanning = false;
                return;
            }

            if (!Directory.Exists(folder.FolderPath))
            {
                folder.SizeBytes = 0;
                folder.FileCount = 0;
                folder.FolderCount = 0;
                folder.TopFiles = new List<JunkFileItem>();
                folder.StatusMessage = "Empty or Not Found";
                folder.IsScanning = false;
                return;
            }

            long totalBytes = 0;
            int fileCount = 0;
            int folderCount = 0;
            var topFilesBag = new ConcurrentBag<JunkFileItem>();

            try
            {
                var dirInfo = new DirectoryInfo(folder.FolderPath);
                var enumOptions = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true,
                    AttributesToSkip = FileAttributes.ReparsePoint
                };

                foreach (var file in dirInfo.EnumerateFiles("*", enumOptions))
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        long len = file.Length;
                        totalBytes += len;
                        fileCount++;

                        if (topFilesBag.Count < 30 || len > 5L * 1024 * 1024)
                        {
                            topFilesBag.Add(new JunkFileItem
                            {
                                FileName = file.Name,
                                FilePath = file.FullName,
                                SizeBytes = len,
                                LastModified = file.LastWriteTime
                            });
                        }
                    }
                    catch { }
                }

                foreach (var _ in dirInfo.EnumerateDirectories("*", enumOptions))
                {
                    if (ct.IsCancellationRequested) break;
                    folderCount++;
                }

                folder.SizeBytes = totalBytes;
                folder.FileCount = fileCount;
                folder.FolderCount = folderCount;
                folder.TopFiles = topFilesBag.OrderByDescending(f => f.SizeBytes).Take(15).ToList();
                folder.StatusMessage = $"Ready: {TargetFolderInfo.FormatBytes(totalBytes)}";

                logAction($"Scanned {folder.Name}: {TargetFolderInfo.FormatBytes(totalBytes)} ({fileCount:N0} files)", LogLevel.Info);
            }
            catch (UnauthorizedAccessException ex)
            {
                folder.StatusMessage = "Access Denied (Admin Required)";
                logAction($"Admin privileges required to scan {folder.Name}: {ex.Message}", LogLevel.Warning);
            }
            catch (Exception ex)
            {
                folder.StatusMessage = "Scan Error";
                logAction($"Error scanning {folder.Name}: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                folder.IsScanning = false;
            }
        }, ct);
    }

    private static void ScanGpuShaderPools(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] shaderDirs =
        {
            Path.Combine(localAppData, "NVIDIA", "DXCache"),
            Path.Combine(localAppData, "NVIDIA", "GLCache"),
            Path.Combine(localAppData, "AMD", "DxCache"),
            Path.Combine(localAppData, "D3DSCache"),
            Path.Combine(localAppData, "Intel", "ShaderCache")
        };

        ScanDirectoryList(folder, shaderDirs, "GPU Shaders", logAction, ct);
    }

    private static void ScanGamingLauncherPools(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        string[] gamingDirs =
        {
            Path.Combine(progFilesX86, "Steam", "downloading"),
            Path.Combine(progFilesX86, "Steam", "shadercache"),
            Path.Combine(progFilesX86, "Steam", "appcache", "httpcache"),
            Path.Combine(localAppData, "Steam", "htmlcache"),
            Path.Combine(localAppData, "EpicGamesLauncher", "Saved", "webcache"),
            Path.Combine(localAppData, "EpicGamesLauncher", "Saved", "webcache_4430"),
            Path.Combine(localAppData, "Battle.net", "Cache"),
            Path.Combine(localAppData, "Blizzard Entertainment", "Battle.net", "Cache"),
            Path.Combine(localAppData, "Electronic Arts", "EA Desktop", "Logs"),
            Path.Combine(localAppData, "Electronic Arts", "EA Desktop", "cache"),
            Path.Combine(localAppData, "Ubisoft Game Launcher", "cache")
        };

        ScanDirectoryList(folder, gamingDirs, "Game Launchers & Shaders", logAction, ct);
    }

    private static void ScanMediaCreatorPools(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        string[] mediaDirs =
        {
            Path.Combine(roamingAppData, "Adobe", "Common", "Media Cache Files"),
            Path.Combine(roamingAppData, "Adobe", "Common", "Media Cache"),
            Path.Combine(roamingAppData, "Adobe", "Common", "Peak Files"),
            Path.Combine(roamingAppData, "Blackmagic Design", "DaVinci Resolve", "Support", "logs"),
            Path.Combine(roamingAppData, "Blackmagic Design", "DaVinci Resolve", "Cache"),
            Path.Combine(roamingAppData, "obs-studio", "logs"),
            Path.Combine(roamingAppData, "obs-studio", "crashes"),
            Path.Combine(roamingAppData, "slobs-client", "cache"),
            Path.Combine(roamingAppData, "Blender Foundation", "Blender", "temp")
        };

        ScanDirectoryList(folder, mediaDirs, "Media & Creator Render Caches", logAction, ct);
    }

    private static void ScanMobileDevPools(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        string[] devDirs =
        {
            Path.Combine(roamingAppData, "Apple Computer", "MobileDeviceBackup", "Temp"),
            Path.Combine(roamingAppData, "Apple Computer", "Logs"),
            Path.Combine(userProfile, ".android", "cache"),
            Path.Combine(userProfile, ".android", "build-cache"),
            Path.Combine(userProfile, ".gradle", "daemon"),
            Path.Combine(userProfile, ".gradle", "caches", "transforms-1"),
            Path.Combine(userProfile, ".gradle", "caches", "transforms-2"),
            Path.Combine(userProfile, ".gradle", "caches", "transforms-3"),
            Path.Combine(userProfile, ".cargo", "registry", "cache")
        };

        ScanDirectoryList(folder, devDirs, "Mobile & Dev Residuals", logAction, ct);
    }

    private static void ScanWinServicingLogs(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string[] logDirs =
        {
            Path.Combine(winDir, "Logs", "CBS"),
            Path.Combine(winDir, "Logs", "DISM"),
            Path.Combine(winDir, "Logs", "DPX")
        };

        ScanDirectoryList(folder, logDirs, "Windows Servicing & CBS Logs", logAction, ct);
    }

    private static void ScanAppCachePools(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        string[] appCacheDirs =
        {
            Path.Combine(localAppData, "Spotify", "Data"),
            Path.Combine(localAppData, "Spotify", "Storage"),
            Path.Combine(roamingAppData, "discord", "Cache"),
            Path.Combine(roamingAppData, "discord", "Code Cache"),
            Path.Combine(roamingAppData, "discord", "GPUCache"),
            Path.Combine(roamingAppData, "Slack", "Cache"),
            Path.Combine(roamingAppData, "Slack", "GPUCache"),
            Path.Combine(roamingAppData, "Code", "Cache"),
            Path.Combine(roamingAppData, "Code", "CachedData"),
            Path.Combine(roamingAppData, "Code", "GPUCache"),
            Path.Combine(roamingAppData, "Cursor", "Cache"),
            Path.Combine(roamingAppData, "Cursor", "GPUCache"),
            Path.Combine(roamingAppData, "Notion", "Cache"),
            Path.Combine(roamingAppData, "Notion", "GPUCache"),
            Path.Combine(localAppData, "Microsoft", "Teams", "Cache"),
            Path.Combine(roamingAppData, "Telegram Desktop", "tdata", "user_data", "cache")
        };

        ScanDirectoryList(folder, appCacheDirs, "Desktop Apps Caches", logAction, ct);
    }

    private static void ScanBrowserCachePools(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] browserCacheDirs =
        {
            Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Cache", "Cache_Data"),
            Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Cache", "Cache_Data"),
            Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data", "Default", "Cache", "Cache_Data")
        };

        ScanDirectoryList(folder, browserCacheDirs, "Web Browsers Cache", logAction, ct);
    }

    private static void ScanDevPackageCaches(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] devDirs =
        {
            Path.Combine(localAppData, "pip", "cache"),
            Path.Combine(localAppData, "npm-cache"),
            Path.Combine(userProfile, ".cache"),
            Path.Combine(userProfile, ".gradle", "caches"),
            Path.Combine(userProfile, ".nuget", "packages", "temp")
        };

        ScanDirectoryList(folder, devDirs, "Developer Caches", logAction, ct);
    }

    private static void ScanDirectoryList(
        TargetFolderInfo folder,
        IEnumerable<string> directories,
        string categoryTitle,
        Action<string, LogLevel> logAction,
        CancellationToken ct)
    {
        long totalBytes = 0;
        int fileCount = 0;
        var topFilesBag = new ConcurrentBag<JunkFileItem>();

        foreach (var dir in directories)
        {
            if (!Directory.Exists(dir)) continue;
            try
            {
                var dirInfo = new DirectoryInfo(dir);
                var enumOptions = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true };
                foreach (var f in dirInfo.EnumerateFiles("*", enumOptions))
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        totalBytes += f.Length;
                        fileCount++;
                        if (topFilesBag.Count < 20 || f.Length > 2 * 1024 * 1024)
                        {
                            topFilesBag.Add(new JunkFileItem
                            {
                                FileName = f.Name,
                                FilePath = f.FullName,
                                SizeBytes = f.Length,
                                LastModified = f.LastWriteTime
                            });
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        folder.SizeBytes = totalBytes;
        folder.FileCount = fileCount;
        folder.TopFiles = topFilesBag.OrderByDescending(f => f.SizeBytes).Take(15).ToList();
        folder.StatusMessage = $"Ready: {TargetFolderInfo.FormatBytes(totalBytes)}";
        logAction($"Scanned {categoryTitle}: {TargetFolderInfo.FormatBytes(totalBytes)} ({fileCount:N0} files)", LogLevel.Info);
    }

    private static void ScanRecycleBin(TargetFolderInfo folder, Action<string, LogLevel> logAction)
    {
        try
        {
            var rbInfo = new SHQUERYRBINFO { cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO)) };
            int hresult = SHQueryRecycleBin(null, ref rbInfo);
            if (hresult == 0)
            {
                folder.SizeBytes = rbInfo.i64Size;
                folder.FileCount = (int)rbInfo.i64NumItems;
                folder.FolderCount = 0;
                folder.StatusMessage = $"Ready: {TargetFolderInfo.FormatBytes(rbInfo.i64Size)}";
                logAction($"Scanned Recycle Bin: {TargetFolderInfo.FormatBytes(rbInfo.i64Size)} across {rbInfo.i64NumItems:N0} items", LogLevel.Info);
            }
            else
            {
                folder.StatusMessage = "Empty";
                folder.SizeBytes = 0;
                folder.FileCount = 0;
            }
        }
        catch (Exception ex)
        {
            folder.StatusMessage = "Scan Error";
            logAction($"Error querying Recycle Bin: {ex.Message}", LogLevel.Warning);
        }
    }

    public async Task<(long freedBytes, int filesDeleted, int foldersDeleted, int filesSkipped)> CleanFolderAsync(
        TargetFolderInfo folder,
        bool safeMode24Hours,
        Action<string, LogLevel> logAction,
        Action<double> progressReport,
        CancellationToken ct)
    {
        folder.IsCleaning = true;
        folder.StatusMessage = "Cleaning...";

        long freedBytes = 0;
        int filesDeleted = 0;
        int foldersDeleted = 0;
        int filesSkipped = 0;

        await Task.Run(() =>
        {
            if (folder.IsSpecialShellTarget && folder.Id == "RecycleBin")
            {
                try
                {
                    long initialSize = folder.SizeBytes;
                    int initialCount = folder.FileCount;
                    uint flags = SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND;
                    int hresult = SHEmptyRecycleBin(IntPtr.Zero, null, flags);
                    if (hresult == 0)
                    {
                        freedBytes = initialSize;
                        filesDeleted = initialCount;
                        folder.SizeBytes = 0;
                        folder.FileCount = 0;
                        folder.StatusMessage = "Emptied successfully";
                        logAction($"Emptied Recycle Bin: {TargetFolderInfo.FormatBytes(freedBytes)} reclaimed", LogLevel.Success);
                    }
                    else
                    {
                        folder.StatusMessage = "Empty";
                    }
                }
                catch (Exception ex)
                {
                    folder.StatusMessage = "Error emptying";
                    logAction($"Error emptying Recycle Bin: {ex.Message}", LogLevel.Error);
                }
                finally
                {
                    folder.IsCleaning = false;
                }
                return;
            }

            var directoriesToClean = new List<string>();
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

            if (folder.Id == "GpuShaderCaches")
            {
                directoriesToClean.Add(Path.Combine(localAppData, "NVIDIA", "DXCache"));
                directoriesToClean.Add(Path.Combine(localAppData, "NVIDIA", "GLCache"));
                directoriesToClean.Add(Path.Combine(localAppData, "AMD", "DxCache"));
                directoriesToClean.Add(Path.Combine(localAppData, "D3DSCache"));
                directoriesToClean.Add(Path.Combine(localAppData, "Intel", "ShaderCache"));
            }
            else if (folder.Id == "GamingLaunchers")
            {
                directoriesToClean.Add(Path.Combine(progFilesX86, "Steam", "downloading"));
                directoriesToClean.Add(Path.Combine(progFilesX86, "Steam", "shadercache"));
                directoriesToClean.Add(Path.Combine(progFilesX86, "Steam", "appcache", "httpcache"));
                directoriesToClean.Add(Path.Combine(localAppData, "Steam", "htmlcache"));
                directoriesToClean.Add(Path.Combine(localAppData, "EpicGamesLauncher", "Saved", "webcache"));
                directoriesToClean.Add(Path.Combine(localAppData, "EpicGamesLauncher", "Saved", "webcache_4430"));
                directoriesToClean.Add(Path.Combine(localAppData, "Battle.net", "Cache"));
                directoriesToClean.Add(Path.Combine(localAppData, "Blizzard Entertainment", "Battle.net", "Cache"));
                directoriesToClean.Add(Path.Combine(localAppData, "Electronic Arts", "EA Desktop", "Logs"));
                directoriesToClean.Add(Path.Combine(localAppData, "Electronic Arts", "EA Desktop", "cache"));
                directoriesToClean.Add(Path.Combine(localAppData, "Ubisoft Game Launcher", "cache"));
            }
            else if (folder.Id == "MediaCreatorCaches")
            {
                directoriesToClean.Add(Path.Combine(roamingAppData, "Adobe", "Common", "Media Cache Files"));
                directoriesToClean.Add(Path.Combine(roamingAppData, "Adobe", "Common", "Media Cache"));
                directoriesToClean.Add(Path.Combine(roamingAppData, "Adobe", "Common", "Peak Files"));
                directoriesToClean.Add(Path.Combine(roamingAppData, "Blackmagic Design", "DaVinci Resolve", "Support", "logs"));
                directoriesToClean.Add(Path.Combine(roamingAppData, "Blackmagic Design", "DaVinci Resolve", "Cache"));
                directoriesToClean.Add(Path.Combine(roamingAppData, "obs-studio", "logs"));
                directoriesToClean.Add(Path.Combine(roamingAppData, "obs-studio", "crashes"));
                directoriesToClean.Add(Path.Combine(roamingAppData, "slobs-client", "cache"));
                directoriesToClean.Add(Path.Combine(roamingAppData, "Blender Foundation", "Blender", "temp"));
            }
            else if (folder.Id == "MobileDevResiduals")
            {
                directoriesToClean.Add(Path.Combine(roamingAppData, "Apple Computer", "MobileDeviceBackup", "Temp"));
                directoriesToClean.Add(Path.Combine(roamingAppData, "Apple Computer", "Logs"));
                directoriesToClean.Add(Path.Combine(userProfile, ".android", "cache"));
                directoriesToClean.Add(Path.Combine(userProfile, ".android", "build-cache"));
                directoriesToClean.Add(Path.Combine(userProfile, ".gradle", "daemon"));
                directoriesToClean.Add(Path.Combine(userProfile, ".gradle", "caches", "transforms-1"));
                directoriesToClean.Add(Path.Combine(userProfile, ".gradle", "caches", "transforms-2"));
                directoriesToClean.Add(Path.Combine(userProfile, ".gradle", "caches", "transforms-3"));
                directoriesToClean.Add(Path.Combine(userProfile, ".cargo", "registry", "cache"));
            }
            else if (folder.Id == "WinServicingLogs")
            {
                directoriesToClean.Add(Path.Combine(winDir, "Logs", "CBS"));
                directoriesToClean.Add(Path.Combine(winDir, "Logs", "DISM"));
                directoriesToClean.Add(Path.Combine(winDir, "Logs", "DPX"));
            }
            else if (folder.Id == "AppCacheSweeper")
            {
                directoriesToClean.Add(Path.Combine(localAppData, "Spotify", "Data"));
                directoriesToClean.Add(Path.Combine(localAppData, "Spotify", "Storage"));
                directoriesToClean.Add(Path.Combine(roamingAppData, "discord", "Cache"));
                directoriesToClean.Add(Path.Combine(roamingAppData, "discord", "Code Cache"));
                directoriesToClean.Add(Path.Combine(roamingAppData, "discord", "GPUCache"));
                directoriesToClean.Add(Path.Combine(roamingAppData, "Slack", "Cache"));
                directoriesToClean.Add(Path.Combine(roamingAppData, "Slack", "GPUCache"));
                directoriesToClean.Add(Path.Combine(roamingAppData, "Code", "Cache"));
                directoriesToClean.Add(Path.Combine(roamingAppData, "Code", "CachedData"));
                directoriesToClean.Add(Path.Combine(roamingAppData, "Code", "GPUCache"));
                directoriesToClean.Add(Path.Combine(roamingAppData, "Cursor", "Cache"));
                directoriesToClean.Add(Path.Combine(roamingAppData, "Cursor", "GPUCache"));
                directoriesToClean.Add(Path.Combine(roamingAppData, "Notion", "Cache"));
                directoriesToClean.Add(Path.Combine(roamingAppData, "Notion", "GPUCache"));
                directoriesToClean.Add(Path.Combine(localAppData, "Microsoft", "Teams", "Cache"));
                directoriesToClean.Add(Path.Combine(roamingAppData, "Telegram Desktop", "tdata", "user_data", "cache"));
            }
            else if (folder.Id == "BrowserCaches")
            {
                directoriesToClean.Add(Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Cache", "Cache_Data"));
                directoriesToClean.Add(Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Cache", "Cache_Data"));
                directoriesToClean.Add(Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data", "Default", "Cache", "Cache_Data"));
            }
            else if (folder.Id == "DevPackageCaches")
            {
                directoriesToClean.Add(Path.Combine(localAppData, "pip", "cache"));
                directoriesToClean.Add(Path.Combine(localAppData, "npm-cache"));
                directoriesToClean.Add(Path.Combine(userProfile, ".cache"));
                directoriesToClean.Add(Path.Combine(userProfile, ".gradle", "caches"));
                directoriesToClean.Add(Path.Combine(userProfile, ".nuget", "packages", "temp"));
            }
            else
            {
                directoriesToClean.Add(folder.FolderPath);
            }

            var cutoffTime = DateTime.Now - TimeSpan.FromHours(24);
            long lastProgressTime = 0;

            foreach (var targetPath in directoriesToClean)
            {
                if (ct.IsCancellationRequested) break;
                if (!Directory.Exists(targetPath)) continue;

                try
                {
                    var dirInfo = new DirectoryInfo(targetPath);
                    var enumOptions = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true };

                    var fileList = new List<FileInfo>();
                    try
                    {
                        fileList = dirInfo.EnumerateFiles("*", enumOptions).ToList();
                    }
                    catch { }

                    int totalFileCount = fileList.Count;
                    int processedFiles = 0;

                    foreach (var file in fileList)
                    {
                        if (ct.IsCancellationRequested) break;

                        if (safeMode24Hours && file.LastWriteTime > cutoffTime)
                        {
                            filesSkipped++;
                            continue;
                        }

                        if (IsProtectedFile(file.FullName))
                        {
                            filesSkipped++;
                            continue;
                        }

                        try
                        {
                            long fileLen = file.Length;
                            if (file.IsReadOnly)
                            {
                                file.Attributes = FileAttributes.Normal;
                            }

                            bool deleted = DeleteFileW(file.FullName);
                            if (!deleted)
                            {
                                file.Delete();
                            }

                            freedBytes += fileLen;
                            filesDeleted++;
                        }
                        catch
                        {
                            filesSkipped++;
                        }

                        processedFiles++;
                        if (Environment.TickCount64 - lastProgressTime > 100)
                        {
                            lastProgressTime = Environment.TickCount64;
                            double progress = totalFileCount > 0 ? (double)processedFiles / totalFileCount : 1.0;
                            progressReport(progress);
                        }
                    }

                    // Prune empty subdirectories
                    try
                    {
                        var subDirs = dirInfo.EnumerateDirectories("*", SearchOption.AllDirectories)
                                             .OrderByDescending(d => d.FullName.Length);

                        foreach (var sub in subDirs)
                        {
                            if (ct.IsCancellationRequested) break;
                            try
                            {
                                if (!sub.EnumerateFileSystemInfos().Any())
                                {
                                    if (RemoveDirectoryW(sub.FullName) || !Directory.Exists(sub.FullName))
                                    {
                                        foldersDeleted++;
                                    }
                                    else
                                    {
                                        sub.Delete();
                                        foldersDeleted++;
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
                catch { }
            }

            folder.SizeBytes = Math.Max(0, folder.SizeBytes - freedBytes);
            folder.FileCount = Math.Max(0, folder.FileCount - filesDeleted);
            folder.StatusMessage = $"Purged {TargetFolderInfo.FormatBytes(freedBytes)}";
            folder.IsCleaning = false;

            logAction($"Cleaned {folder.Name}: {TargetFolderInfo.FormatBytes(freedBytes)} reclaimed ({filesDeleted:N0} files deleted, {filesSkipped:N0} skipped)", LogLevel.Success);
        }, ct);

        return (freedBytes, filesDeleted, foldersDeleted, filesSkipped);
    }

    private static bool IsProtectedFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        string[] dangerousExtensions = { ".sys", ".drv", ".msc", ".cpl", ".key", ".dat", ".docx", ".xlsx", ".pptx", ".pdf", ".psd", ".blend" };
        if (dangerousExtensions.Contains(ext))
        {
            var fileName = Path.GetFileName(filePath).ToLowerInvariant();
            if (fileName == "thumbcache_idx.db" || fileName.StartsWith("thumbcache_")) return false;
            return true;
        }
        return false;
    }

    public static string GenerateAuditReport(
        IEnumerable<TargetFolderInfo> targets,
        CleanSummary summary,
        bool safetyShieldEnabled)
    {
        var sb = new StringBuilder();
        sb.AppendLine("==================================================================");
        sb.AppendLine("         DELTEMPO — PRECISION AUDIT REPORT");
        sb.AppendLine("==================================================================");
        sb.AppendLine($"Execution Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Safety Shield (>24h Protection): {(safetyShieldEnabled ? "ENABLED (100% Safe)" : "DISABLED")}");
        sb.AppendLine($"Total Space Reclaimed: {summary.FormattedFreedSize}");
        sb.AppendLine($"Total Files Deleted: {summary.TotalFilesDeleted:N0}");
        sb.AppendLine($"Total Folders Purged: {summary.TotalFoldersDeleted:N0}");
        sb.AppendLine($"Total Files Skipped / Protected: {summary.TotalFilesSkipped:N0}");
        if (summary.ElapsedTime > TimeSpan.Zero)
        {
            sb.AppendLine($"Time Elapsed: {summary.ElapsedTime.TotalSeconds:F2} seconds");
        }
        sb.AppendLine("------------------------------------------------------------------");
        sb.AppendLine("CATEGORY BREAKDOWN:");
        foreach (var target in targets)
        {
            sb.AppendLine($" • [{target.Category}] {target.Name}: {TargetFolderInfo.FormatBytes(target.SizeBytes)} ({target.FileCount:N0} files) - {target.StatusMessage}");
        }
        sb.AppendLine("==================================================================");
        sb.AppendLine("Zero Telemetry • Pure Precision • Deltempo Open Source Project");
        return sb.ToString();
    }
}
