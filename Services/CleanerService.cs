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
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var winTemp = Path.Combine(winDir, "Temp");
        var winPrefetch = Path.Combine(winDir, "Prefetch");
        var winUpdateDownload = Path.Combine(winDir, "SoftwareDistribution", "Download");
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
                FolderPath = Path.Combine(winDir, "ServiceProfiles", "NetworkService", "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization", "Cache"),
                IconGlyph = "\uE774",
                RequiresAdmin = true,
                HasAccess = isAdmin,
                IsSelected = true
            },

            // 6. Device Driver Packages (PC Manager Match)
            new TargetFolderInfo
            {
                Id = "DeviceDriverPackages",
                Name = "Device Driver Packages & GPU Updates",
                Category = "System & Drivers",
                CategoryColor = "#10B981",
                SafetyBadge = "🟢 100% Safe Drivers",
                SafetyBadgeColor = "#10B981",
                Description = "NVIDIA App/GeForce OTA driver packages, AMD & Intel installer caches, DriverStore temp",
                FolderPath = "Device Driver Packages Pool",
                IconGlyph = "\uEA86",
                RequiresAdmin = true,
                HasAccess = isAdmin,
                IsSelected = true
            },

            // 7. Microsoft Defender Antivirus (PC Manager Match)
            new TargetFolderInfo
            {
                Id = "DefenderAntivirus",
                Name = "Microsoft Defender Support & Scans",
                Category = "Security & Logs",
                CategoryColor = "#06B6D4",
                SafetyBadge = "🟢 100% Safe Logs",
                SafetyBadgeColor = "#10B981",
                Description = "Defender support diagnostic logs (MPLog), definition update backups & scan history cache",
                FolderPath = "Defender Support Pool",
                IconGlyph = "\uE83D",
                RequiresAdmin = true,
                HasAccess = isAdmin,
                IsSelected = true
            },

            // 8. Windows System & Diagnostic Logs (PC Manager Match)
            new TargetFolderInfo
            {
                Id = "WinSystemLogs",
                Name = "Windows System Diagnostic Logs",
                Category = "Diagnostics",
                CategoryColor = "#8B5CF6",
                SafetyBadge = "🟢 100% Safe Logs",
                SafetyBadgeColor = "#10B981",
                Description = "CBS, DISM, Panther, SetupAPI, LogFiles (WMI/HTTPERR), and tracing logs",
                FolderPath = "Windows Logs Pool",
                IconGlyph = "\uE7C3",
                RequiresAdmin = true,
                HasAccess = isAdmin,
                IsSelected = true
            },

            // 9. System Crash Dumps & Minidumps (PC Manager Match)
            new TargetFolderInfo
            {
                Id = "SystemDumps",
                Name = "BSOD Minidumps & Kernel Reports",
                Category = "Diagnostics",
                CategoryColor = "#EF4444",
                SafetyBadge = "🟢 100% Safe Dumps",
                SafetyBadgeColor = "#10B981",
                Description = "Windows crash minidumps (*.dmp), MEMORY.DMP, and LiveKernelReports",
                FolderPath = Path.Combine(winDir, "Minidump"),
                IconGlyph = "\uE7BA",
                RequiresAdmin = true,
                HasAccess = isAdmin,
                IsSelected = true
            },

            // 10. Temporary Internet Files & WebCache (PC Manager Match)
            new TargetFolderInfo
            {
                Id = "TemporaryInternetFiles",
                Name = "Temporary Internet Files & WebCache",
                Category = "Internet Cache",
                CategoryColor = "#F59E0B",
                SafetyBadge = "🟢 100% Safe Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Windows INetCache, WebCache, and CryptnetUrlCache certificate content",
                FolderPath = "Temporary Internet Files Pool",
                IconGlyph = "\uE774",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            },

            // 11. GPU & DirectX Shaders
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

            // 12. Gaming Launchers & Shaders
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

            // 13. Media & Creator Render Scratchpads
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

            // 14. Desktop & Electron Apps Cache Sweeper
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

            // 15. Web Browser Caches
            new TargetFolderInfo
            {
                Id = "BrowserCaches",
                Name = "Web Browsers Cache Pool",
                Category = "User Cache",
                CategoryColor = "#F97316",
                SafetyBadge = "🟢 100% Safe Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Chrome, Edge, Brave, Opera, Firefox web cache & code cache (cookies and logins preserved)",
                FolderPath = "Browser Web Caches",
                IconGlyph = "\uE774",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            },

            // 16. Developer & Package Caches
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

            // 17. Mobile Sync & Dev Daemons
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

            // 18. Error Reports (WER)
            new TargetFolderInfo
            {
                Id = "CrashDumps",
                Name = "Windows Error Reports (WER)",
                Category = "Diagnostics",
                CategoryColor = "#F59E0B",
                SafetyBadge = "🟢 100% Safe Logs",
                SafetyBadgeColor = "#10B981",
                Description = "Windows Error Reporting logs & diagnostic queues (WER ReportArchive/ReportQueue)",
                FolderPath = werPath,
                IconGlyph = "\uE7BA",
                RequiresAdmin = true,
                HasAccess = isAdmin,
                IsSelected = true
            },

            // 19. Explorer Thumbnails
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

            // 20. System & Explorer Usage Traces (PC Manager Match)
            new TargetFolderInfo
            {
                Id = "SystemUsageTraces",
                Name = "System & Explorer Usage Traces",
                Category = "Privacy Traces",
                CategoryColor = "#3B82F6",
                SafetyBadge = "🟢 100% Safe Privacy",
                SafetyBadgeColor = "#10B981",
                Description = "Recent items shortcuts, AutomaticDestinations, and CustomDestinations Jump Lists",
                FolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Recent"),
                IconGlyph = "\uE81C",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            },

            // 21. Windows Recycle Bin
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

        // 22. Orphaned Uninstalled AppData Leftovers
        try
        {
            var orphans = OrphanedAppService.ScanVerifiedOrphanedFolders();
            foreach (var o in orphans)
            {
                targets.Add(o);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }

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

            if (folder.Id == "DeviceDriverPackages")
            {
                ScanDeviceDriverPackages(folder, logAction, ct);
                folder.IsScanning = false;
                return;
            }

            if (folder.Id == "DefenderAntivirus")
            {
                ScanDefenderAntivirus(folder, logAction, ct);
                folder.IsScanning = false;
                return;
            }

            if (folder.Id == "WinSystemLogs")
            {
                ScanWinSystemLogs(folder, logAction, ct);
                folder.IsScanning = false;
                return;
            }

            if (folder.Id == "SystemDumps")
            {
                ScanSystemDumps(folder, logAction, ct);
                folder.IsScanning = false;
                return;
            }

            if (folder.Id == "TemporaryInternetFiles")
            {
                ScanTemporaryInternetFiles(folder, logAction, ct);
                folder.IsScanning = false;
                return;
            }

            if (folder.Id == "SystemUsageTraces")
            {
                ScanSystemUsageTraces(folder, logAction, ct);
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
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
                    }
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

    private static void ScanDeviceDriverPackages(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        string[] driverDirs =
        {
            Path.Combine(progData, "NVIDIA Corporation", "NVIDIA App", "UpdateFramework", "ota-artifacts"),
            Path.Combine(progData, "NVIDIA Corporation", "Downloader"),
            Path.Combine(progData, "NVIDIA", "Updates"),
            Path.Combine(progData, "AMD"),
            Path.Combine(progData, "Intel"),
            Path.Combine(winDir, "System32", "DriverStore", "Temp"),
            Path.Combine(winDir, "System32", "DriverState")
        };

        ScanDirectoryList(folder, driverDirs, "Device Driver Packages & GPU Updates", logAction, ct);
    }

    private static void ScanDefenderAntivirus(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        string[] defenderDirs =
        {
            Path.Combine(progData, "Microsoft", "Windows Defender", "Support"),
            Path.Combine(progData, "Microsoft", "Windows Defender", "Definition Updates", "Backup"),
            Path.Combine(progData, "Microsoft", "Windows Defender", "Scans", "History", "Results", "Quick"),
            Path.Combine(progData, "Microsoft", "Windows Defender", "Scans", "History", "Results", "Resource")
        };

        ScanDirectoryList(folder, defenderDirs, "Microsoft Defender Antivirus Support", logAction, ct);
    }

    private static void ScanWinSystemLogs(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        string[] logDirs =
        {
            Path.Combine(winDir, "Logs"),
            Path.Combine(winDir, "Debug"),
            Path.Combine(winDir, "System32", "LogFiles"),
            Path.Combine(winDir, "tracing")
        };

        ScanDirectoryList(folder, logDirs, "Windows Diagnostic Logs", logAction, ct);
    }

    private static void ScanSystemDumps(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        string[] dumpDirs =
        {
            Path.Combine(winDir, "Minidump"),
            Path.Combine(winDir, "LiveKernelReports"),
            Path.Combine(winDir, "System32", "CrashDump")
        };

        ScanDirectoryList(folder, dumpDirs, "BSOD Minidumps & Kernel Reports", logAction, ct);
    }

    private static void ScanTemporaryInternetFiles(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string[] inetDirs =
        {
            Path.Combine(localAppData, "Microsoft", "Windows", "INetCache"),
            Path.Combine(localAppData, "Microsoft", "Windows", "WebCache"),
            Path.Combine(localAppData, "Microsoft", "Windows", "Caches"),
            Path.Combine(userProfile, "AppData", "LocalLow", "Microsoft", "CryptnetUrlCache")
        };

        ScanDirectoryList(folder, inetDirs, "Temporary Internet Files & WebCache", logAction, ct);
    }

    private static void ScanSystemUsageTraces(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        string[] traceDirs =
        {
            Path.Combine(roamingAppData, "Microsoft", "Windows", "Recent")
        };

        ScanDirectoryList(folder, traceDirs, "System & Explorer Usage Traces", logAction, ct);
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
            Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Code Cache"),
            Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "GPUCache"),
            Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "DawnCache"),
            Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "ShaderCache"),
            Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "GrShaderCache"),
            Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Service Worker", "CacheStorage"),

            Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Cache", "Cache_Data"),
            Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Code Cache"),
            Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "GPUCache"),
            Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "DawnCache"),
            Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "ShaderCache"),
            Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "GrShaderCache"),

            Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data", "Default", "Cache", "Cache_Data"),
            Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data", "Default", "Code Cache"),
            Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data", "Default", "GPUCache"),
            Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data", "Default", "DawnCache"),
            Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data", "Default", "ShaderCache"),

            Path.Combine(localAppData, "Opera Software", "Opera Stable", "Cache"),
            Path.Combine(localAppData, "Opera Software", "Opera Stable", "GPUCache"),
            Path.Combine(localAppData, "Vivaldi", "User Data", "Default", "Cache", "Cache_Data"),
            Path.Combine(localAppData, "Vivaldi", "User Data", "Default", "Code Cache")
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
            Path.Combine(localAppData, "Yarn", "Cache"),
            Path.Combine(localAppData, "pnpm", "store", "v3"),
            Path.Combine(localAppData, "pnpm-cache"),
            Path.Combine(localAppData, "NuGet", "v3-cache"),
            Path.Combine(userProfile, ".cache"),
            Path.Combine(userProfile, ".gradle", "caches"),
            Path.Combine(userProfile, ".cargo", "registry", "cache"),
            Path.Combine(userProfile, ".cargo", "git", "db"),
            Path.Combine(userProfile, ".bun", "install", "cache"),
            Path.Combine(localAppData, "deno", "deps"),
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
            var progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            if (folder.Id == "DeviceDriverPackages")
            {
                directoriesToClean.Add(Path.Combine(progData, "NVIDIA Corporation", "NVIDIA App", "UpdateFramework", "ota-artifacts"));
                directoriesToClean.Add(Path.Combine(progData, "NVIDIA Corporation", "Downloader"));
                directoriesToClean.Add(Path.Combine(progData, "NVIDIA", "Updates"));
                directoriesToClean.Add(Path.Combine(progData, "AMD"));
                directoriesToClean.Add(Path.Combine(progData, "Intel"));
                directoriesToClean.Add(Path.Combine(winDir, "System32", "DriverStore", "Temp"));
                directoriesToClean.Add(Path.Combine(winDir, "System32", "DriverState"));
            }
            else if (folder.Id == "DefenderAntivirus")
            {
                directoriesToClean.Add(Path.Combine(progData, "Microsoft", "Windows Defender", "Support"));
                directoriesToClean.Add(Path.Combine(progData, "Microsoft", "Windows Defender", "Definition Updates", "Backup"));
                directoriesToClean.Add(Path.Combine(progData, "Microsoft", "Windows Defender", "Scans", "History", "Results", "Quick"));
                directoriesToClean.Add(Path.Combine(progData, "Microsoft", "Windows Defender", "Scans", "History", "Results", "Resource"));
            }
            else if (folder.Id == "WinSystemLogs")
            {
                directoriesToClean.Add(Path.Combine(winDir, "Logs"));
                directoriesToClean.Add(Path.Combine(winDir, "Debug"));
                directoriesToClean.Add(Path.Combine(winDir, "System32", "LogFiles"));
                directoriesToClean.Add(Path.Combine(winDir, "tracing"));
            }
            else if (folder.Id == "SystemDumps")
            {
                directoriesToClean.Add(Path.Combine(winDir, "Minidump"));
                directoriesToClean.Add(Path.Combine(winDir, "LiveKernelReports"));
                directoriesToClean.Add(Path.Combine(winDir, "System32", "CrashDump"));
            }
            else if (folder.Id == "TemporaryInternetFiles")
            {
                directoriesToClean.Add(Path.Combine(localAppData, "Microsoft", "Windows", "INetCache"));
                directoriesToClean.Add(Path.Combine(localAppData, "Microsoft", "Windows", "WebCache"));
                directoriesToClean.Add(Path.Combine(localAppData, "Microsoft", "Windows", "Caches"));
                directoriesToClean.Add(Path.Combine(userProfile, "AppData", "LocalLow", "Microsoft", "CryptnetUrlCache"));
            }
            else if (folder.Id == "SystemUsageTraces")
            {
                directoriesToClean.Add(Path.Combine(roamingAppData, "Microsoft", "Windows", "Recent"));
            }
            else if (folder.Id == "GpuShaderCaches")
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
                directoriesToClean.Add(Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Code Cache"));
                directoriesToClean.Add(Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "GPUCache"));
                directoriesToClean.Add(Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "DawnCache"));
                directoriesToClean.Add(Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "ShaderCache"));
                directoriesToClean.Add(Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "GrShaderCache"));
                directoriesToClean.Add(Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Service Worker", "CacheStorage"));

                directoriesToClean.Add(Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Cache", "Cache_Data"));
                directoriesToClean.Add(Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Code Cache"));
                directoriesToClean.Add(Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "GPUCache"));
                directoriesToClean.Add(Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "DawnCache"));
                directoriesToClean.Add(Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "ShaderCache"));
                directoriesToClean.Add(Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "GrShaderCache"));

                directoriesToClean.Add(Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data", "Default", "Cache", "Cache_Data"));
                directoriesToClean.Add(Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data", "Default", "Code Cache"));
                directoriesToClean.Add(Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data", "Default", "GPUCache"));
                directoriesToClean.Add(Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data", "Default", "DawnCache"));
                directoriesToClean.Add(Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data", "Default", "ShaderCache"));

                directoriesToClean.Add(Path.Combine(localAppData, "Opera Software", "Opera Stable", "Cache"));
                directoriesToClean.Add(Path.Combine(localAppData, "Opera Software", "Opera Stable", "GPUCache"));
                directoriesToClean.Add(Path.Combine(localAppData, "Vivaldi", "User Data", "Default", "Cache", "Cache_Data"));
                directoriesToClean.Add(Path.Combine(localAppData, "Vivaldi", "User Data", "Default", "Code Cache"));
            }
            else if (folder.Id == "DevPackageCaches")
            {
                directoriesToClean.Add(Path.Combine(localAppData, "pip", "cache"));
                directoriesToClean.Add(Path.Combine(localAppData, "npm-cache"));
                directoriesToClean.Add(Path.Combine(localAppData, "Yarn", "Cache"));
                directoriesToClean.Add(Path.Combine(localAppData, "NuGet", "v3-cache"));
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
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
                    }

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
                            string path = file.FullName;

                            if (DeleteFileW(path))
                            {
                                freedBytes += fileLen;
                                filesDeleted++;
                            }
                            else
                            {
                                try
                                {
                                    file.Attributes = FileAttributes.Normal;
                                    file.Delete();
                                    freedBytes += fileLen;
                                    filesDeleted++;
                                }
                                catch
                                {
                                    filesSkipped++;
                                }
                            }
                        }
                        catch
                        {
                            filesSkipped++;
                        }

                        processedFiles++;
                        var now = Environment.TickCount64;
                        if (now - lastProgressTime > 150 && totalFileCount > 0)
                        {
                            lastProgressTime = now;
                            progressReport((double)processedFiles / totalFileCount);
                        }
                    }

                    // Delete empty subdirectories safely
                    try
                    {
                        foreach (var subDir in dirInfo.EnumerateDirectories("*", SearchOption.AllDirectories).OrderByDescending(d => d.FullName.Length))
                        {
                            if (ct.IsCancellationRequested) break;
                            try
                            {
                                if (!subDir.EnumerateFileSystemInfos().Any())
                                {
                                    if (RemoveDirectoryW(subDir.FullName) || !Directory.Exists(subDir.FullName))
                                    {
                                        foldersDeleted++;
                                    }
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
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
                }
            }

            folder.SizeBytes = Math.Max(0, folder.SizeBytes - freedBytes);
            folder.FileCount = Math.Max(0, folder.FileCount - filesDeleted);
            folder.StatusMessage = $"Reclaimed: {TargetFolderInfo.FormatBytes(freedBytes)}";

            logAction($"Cleaned {folder.Name}: {TargetFolderInfo.FormatBytes(freedBytes)} reclaimed ({filesDeleted:N0} files deleted, {filesSkipped:N0} protected)",
                filesDeleted > 0 ? LogLevel.Success : LogLevel.Info);

            folder.IsCleaning = false;
        }, ct);

        return (freedBytes, filesDeleted, foldersDeleted, filesSkipped);
    }

    private static bool IsProtectedFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        string[] dangerousExtensions = { ".exe", ".dll", ".sys", ".drv", ".msc", ".bat", ".cmd", ".vbs", ".ps1", ".docx", ".xlsx", ".pptx", ".pdf", ".psd", ".key", ".kdbx" };

        if (dangerousExtensions.Contains(ext))
        {
            var fileName = Path.GetFileName(filePath).ToLowerInvariant();
            if (filePath.Contains("temp", StringComparison.OrdinalIgnoreCase) ||
                filePath.Contains("cache", StringComparison.OrdinalIgnoreCase) ||
                filePath.Contains("download", StringComparison.OrdinalIgnoreCase) ||
                filePath.Contains("ota-artifacts", StringComparison.OrdinalIgnoreCase) ||
                filePath.Contains("wer", StringComparison.OrdinalIgnoreCase) ||
                filePath.Contains("logs", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return true;
        }

        return false;
    }

    public static string GenerateAuditReport(IEnumerable<TargetFolderInfo> targets, CleanSummary summary, bool safeMode)
    {
        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine("                  DELTEMPO SYSTEM PURGE & AUDIT REPORT                          ");
        sb.AppendLine("================================================================================");
        sb.AppendLine($"Timestamp        : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Safety Shield    : {(safeMode ? "ENABLED (Protected items modified in last 24h)" : "DISABLED")}");
        sb.AppendLine($"Total Disk Freed : {summary.FormattedFreedSize} ({summary.TotalFreedBytes:N0} bytes)");
        sb.AppendLine($"Files Removed    : {summary.TotalFilesDeleted:N0}");
        sb.AppendLine($"Folders Cleaned  : {summary.TotalFoldersDeleted:N0}");
        sb.AppendLine($"Files Protected  : {summary.TotalFilesSkipped:N0}");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("CATEGORIES PROCESSED:");
        foreach (var t in targets)
        {
            sb.AppendLine($"  • [{t.Category}] {t.Name,-35} : {t.StatusMessage}");
        }
        sb.AppendLine("================================================================================");
        sb.AppendLine("Deltempo - Pure Precision Windows Optimizer • 100% Free & Open Source");
        return sb.ToString();
    }
}
