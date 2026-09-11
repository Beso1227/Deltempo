using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using WinTempCleaner.Core.Cleaning;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;
using WinTempCleaner.Services.Providers.CacheResolvers;

namespace WinTempCleaner.Services;

public partial class CleanerService
{
    private static void UiInvoke(Action action)
    {
        try
        {
            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(action);
                return;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Deltempo.Cleaner] UiInvoke dispatcher fallback: {ex.Message}");
        }
        action();
    }



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
                SafetyBadge = "✓ SAFE • Cache",
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
                SafetyBadge = "✓ SAFE • Cache",
                SafetyBadgeColor = "#10B981",
                Description = "OS diagnostic traces, system update scratchpad (C:\\Windows\\Temp)",
                FolderPath = winTemp,
                IconGlyph = "\uE770",
                RequiresAdmin = true,
                HasAccess = isAdmin,
                IsSelected = isAdmin
            },

            // 3. Windows Prefetch
            new TargetFolderInfo
            {
                Id = "WinPrefetch",
                Name = "Windows Prefetch Cache",
                Category = "System & GPU",
                CategoryColor = "#8B5CF6",
                SafetyBadge = "✓ SAFE • Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Stale execution traces & cached startup headers (C:\\Windows\\Prefetch)",
                FolderPath = winPrefetch,
                IconGlyph = "\uE945",
                RequiresAdmin = true,
                HasAccess = isAdmin,
                IsSelected = isAdmin
            },

            // 4. Windows Update Delivery Cache
            new TargetFolderInfo
            {
                Id = "WinUpdateCache",
                Name = "Windows Update Cache",
                Category = "System & GPU",
                CategoryColor = "#EC4899",
                SafetyBadge = "✓ SAFE • Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Downloaded update installers & delivery cache (SoftwareDistribution\\Download)",
                FolderPath = winUpdateDownload,
                IconGlyph = "\uE896",
                RequiresAdmin = true,
                HasAccess = isAdmin,
                IsSelected = isAdmin
            },

            // 5. Windows Upgrade & Setup Leftovers (PC Manager Deep Match)
            new TargetFolderInfo
            {
                Id = "WinUpgradeLeftovers",
                Name = "Windows Upgrade & Setup Leftovers",
                Category = "System & OS",
                CategoryColor = "#F43F5E",
                SafetyBadge = "✓ SAFE • Leftovers",
                SafetyBadgeColor = "#10B981",
                Description = "Old OS installation leftovers, $WINDOWS.~BT, $WINDOWS.~WS, ESD, and Setup scratchpads",
                FolderPath = "Windows Upgrade Leftovers Pool",
                IconGlyph = "\uE777",
                RequiresAdmin = true,
                HasAccess = isAdmin,
                IsSelected = isAdmin
            },

            // 6. Windows Delivery Optimization (WUDO)
            new TargetFolderInfo
            {
                Id = "WinDeliveryOpt",
                Name = "Windows Delivery Optimization",
                Category = "System & OS",
                CategoryColor = "#3B82F6",
                SafetyBadge = "✓ SAFE • Cache",
                SafetyBadgeColor = "#10B981",
                Description = "P2P Windows update delivery chunks and background bits cache (DeliveryOptimization)",
                FolderPath = Path.Combine(winDir, "ServiceProfiles", "NetworkService", "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization", "Cache"),
                IconGlyph = "\uE774",
                RequiresAdmin = true,
                HasAccess = isAdmin,
                IsSelected = isAdmin
            },

            // 7. Windows Component & Font Caches (PC Manager Match)
            new TargetFolderInfo
            {
                Id = "WinComponentCaches",
                Name = "Windows Component & Font Caches",
                Category = "System & OS",
                CategoryColor = "#06B6D4",
                SafetyBadge = "✓ SAFE • Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Windows FontCache, Downloaded Program Files, WinSxS temp, DISM scratch & BranchCache",
                FolderPath = "Windows Components Pool",
                IconGlyph = "\uE790",
                RequiresAdmin = true,
                HasAccess = isAdmin,
                IsSelected = isAdmin
            },

            // 8. Device Driver Packages (PC Manager Match)
            new TargetFolderInfo
            {
                Id = "DeviceDriverPackages",
                Name = "Device Driver Packages & GPU Updates",
                Category = "System & Drivers",
                CategoryColor = "#10B981",
                SafetyBadge = "✓ SAFE • Drivers",
                SafetyBadgeColor = "#10B981",
                Description = "NVIDIA App/GeForce OTA driver packages, AMD & Intel installer caches, DriverStore temp",
                FolderPath = "Device Driver Packages Pool",
                IconGlyph = "\uEA86",
                RequiresAdmin = true,
                HasAccess = isAdmin,
                IsSelected = isAdmin
            },

            // 9. Microsoft Defender Antivirus (PC Manager Match)
            new TargetFolderInfo
            {
                Id = "DefenderAntivirus",
                Name = "Microsoft Defender Support & Scans",
                Category = "Security & Logs",
                CategoryColor = "#06B6D4",
                SafetyBadge = "✓ SAFE • Logs",
                SafetyBadgeColor = "#10B981",
                Description = "Defender support diagnostic logs (MPLog), definition update backups & scan history cache",
                FolderPath = "Defender Support Pool",
                IconGlyph = "\uE83D",
                RequiresAdmin = true,
                HasAccess = isAdmin,
                IsSelected = isAdmin
            },

            // 10. Windows System & Diagnostic Logs (PC Manager Match)
            new TargetFolderInfo
            {
                Id = "WinSystemLogs",
                Name = "Windows System Diagnostic Logs",
                Category = "Diagnostics",
                CategoryColor = "#8B5CF6",
                SafetyBadge = "✓ SAFE • Logs",
                SafetyBadgeColor = "#10B981",
                Description = "CBS, DISM, Panther, SetupAPI, LogFiles (WMI/HTTPERR), and tracing logs",
                FolderPath = "Windows Logs Pool",
                IconGlyph = "\uE7C3",
                RequiresAdmin = true,
                HasAccess = isAdmin,
                IsSelected = isAdmin
            },

            // 11. System Crash Dumps & Minidumps (PC Manager Match)
            new TargetFolderInfo
            {
                Id = "SystemDumps",
                Name = "BSOD Minidumps & Kernel Reports",
                Category = "Diagnostics",
                CategoryColor = "#EF4444",
                SafetyBadge = "✓ SAFE • Dumps",
                SafetyBadgeColor = "#10B981",
                Description = "Windows crash minidumps (*.dmp), MEMORY.DMP, and LiveKernelReports",
                FolderPath = Path.Combine(winDir, "Minidump"),
                IconGlyph = "\uE7BA",
                RequiresAdmin = true,
                HasAccess = isAdmin,
                IsSelected = isAdmin
            },

            // 12. Temporary Internet Files & WebCache (PC Manager Match)
            new TargetFolderInfo
            {
                Id = "TemporaryInternetFiles",
                Name = "Temporary Internet Files & WebCache",
                Category = "Internet Cache",
                CategoryColor = "#F59E0B",
                SafetyBadge = "✓ SAFE • Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Windows INetCache, WebCache, and CryptnetUrlCache certificate content",
                FolderPath = "Temporary Internet Files Pool",
                IconGlyph = "\uE774",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            },

            // 13. GPU & DirectX Shaders
            new TargetFolderInfo
            {
                Id = "GpuShaderCaches",
                Name = "DirectX & GPU Shader Caches",
                Category = "System & GPU",
                CategoryColor = "#8B5CF6",
                SafetyBadge = "✓ SAFE • Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Compiled graphics shaders from NVIDIA, AMD, D3DSCache & Intel",
                FolderPath = Path.Combine(localAppData, "D3DSCache"),
                IconGlyph = "\uE790",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            },

            // 14. Gaming Launchers & Shaders
            new TargetFolderInfo
            {
                Id = "GamingLaunchers",
                Name = "Game Launchers & Shaders",
                Category = "Gaming & Media",
                CategoryColor = "#EC4899",
                SafetyBadge = "✓ SAFE • Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Steam downloads & shaders, Epic Games webcache, Battle.net, EA App, Riot Games, Roblox",
                FolderPath = "Gaming Launchers Pool",
                IconGlyph = "\uE7FC",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            },

            // 15. Media & Creator Render Scratchpads
            new TargetFolderInfo
            {
                Id = "MediaCreatorCaches",
                Name = "Media & Creator Render Caches",
                Category = "Creator & Media",
                CategoryColor = "#F59E0B",
                SafetyBadge = "✓ SAFE • Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Adobe Premiere/After Effects/Photoshop scratch, CapCut cache, DaVinci proxy, OBS logs, Blender temp",
                FolderPath = "Media Creator Caches Pool",
                IconGlyph = "\uE714",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            },

            // 16. Desktop & Electron Apps Cache Sweeper
            new TargetFolderInfo
            {
                Id = "AppCacheSweeper",
                Name = "Desktop Apps Cache Sweeper",
                Category = "User Cache",
                CategoryColor = "#10B981",
                SafetyBadge = "✓ SAFE • Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Disposable GPU & Code Cache in Discord, Spotify, Slack, VS Code, Cursor, Teams, WhatsApp, Notion",
                FolderPath = "App Caches Pool",
                IconGlyph = "\uE715",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            },

            // 17. Windows Store Apps & Modern UWP Caches (PC Manager Match)
            new TargetFolderInfo
            {
                Id = "WinStoreAppCaches",
                Name = "Windows Store Apps & UWP Caches",
                Category = "Store Apps",
                CategoryColor = "#10B981",
                SafetyBadge = "✓ SAFE • Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Temporary LocalCache & INetCache across Windows Store packages (New Teams, Xbox, WhatsApp, etc.)",
                FolderPath = "Windows Store App Packages Pool",
                IconGlyph = "\uE719",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            },

            // 18. Messaging & Social Apps Cache Pool
            new TargetFolderInfo
            {
                Id = "MessagingAppCaches",
                Name = "Messaging & Social Apps Caches",
                Category = "Communication",
                CategoryColor = "#06B6D4",
                SafetyBadge = "✓ SAFE • Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Safe media & GPU caches for WhatsApp, Telegram, Discord, Slack, Teams, Signal, Skype, Viber, Zoom (logins strictly preserved)",
                FolderPath = "Messaging Apps Cache Pool",
                IconGlyph = "\uE8BD",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            },

            // 19. Web Browser Caches (Multi-Profile Engine)
            new TargetFolderInfo
            {
                Id = "BrowserCaches",
                Name = "Web Browsers Cache Pool",
                Category = "User Cache",
                CategoryColor = "#F97316",
                SafetyBadge = "✓ SAFE • Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Chrome, Edge, Brave, Opera, Firefox, Arc, Vivaldi multi-profile web & shader cache (logins preserved)",
                FolderPath = "Browser Web Caches",
                IconGlyph = "\uE774",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            },

            // 19. Developer & Package Caches
            new TargetFolderInfo
            {
                Id = "DevPackageCaches",
                Name = "Developer & Package Caches",
                Category = "Dev Caches",
                CategoryColor = "#06B6D4",
                SafetyBadge = "✓ SAFE • Cache",
                SafetyBadgeColor = "#10B981",
                Description = "pip, npm, yarn, pnpm, NuGet, .gradle, Cargo, Go build, Bun, Deno, and .NET temp caches",
                FolderPath = Path.Combine(localAppData, "pip", "cache"),
                IconGlyph = "\uE7B8",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            },

            // 20. Mobile Sync & Dev Daemons
            new TargetFolderInfo
            {
                Id = "MobileDevResiduals",
                Name = "Mobile Sync & Dev Daemons",
                Category = "Dev & Mobile",
                CategoryColor = "#06B6D4",
                SafetyBadge = "✓ SAFE • Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Apple iTunes temp sync cache, Android Studio emulator cache, Gradle & Cargo caches",
                FolderPath = "Mobile & Dev Residuals Pool",
                IconGlyph = "\uE8EA",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            },

            // 21. Error Reports (WER)
            new TargetFolderInfo
            {
                Id = "CrashDumps",
                Name = "Windows Error Reports (WER)",
                Category = "Diagnostics",
                CategoryColor = "#F59E0B",
                SafetyBadge = "✓ SAFE • Logs",
                SafetyBadgeColor = "#10B981",
                Description = "Windows Error Reporting logs & diagnostic queues (WER ReportArchive/ReportQueue)",
                FolderPath = werPath,
                IconGlyph = "\uE7BA",
                RequiresAdmin = true,
                HasAccess = isAdmin,
                IsSelected = isAdmin
            },

            // 22. Explorer Thumbnails
            new TargetFolderInfo
            {
                Id = "Thumbnails",
                Name = "Explorer Thumbnail Cache",
                Category = "Diagnostics",
                CategoryColor = "#06B6D4",
                SafetyBadge = "✓ SAFE • Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Cached image & video thumbnail databases (thumbcache_*.db)",
                FolderPath = explorerThumbnails,
                IconGlyph = "\uE8B9",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            },

            // 23. System & Explorer Usage Traces (PC Manager Match)
            new TargetFolderInfo
            {
                Id = "SystemUsageTraces",
                Name = "System & Explorer Usage Traces",
                Category = "Privacy Traces",
                CategoryColor = "#3B82F6",
                SafetyBadge = "✓ SAFE • Privacy",
                SafetyBadgeColor = "#10B981",
                Description = "Recent items shortcuts, AutomaticDestinations, and CustomDestinations Jump Lists",
                FolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Recent"),
                IconGlyph = "\uE81C",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            },

            // 24. Windows System Restore Points & Shadow Copies
            new TargetFolderInfo
            {
                Id = "SystemRestorePoints",
                Name = "Windows Restore Points & Shadow Copies",
                Category = "System & OS",
                CategoryColor = "#EC4899",
                SafetyBadge = "✓ SAFE • Restore Points",
                SafetyBadgeColor = "#10B981",
                Description = "System Volume Information shadow copies (VSS). Safely purges older restore points while preserving the latest for recovery.",
                FolderPath = "VSS Shadow Storage (System Volume Information)",
                IconGlyph = "\uE777",
                RequiresAdmin = true,
                HasAccess = isAdmin,
                IsSelected = isAdmin
            },

            // 25. Windows Recycle Bin
            new TargetFolderInfo
            {
                Id = "RecycleBin",
                Name = "Windows Recycle Bin",
                Category = "Storage",
                CategoryColor = "#EF4444",
                SafetyBadge = "✓ SAFE • Cache",
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

        return targets;
    }

    public static async Task<List<TargetFolderInfo>> LoadOrphanedTargetsAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                return OrphanedAppService.ScanVerifiedOrphanedFolders();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
                return new List<TargetFolderInfo>();
            }
        }, ct);
    }

    public async Task ScanFolderAsync(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct, bool safeMode24Hours = false)
    {
        folder.IsScanning = true;
        folder.StatusMessage = "Scanning...";

        if (folder.RequiresAdmin && !ElevationService.IsRunAsAdmin())
        {
            folder.SizeBytes = 0;
            folder.FileCount = 0;
            folder.FolderCount = 0;
            folder.TopFiles = new List<JunkFileItem>();
            folder.StatusMessage = "Requires Admin";
            folder.IsScanning = false;
            return;
        }

        await Task.Run(() =>
        {
            EnableFileManagementPrivileges();

            if (folder.IsSpecialShellTarget && folder.Id == "RecycleBin")
            {
                ScanRecycleBin(folder, logAction);
                UiInvoke(() => folder.IsScanning = false);
                return;
            }

            if (folder.Id == "SystemRestorePoints")
            {
                ScanRestorePoints(folder, logAction);
                UiInvoke(() => folder.IsScanning = false);
                return;
            }

            try
            {
                var targetDirs = ResolveDirectoriesForFolder(folder);
                if (targetDirs.Count > 0)
                {
                    ScanDirectoryList(folder, targetDirs, folder.Name, logAction, ct, safeMode24Hours);
                    UiInvoke(() => folder.IsScanning = false);
                    return;
                }

                UiInvoke(() => folder.SizeBytes = 0);
                UiInvoke(() => folder.FileCount = 0);
                UiInvoke(() => folder.FolderCount = 0);
                UiInvoke(() => folder.TopFiles = new List<JunkFileItem>());
                UiInvoke(() => folder.StatusMessage = "Empty or Not Found");
                UiInvoke(() => folder.IsScanning = false);
                return;
            }
            catch (UnauthorizedAccessException ex)
            {
                UiInvoke(() => folder.StatusMessage = "Access Denied (Admin Required)");
                UiInvoke(() => folder.HasError = true);
                UiInvoke(() => folder.ErrorMessage = ex.Message);
                logAction($"Admin privileges required to scan {folder.Name}: {ex.Message}", LogLevel.Warning);
            }
            catch (Exception ex)
            {
                UiInvoke(() => folder.StatusMessage = "Scan Error");
                UiInvoke(() => folder.HasError = true);
                UiInvoke(() => folder.ErrorMessage = ex.Message);
                logAction($"Error scanning {folder.Name}: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                UiInvoke(() => folder.IsScanning = false);
            }
        }, ct);
    }

    #region Directory Resolvers (Single Source of Truth for Scan & Clean)

    public static List<string> GetUpgradeLeftoverDirectories() => SystemCacheResolver.ResolveUpgradeLeftovers();

    public static List<string> GetComponentCacheDirectories() => SystemCacheResolver.ResolveComponentCaches();

    public static List<string> GetStoreAppCacheDirectories() => StoreAppCacheResolver.Resolve();

    private static void AddEbWebViewSafeCaches(string ebRoot, List<string> dirs) => StoreAppCacheResolver.AddEbWebViewSafeCaches(ebRoot, dirs);

    public static List<string> GetDeviceDriverDirectories() => SystemCacheResolver.ResolveDeviceDriverDirectories();

    public static List<string> GetDefenderDirectories() => SystemCacheResolver.ResolveDefenderDirectories();

    public static List<string> GetWinSystemLogDirectories() => SystemCacheResolver.ResolveWinSystemLogDirectories();

    public static List<string> GetSystemDumpDirectories() => SystemCacheResolver.ResolveSystemDumpDirectories();

    public static List<string> GetTemporaryInternetDirectories() => SystemCacheResolver.ResolveTemporaryInternetDirectories();

    public static List<string> GetDeliveryOptimizationDirectories() => SystemCacheResolver.ResolveDeliveryOptimizationDirectories();

    public static List<string> GetGpuShaderDirectories() => SystemCacheResolver.ResolveGpuShaderDirectories();

    public static List<string> GetGamingLauncherDirectories() => SystemCacheResolver.ResolveGamingLauncherDirectories();

    public static List<string> GetMediaCreatorDirectories() => SystemCacheResolver.ResolveMediaCreatorDirectories();

    public static List<string> GetMobileDevDirectories() => SystemCacheResolver.ResolveMobileDevDirectories();

    public static List<string> GetAppCacheDirectories() => SystemCacheResolver.ResolveAppCacheDirectories();

    public static List<string> GetMessagingAppCacheDirectories() => MessagingAppCacheResolver.Resolve();

    public static List<string> GetBrowserCacheDirectories() => BrowserCacheResolver.Resolve();

    public static List<string> GetDevPackageDirectories() => DevPackageCacheResolver.Resolve();

    #endregion


    private static void ScanDirectoryList(
        TargetFolderInfo folder,
        IEnumerable<string> directories,
        string categoryTitle,
        Action<string, LogLevel> logAction,
        CancellationToken ct,
        bool safeMode24Hours = false)
    {
        long totalBytes = 0;
        int fileCount = 0;
        var topCollector = new WinTempCleaner.Core.Scanning.TopFilesCollector(30);
        var cutoffUtc = DateTime.UtcNow - TimeSpan.FromHours(24);

        foreach (var dir in directories)
        {
            if (ct.IsCancellationRequested) break;
            if (!Directory.Exists(dir)) continue;
            try
            {
                var dirInfo = new DirectoryInfo(dir);
                var enumOptions = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true,
                    AttributesToSkip = FileAttributes.ReparsePoint
                };

                foreach (var f in dirInfo.EnumerateFiles("*", enumOptions))
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        if (IsProtectedFile(f.FullName)) continue;

                        if (safeMode24Hours && folder.IsSafeModeEligible && f.LastWriteTimeUtc > cutoffUtc)
                        {
                            continue;
                        }

                        totalBytes += f.Length;
                        fileCount++;
                        topCollector.TryAdd(f.Name, f.FullName, f.Length, f.LastWriteTime);
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

        UiInvoke(() => folder.SizeBytes = totalBytes);
        UiInvoke(() => folder.FileCount = fileCount);
        UiInvoke(() => folder.TopFiles = topCollector.ToDescendingList(15));
        UiInvoke(() => folder.StatusMessage = $"Ready: {TargetFolderInfo.FormatBytes(totalBytes)}");
        logAction($"Scanned {categoryTitle}: {TargetFolderInfo.FormatBytes(totalBytes)} ({fileCount:N0} files)", LogLevel.Info);
    }

    public static (long sizeBytes, int fileCount) QueryRecycleBinInfo()
    {
        try
        {
            var rbInfo = new SHQUERYRBINFO { cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO)) };
            int hresult = SHQueryRecycleBin(null, ref rbInfo);
            if (hresult == 0)
            {
                return (rbInfo.i64Size, (int)rbInfo.i64NumItems);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] QueryRecycleBinInfo failed: {ex.Message}");
        }
        return (0, 0);
    }

    private static void ScanRecycleBin(TargetFolderInfo folder, Action<string, LogLevel> logAction)
    {
        try
        {
            var (size, count) = QueryRecycleBinInfo();
            if (size > 0 || count > 0)
            {
                UiInvoke(() => folder.SizeBytes = size);
                UiInvoke(() => folder.FileCount = count);
                UiInvoke(() => folder.FolderCount = 0);
                UiInvoke(() => folder.StatusMessage = $"Ready: {TargetFolderInfo.FormatBytes(size)}");
                logAction($"Scanned Recycle Bin: {TargetFolderInfo.FormatBytes(size)} across {count:N0} items", LogLevel.Info);
            }
            else
            {
                UiInvoke(() => folder.StatusMessage = "Empty");
                UiInvoke(() => folder.SizeBytes = 0);
                UiInvoke(() => folder.FileCount = 0);
            }
        }
        catch (Exception ex)
        {
            UiInvoke(() => folder.StatusMessage = "Scan Error");
            UiInvoke(() => folder.HasError = true);
            UiInvoke(() => folder.ErrorMessage = ex.Message);
            logAction($"Error querying Recycle Bin: {ex.Message}", LogLevel.Warning);
        }
    }

    private static void ScanRestorePoints(TargetFolderInfo folder, Action<string, LogLevel> logAction)
    {
        try
        {
            var (usedBytes, snapshotCount) = QueryShadowStorageInfo();
            UiInvoke(() => folder.SizeBytes = usedBytes);
            UiInvoke(() => folder.FileCount = snapshotCount);
            UiInvoke(() => folder.FolderCount = 0);
            if (usedBytes > 0)
            {
                UiInvoke(() => folder.StatusMessage = $"Ready: {TargetFolderInfo.FormatBytes(usedBytes)} ({snapshotCount} snapshots)");
                logAction($"Scanned System Restore Points: {TargetFolderInfo.FormatBytes(usedBytes)} across {snapshotCount} shadow copies", LogLevel.Info);
            }
            else
            {
                UiInvoke(() => folder.StatusMessage = "Clean / None Found");
                logAction("Scanned System Restore Points: No shadow copies found (0 bytes)", LogLevel.Info);
            }
        }
        catch (Exception ex)
        {
            UiInvoke(() => folder.StatusMessage = "Scan Error");
            UiInvoke(() => folder.HasError = true);
            UiInvoke(() => folder.ErrorMessage = ex.Message);
            logAction($"Error querying restore points: {ex.Message}", LogLevel.Warning);
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

        await Task.Run(async () =>
        {
            EnableFileManagementPrivileges();

            if (folder.IsSpecialShellTarget && folder.Id == "RecycleBin")
            {
                try
                {
                    if (folder.SizeBytes == 0 && folder.FileCount == 0)
                    {
                        ScanRecycleBin(folder, (m, l) => { });
                    }
                    long initialSize = folder.SizeBytes;
                    int initialCount = folder.FileCount;
                    uint flags = SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND;
                    int hresult = SHEmptyRecycleBin(IntPtr.Zero, null, flags);
                    if (hresult == 0)
                    {
                        freedBytes = initialSize;
                        filesDeleted = initialCount;
                        UiInvoke(() => folder.SizeBytes = 0);
                        UiInvoke(() => folder.FileCount = 0);
                        UiInvoke(() => folder.StatusMessage = "Emptied successfully");
                        logAction($"Emptied Recycle Bin: {TargetFolderInfo.FormatBytes(freedBytes)} reclaimed", LogLevel.Success);
                    }
                    else
                    {
                        UiInvoke(() => folder.StatusMessage = "Empty");
                    }
                }
                catch (Exception ex)
                {
                    UiInvoke(() => folder.StatusMessage = "Error emptying");
                    logAction($"Error emptying Recycle Bin: {ex.Message}", LogLevel.Error);
                }
                finally
                {
                    UiInvoke(() => folder.IsCleaning = false);
                }
                return;
            }

            if (folder.Id == "SystemRestorePoints")
            {
                try
                {
                    var (ok, reclaimed, msg) = CleanRestorePointsAsync(false, logAction, ct).GetAwaiter().GetResult();
                    if (ok && reclaimed > 0)
                    {
                        freedBytes = reclaimed;
                        filesDeleted = 1;
                        UiInvoke(() => folder.SizeBytes = 0);
                        UiInvoke(() => folder.FileCount = 0);
                        UiInvoke(() => folder.StatusMessage = $"Reclaimed: {TargetFolderInfo.FormatBytes(reclaimed)}");
                    }
                    else
                    {
                        UiInvoke(() => folder.StatusMessage = "Preserved Latest / Clean");
                    }
                }
                catch (Exception ex)
                {
                    UiInvoke(() => folder.StatusMessage = "Error");
                    UiInvoke(() => folder.HasError = true);
                    UiInvoke(() => folder.ErrorMessage = ex.Message);
                    logAction($"Error cleaning restore points: {ex.Message}", LogLevel.Warning);
                }
                finally
                {
                    UiInvoke(() => folder.IsCleaning = false);
                }
                return;
            }

            if (folder.IsOrphanedAppFolder)
            {
                try
                {
                    if (Directory.Exists(folder.FolderPath))
                    {
                        if (folder.SizeBytes == 0)
                        {
                            try
                            {
                                var di = new DirectoryInfo(folder.FolderPath);
                                var files = di.EnumerateFiles("*", SearchOption.AllDirectories).ToList();
                                folder.SizeBytes = files.Sum(f => f.Length);
                                folder.FileCount = files.Count;
                            }
                            catch { }
                        }
                        long initialSize = folder.SizeBytes;
                        int initialFiles = folder.FileCount;
                        bool ok = LargeFileHunterService.MoveToRecycleBin(folder.FolderPath);
                        if (ok)
                        {
                            freedBytes = initialSize;
                            filesDeleted = initialFiles;
                            foldersDeleted = 1;
                            UiInvoke(() => folder.SizeBytes = 0);
                            UiInvoke(() => folder.FileCount = 0);
                            UiInvoke(() => folder.StatusMessage = "Moved to Recycle Bin (Undoable)");
                            logAction($"Safely recycled residual folder '{folder.FolderPath}' to Windows Recycle Bin ({TargetFolderInfo.FormatBytes(initialSize)})", LogLevel.Success);
                        }
                        else
                        {
                            UiInvoke(() => folder.StatusMessage = "In Use or Locked");
                            logAction($"Could not recycle '{folder.FolderPath}'. File may be in use or require admin rights.", LogLevel.Warning);
                        }
                    }
                }
                catch (Exception ex)
                {
                    UiInvoke(() => folder.StatusMessage = "Error");
                    UiInvoke(() => folder.HasError = true);
                    UiInvoke(() => folder.ErrorMessage = ex.Message);
                    logAction($"Error cleaning residual folder '{folder.FolderPath}': {ex.Message}", LogLevel.Error);
                }
                finally
                {
                    UiInvoke(() => folder.IsCleaning = false);
                }
                return;
            }

            var directoriesToClean = ResolveDirectoriesForFolder(folder);

            // Use the authoritative CleanupPlanner → CleanupExecutor path
            bool applyShield = safeMode24Hours && folder.IsSafeModeEligible;
            bool sendToRecycle = SettingsService.Current.SendToRecycleBin;

            var plan = CleanupPlanner.CreatePlan(
                scopeId: folder.Id,
                scopeName: folder.Name,
                directories: directoriesToClean,
                category: folder.Category,
                apply24HourShield: applyShield,
                sendToRecycleBin: sendToRecycle,
                ct: ct);

            var txResult = await CleanupExecutor.ExecutePlanAsync(
                plan,
                directoriesToClean,
                logAction,
                progressReport,
                ct).ConfigureAwait(false);

            freedBytes = txResult.TotalFreedBytes;
            filesDeleted = txResult.TotalItemsFreed;
            filesSkipped = txResult.SkippedCount + txResult.ReviewRequiredCount + txResult.UnknownCount + txResult.FailedCount;

            // Clean empty subdirectories safely (Targeted bottom-up pruning from affected directories)
            var candidateDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (txResult.AffectedParentDirectories.Count > 0)
            {
                foreach (var parentDir in txResult.AffectedParentDirectories)
                {
                    var current = parentDir;
                    while (!string.IsNullOrEmpty(current))
                    {
                        bool isInsideTarget = false;
                        foreach (var targetPath in directoriesToClean)
                        {
                            if (current.StartsWith(targetPath, StringComparison.OrdinalIgnoreCase) &&
                                !current.Equals(targetPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                            {
                                isInsideTarget = true;
                                break;
                            }
                        }

                        if (!isInsideTarget) break;

                        candidateDirs.Add(current);

                        try
                        {
                            var parent = Directory.GetParent(current);
                            current = parent?.FullName;
                        }
                        catch
                        {
                            break;
                        }
                    }
                }
            }
            else
            {
                // Fallback for edge cases
                foreach (var targetPath in directoriesToClean)
                {
                    if (ct.IsCancellationRequested) break;
                    if (!Directory.Exists(targetPath)) continue;

                    try
                    {
                        var dirInfo = new DirectoryInfo(targetPath);
                        var enumOptions = new EnumerationOptions
                        {
                            IgnoreInaccessible = true,
                            RecurseSubdirectories = true,
                            AttributesToSkip = FileAttributes.ReparsePoint
                        };
                        foreach (var subDir in dirInfo.EnumerateDirectories("*", enumOptions))
                        {
                            candidateDirs.Add(subDir.FullName);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed directory enumeration: {ex.Message}");
                    }
                }
            }

            var subCheckOptions = new EnumerationOptions { IgnoreInaccessible = true };
            foreach (var dirPath in candidateDirs.OrderByDescending(p => p.Length))
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    if (!Directory.Exists(dirPath)) continue;
                    if (ProtectionPolicy.IsProtected(dirPath, out _)) continue;
                    if (PathSecurity.IsReparsePointOrLink(dirPath)) continue;

                    var dirInfo = new DirectoryInfo(dirPath);
                    if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0) continue;

                    if (!dirInfo.EnumerateFileSystemInfos("*", subCheckOptions).Any())
                    {
                        if ((dirInfo.Attributes & FileAttributes.ReadOnly) != 0)
                        {
                            try { dirInfo.Attributes = FileAttributes.Normal; }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[Deltempo.Cleaner] Normalizing attributes failed: {ex.Message}");
                            }
                        }

                        if (RemoveDirectoryW(dirInfo.FullName) || !Directory.Exists(dirInfo.FullName))
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

            // Flush Explorer thumbnail databases if cleaned
            if (folder.Id == "Thumbnails")
            {
                try
                {
                    SHChangeNotify(0x08000000 /* SHCNE_ASSOCCHANGED */, 0x0000 /* SHCNF_IDLIST */, IntPtr.Zero, IntPtr.Zero);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Deltempo.Cleaner] SHChangeNotify failed: {ex.Message}");
                }
            }

            UiInvoke(() => folder.SizeBytes = Math.Max(0, folder.SizeBytes - freedBytes));
            UiInvoke(() => folder.FileCount = Math.Max(0, folder.FileCount - filesDeleted));

            if (freedBytes > 0)
            {
                UiInvoke(() => folder.StatusMessage = $"Reclaimed: {TargetFolderInfo.FormatBytes(freedBytes)}");
                logAction($"Cleaned {folder.Name}: {TargetFolderInfo.FormatBytes(freedBytes)} reclaimed ({filesDeleted:N0} files deleted, {filesSkipped:N0} skipped/protected/failed)", LogLevel.Success);
            }
            else if (filesSkipped > 0)
            {
                UiInvoke(() => folder.StatusMessage = $"Protected ({filesSkipped:N0} items)");
                logAction($"Protected {folder.Name}: {filesSkipped:N0} files skipped by safety engine, protection policy, or verification", LogLevel.Info);
            }
            else
            {
                UiInvoke(() => folder.StatusMessage = "Already Clean (0 B)");
                logAction($"Checked {folder.Name}: Already clean (0 bytes)", LogLevel.Info);
            }

            UiInvoke(() => folder.IsCleaning = false);
        }, ct);

        return (freedBytes, filesDeleted, foldersDeleted, filesSkipped);
    }

    private static List<string> ResolveDirectoriesForFolder(TargetFolderInfo folder)
    {
        var dirs = new List<string>();

        if (folder.Id == "WinUpgradeLeftovers") dirs.AddRange(GetUpgradeLeftoverDirectories());
        else if (folder.Id == "WinComponentCaches") dirs.AddRange(GetComponentCacheDirectories());
        else if (folder.Id == "WinStoreAppCaches") dirs.AddRange(GetStoreAppCacheDirectories());
        else if (folder.Id == "DeviceDriverPackages") dirs.AddRange(GetDeviceDriverDirectories());
        else if (folder.Id == "DefenderAntivirus") dirs.AddRange(GetDefenderDirectories());
        else if (folder.Id == "WinSystemLogs") dirs.AddRange(GetWinSystemLogDirectories());
        else if (folder.Id == "SystemDumps") dirs.AddRange(GetSystemDumpDirectories());
        else if (folder.Id == "TemporaryInternetFiles") dirs.AddRange(GetTemporaryInternetDirectories());
        else if (folder.Id == "SystemUsageTraces") dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Recent"));
        else if (folder.Id == "GpuShaderCaches") dirs.AddRange(GetGpuShaderDirectories());
        else if (folder.Id == "GamingLaunchers") dirs.AddRange(GetGamingLauncherDirectories());
        else if (folder.Id == "MediaCreatorCaches") dirs.AddRange(GetMediaCreatorDirectories());
        else if (folder.Id == "MobileDevResiduals") dirs.AddRange(GetMobileDevDirectories());
        else if (folder.Id == "AppCacheSweeper") dirs.AddRange(GetAppCacheDirectories());
        else if (folder.Id == "MessagingAppCaches") dirs.AddRange(GetMessagingAppCacheDirectories());
        else if (folder.Id == "BrowserCaches") dirs.AddRange(GetBrowserCacheDirectories());
        else if (folder.Id == "DevPackageCaches") dirs.AddRange(GetDevPackageDirectories());
        else if (folder.Id == "WinDeliveryOpt") dirs.AddRange(GetDeliveryOptimizationDirectories());
        else if (!string.IsNullOrWhiteSpace(folder.FolderPath)) dirs.Add(folder.FolderPath);

        return dirs;
    }

    /// <summary>
    /// Public wrapper for dry-run planning. Returns resolved directories for a target folder.
    /// </summary>
    public static List<string> ResolveDirectoriesForFolderPublic(TargetFolderInfo folder) => ResolveDirectoriesForFolder(folder);



    public static readonly string[] CommunicationAppKeywords =
    {
        "whatsapp", "telegram", "msteams", "teams", "discord", "slack", "signal",
        "skype", "zoom", "viber", "element", "wechat", "line", "kakao", "messenger",
        "session", "threema", "wire", "icq", "mattermost", "webex", "cisco-spark", "ciscospark",
        "ringcentral", "thunderbird", "outlook", "rocketchat", "keybase", "zulip", "chime", "flock",
        "matrix", "accountscontrol", "aad", "cloudexperiencehost", "bioenrollment", "auth"
    };

    public static bool IsProtectedSessionOrCredentialFile(string filePath)
    {
        return ProtectionPolicy.IsProtected(filePath, out _);
    }

    public static bool IsProtectedFile(string filePath)
    {
        return ProtectionPolicy.IsProtected(filePath, out _);
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
        sb.AppendLine("Deltempo - Windows Cleaner and Memory Optimizer (MIT Licensed)");
        return sb.ToString();
    }
}
