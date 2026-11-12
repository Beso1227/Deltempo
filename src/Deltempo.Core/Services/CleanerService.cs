using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using WinTempCleaner.Core.Cleaning;
using WinTempCleaner.Core.Discovery;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Core.Scanning;
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

        // No WPF Application (the WinUI 3 shell): ask the host how to reach its UI
        // thread instead of running the callback inline on a worker thread.
        UiDispatcher.Invoke(action);
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

            // 4b. Windows Update Cleanup — the component-store scope Disk Cleanup offers
            // after an update. Irreversible, so it is never pre-selected and never runs
            // automatically; the user must tick it (or name it on the CLI).
            new TargetFolderInfo
            {
                Id = "WinUpdateCleanup",
                Name = "Windows Update Cleanup (Component Store)",
                Category = "System & OS",
                CategoryColor = "#EC4899",
                SafetyBadge = "⚠ REVIEW • Irreversible",
                SafetyBadgeColor = "#F59E0B",
                Description = "Superseded update payloads in WinSxS reclaimed via DISM /StartComponentCleanup /ResetBase. Installed updates can no longer be uninstalled.",
                FolderPath = "Component Store (WinSxS) & servicing stack",
                IconGlyph = "\uE896",
                RequiresAdmin = true,
                HasAccess = isAdmin,
                IsSelected = false,
                IsSafeModeEligible = false,
                IrreversibleWarning = "Windows Update Cleanup permanently removes superseded update payloads: installed updates can no longer be uninstalled after this runs."
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
                Description = "Superseded & never-installed DriverStore packages (pnputil), NVIDIA App/GeForce OTA packages, AMD & Intel installer caches",
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

            // 22. Explorer Thumbnails (thumbcache)
            new TargetFolderInfo
            {
                Id = "Thumbnails",
                Name = "Explorer Thumbnail Cache (thumbcache)",
                Category = "Diagnostics",
                CategoryColor = "#06B6D4",
                SafetyBadge = "✓ SAFE • Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Windows Explorer thumbnail and icon cache databases (thumbcache_*.db, iconcache_*.db)",
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

    public static async Task<List<TargetFolderInfo>> LoadStoreOrphanTargetsAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                return StoreAppOrphanResolver.Resolve();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Store orphan scan failed: {ex.Message}");
                return new List<TargetFolderInfo>();
            }
        }, ct);
    }

    public static async Task<List<TargetFolderInfo>> LoadDynamicRulepackTargetsAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var isAdmin = ElevationService.IsRunAsAdmin();
                var builtIn = RulepackEngine.GetBuiltInRulepacks();

                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string userRulesDir = Path.Combine(appData, "Deltempo", "rules");
                var userRules = RulepackEngine.LoadUserRulepacks(userRulesDir);

                var allRules = builtIn.Concat(userRules);
                var targets = RulepackEngine.BuildTargetFolderInfos(allRules, isAdmin);
                foreach (var t in targets)
                {
                    t.DiscoveryTag = "[Rulepack]";
                }
                return targets;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] LoadDynamicRulepackTargetsAsync error: {ex.Message}");
                return new List<TargetFolderInfo>();
            }
        }, ct);
    }

    public async Task ScanFolderAsync(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct, bool safeMode24Hours = false)
    {
        ArgumentNullException.ThrowIfNull(folder);

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

        await Task.Run(async () =>
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

            if (folder.Id == "WinUpdateCleanup")
            {
                await ScanWindowsUpdateCleanupAsync(folder, logAction, ct).ConfigureAwait(false);
                UiInvoke(() => folder.IsScanning = false);
                return;
            }

            if (folder.Id == "DeviceDriverPackages")
            {
                var targetDirs = ResolveDirectoriesForFolder(folder);
                if (targetDirs.Count > 0)
                {
                    ScanDirectoryList(folder, targetDirs, folder.Name, logAction, ct, safeMode24Hours);
                }

                try
                {
                    var driverStoreResult = await WindowsDriverMaintenanceService.ScanSupersededDriverPackagesAsync(ct).ConfigureAwait(false);
                    if (driverStoreResult.TotalSupersededBytes > 0 || driverStoreResult.SupersededPackageCount > 0)
                    {
                        UiInvoke(() =>
                        {
                            folder.SizeBytes += driverStoreResult.TotalSupersededBytes;
                            folder.FileCount += driverStoreResult.SupersededPackageCount;

                            var currentTop = folder.TopFiles?.ToList() ?? new List<JunkFileItem>();
                            foreach (var pkg in driverStoreResult.SupersededPackages.OrderByDescending(p => p.EstimatedSizeBytes).Take(15))
                            {
                                currentTop.Add(new JunkFileItem
                                {
                                    FileName = $"{pkg.PublishedName} ({pkg.OriginalName})",
                                    FilePath = string.IsNullOrEmpty(pkg.FolderPath) ? $"DriverStore\\{pkg.PublishedName}" : pkg.FolderPath,
                                    SizeBytes = pkg.EstimatedSizeBytes,
                                    LastModified = pkg.DriverDate?.ToLocalTime() ?? DateTime.Now
                                });
                            }
                            folder.TopFiles = currentTop.OrderByDescending(f => f.SizeBytes).Take(30).ToList();
                            folder.StatusMessage = driverStoreResult.SupersededPackages.Any(p => p.State == DriverPackageState.Staged)
                                ? $"Ready: {TargetFolderInfo.FormatBytes(folder.SizeBytes)} ({driverStoreResult.SupersededPackageCount} obsolete/never-installed packages)"
                                : $"Ready: {TargetFolderInfo.FormatBytes(folder.SizeBytes)} ({driverStoreResult.SupersededPackageCount} superseded packages)";
                        });

                        logAction(
                            $"DriverStore Analysis: {driverStoreResult.SupersededPackageCount:N0} reclaimable packages " +
                            $"({driverStoreResult.StagedPackageCount:N0} never installed, {TargetFolderInfo.FormatBytes(driverStoreResult.TotalSupersededBytes)}). " +
                            $"Retained {driverStoreResult.BoundPackageCount:N0} active and {driverStoreResult.InstalledPackageCount:N0} installed packages.",
                            LogLevel.Info);
                    }
                    else
                    {
                        UiInvoke(() => folder.StatusMessage =
                            $"Ready: {TargetFolderInfo.FormatBytes(folder.SizeBytes)} ({driverStoreResult.BoundPackageCount + driverStoreResult.InstalledPackageCount} drivers active)");
                    }
                }
                catch (Exception ex)
                {
                    logAction($"Note during DriverStore inspection: {ex.Message}", LogLevel.Info);
                }

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
        // The card's numbers must be what CLEAN will actually deliver, not what the
        // policy merely does not refuse. The scan therefore runs the SAME safety engine
        // (FileSafetyEngine, with the scanner's attributes so it costs zero extra
        // filesystem round-trips) that CleanupPlanner runs, and reports:
        //   SizeBytes / FileCount      = eligible (deletable) files only
        //   ProtectedBytes             = policy-protected + unknown-with-shield bytes
        //   ReviewRequiredBytes        = review-tier bytes (shielded <24h, installers, ...)
        // so "Clean (12 GB)" can never quietly turn into "freed 3 GB".
        long totalBytes = 0;
        int fileCount = 0;
        long protectedBytes = 0;
        long reviewBytes = 0;
        var topCollector = new WinTempCleaner.Core.Scanning.TopFilesCollector(30);

        // The 24-hour shield applies at clean time as safeMode24Hours && IsSafeModeEligible;
        // the scan must apply exactly the same gate or the two numbers diverge.
        bool applyShield = safeMode24Hours && folder.IsSafeModeEligible;

        // Canonicalize the target roots once - they feed both the scanner and the
        // engine's containment check.
        var canonicalDirs = new List<string>();
        foreach (var dir in directories)
        {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) continue;
            string canonical = PathSecurity.NormalizeCanonicalPath(dir);
            if (!string.IsNullOrEmpty(canonical)) canonicalDirs.Add(canonical);
        }

        foreach (var dir in canonicalDirs)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                NativeFileScanner.ScanDirectory(dir, file =>
                {
                    if (ct.IsCancellationRequested) return;

                    try
                    {
                        var analysis = FileSafetyEngine.Analyze(
                            filePath: file.FullPath,
                            fileName: file.FileName,
                            category: folder.Category,
                            sizeBytes: file.SizeBytes,
                            lastModified: file.LastWriteTimeUtc,
                            allowedRoot: dir,
                            apply24HourThreshold: applyShield,
                            allowedRoots: canonicalDirs,
                            knownAttributes: file.Attributes);

                        bool deletable = analysis.Tier is SafetyRiskTier.Safe or SafetyRiskTier.LowRisk ||
                                         (analysis.Tier == SafetyRiskTier.Unknown && !applyShield);

                        if (deletable)
                        {
                            totalBytes += file.SizeBytes;
                            fileCount++;
                            topCollector.TryAdd(file.FileName, file.FullPath, file.SizeBytes, file.LastWriteTimeUtc.ToLocalTime());
                        }
                        else if (analysis.Tier == SafetyRiskTier.ReviewRequired)
                        {
                            reviewBytes += file.SizeBytes;
                        }
                        else
                        {
                            // Protected tier, and Unknown-with-shield (planner maps it to SkipProtected)
                            protectedBytes += file.SizeBytes;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
                    }
                }, ct, recurse: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        }

        UiInvoke(() => folder.ResolvedDirectoryCount = canonicalDirs.Count);
        UiInvoke(() => folder.SizeBytes = totalBytes);
        UiInvoke(() => folder.FileCount = fileCount);
        UiInvoke(() => folder.TopFiles = topCollector.ToDescendingList(15));
        UiInvoke(() => folder.ProtectedBytes = protectedBytes);
        UiInvoke(() => folder.ReviewRequiredBytes = reviewBytes);
        UiInvoke(() => folder.StatusMessage = $"Ready: {TargetFolderInfo.FormatBytes(totalBytes)}");
        logAction($"Scanned {categoryTitle}: {TargetFolderInfo.FormatBytes(totalBytes)} cleanable of {TargetFolderInfo.FormatBytes(totalBytes + protectedBytes + reviewBytes)} ({fileCount:N0} files)", LogLevel.Info);
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

    /// <summary>
    /// Advertises exactly what DISM reports as reclaimable, using the same analysis Disk
    /// Cleanup runs before it offers "Windows Update Cleanup". Nothing here deletes.
    /// </summary>
    private static async Task ScanWindowsUpdateCleanupAsync(
        TargetFolderInfo folder,
        Action<string, LogLevel> logAction,
        CancellationToken ct)
    {
        try
        {
            var analysis = await DismComponentStoreAnalyzer.AnalyzeAsync(ct).ConfigureAwait(false);
            if (analysis == null || !analysis.HasData)
            {
                UiInvoke(() =>
                {
                    folder.SizeBytes = 0;
                    folder.FileCount = 0;
                    folder.FolderCount = 0;
                    folder.TopFiles = new List<JunkFileItem>();
                    folder.HasError = true;
                    folder.ErrorMessage = "DISM component-store analysis returned no data.";
                    folder.StatusMessage = "Analysis Unavailable";
                });
                logAction("Windows Update Cleanup: DISM component-store analysis returned no data.", LogLevel.Warning);
                return;
            }

            long reclaimable = Math.Max(0, analysis.EstimatedReclaimableBytes);
            int packages = Math.Max(0, analysis.ReclaimablePackageCount);

            UiInvoke(() =>
            {
                folder.SizeBytes = reclaimable;
                folder.FileCount = packages;
                folder.FolderCount = 0;
                folder.TopFiles = new List<JunkFileItem>
                {
                    new()
                    {
                        FileName = "Superseded component-store payloads",
                        FilePath = "WinSxS — DISM /StartComponentCleanup /ResetBase",
                        SizeBytes = reclaimable,
                        LastModified = analysis.LastCleanupUtc?.ToLocalTime() ?? DateTime.Now
                    }
                };
                folder.StatusMessage = reclaimable > 0
                    ? $"Ready: {TargetFolderInfo.FormatBytes(reclaimable)} reclaimable ({packages} packages)"
                    : "Clean (no reclaimable update payloads)";
            });

            logAction(
                $"Windows Update Cleanup analysis: {TargetFolderInfo.FormatBytes(reclaimable)} reclaimable across {packages} packages " +
                $"(store {TargetFolderInfo.FormatBytes(analysis.ActualSizeBytes)}, cleanup recommended: {analysis.CleanupRecommended}).",
                LogLevel.Info);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            UiInvoke(() => folder.StatusMessage = "Scan Error");
            UiInvoke(() => folder.HasError = true);
            UiInvoke(() => folder.ErrorMessage = ex.Message);
            logAction($"Error analysing the Windows component store: {ex.Message}", LogLevel.Warning);
        }
    }

    public async Task<(long freedBytes, int filesDeleted, int foldersDeleted, int filesSkipped, int filesFailed)> CleanFolderAsync(
        TargetFolderInfo folder,
        bool safeMode24Hours,
        Action<string, LogLevel> logAction,
        Action<double> progressReport,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(folder);

        folder.IsCleaning = true;
        folder.StatusMessage = "Cleaning...";

        long freedBytes = 0;
        int filesDeleted = 0;
        int foldersDeleted = 0;
        int filesSkipped = 0;
        // Counted SEPARATELY from filesSkipped. A caller that cannot tell "the policy kept
        // this file" from "we tried and failed" is forced to report a run where every
        // deletion errored as a success with zero bytes reclaimed.
        int filesFailed = 0;

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

            if (folder.Id == "WinUpdateCleanup")
            {
                try
                {
                    var (ok, reclaimed, message) = await RunDismComponentCleanupAsync(logAction, ct, resetBase: true).ConfigureAwait(false);
                    if (ok)
                    {
                        freedBytes = reclaimed;
                        filesDeleted = 1;
                        UiInvoke(() =>
                        {
                            folder.SizeBytes = 0;
                            folder.FileCount = 0;
                            folder.StatusMessage = reclaimed > 0
                                ? $"Reclaimed: {TargetFolderInfo.FormatBytes(reclaimed)}"
                                : "Cleaned (0 B reclaimed)";
                        });
                        logAction(message, LogLevel.Success);
                    }
                    else
                    {
                        UiInvoke(() =>
                        {
                            folder.StatusMessage = "Cleanup Failed";
                            folder.HasError = true;
                            folder.ErrorMessage = message;
                        });
                        filesFailed++;
                        logAction(message, LogLevel.Warning);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    UiInvoke(() => folder.StatusMessage = "Cleanup Failed");
                    UiInvoke(() => folder.HasError = true);
                    UiInvoke(() => folder.ErrorMessage = ex.Message);
                    filesFailed++;
                    logAction($"Error cleaning the Windows component store: {ex.Message}", LogLevel.Warning);
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
                        // Enumerate without descending into reparse points. Two reasons, both
                        // real on a developer profile: a junction to a shared store (installed
                        // skills, plugins) is counted once per link, inflating the reported
                        // reclaimable size by the same amount repeatedly; and traversal would
                        // enter linked trees that this folder does not actually own.
                        var residualEnum = new EnumerationOptions
                        {
                            IgnoreInaccessible = true,
                            RecurseSubdirectories = true,
                            AttributesToSkip = FileAttributes.ReparsePoint
                        };

                        if (folder.SizeBytes == 0)
                        {
                            try
                            {
                                var di = new DirectoryInfo(folder.FolderPath);
                                var files = di.EnumerateFiles("*", residualEnum).ToList();
                                folder.SizeBytes = files.Sum(f => f.Length);
                                folder.FileCount = files.Count;
                            }
                            catch { }
                        }
                        long initialSize = folder.SizeBytes;
                        int initialFiles = folder.FileCount;

                        // Respect the user's Send-to-Recycle-Bin choice. This path previously
                        // called LargeFileHunterService.MoveToRecycleBin UNCONDITIONALLY, so
                        // orphaned-app residuals landed in the Recycle Bin even with the
                        // toggle off - the exact "I disabled it but files still go to the
                        // bin" report. Recycle only when the setting says so.
                        bool recycleResidual = SettingsService.Current.SendToRecycleBin;

                        bool TryRemoveResidual() => recycleResidual
                            ? LargeFileHunterService.MoveToRecycleBin(folder.FolderPath)
                            : DeleteOrphanResidualPermanently(folder.FolderPath, logAction);

                        bool ok = TryRemoveResidual();
                        if (!ok)
                        {
                            // Proactively inspect file lockers and terminate non-system helper processes
                            try
                            {
                                var di = new DirectoryInfo(folder.FolderPath);
                                foreach (var file in di.EnumerateFiles("*", residualEnum))
                                {
                                    var lockers = WinTempCleaner.Core.Safety.RestartManagerService.GetLockingProcesses(file.FullName);
                                    foreach (var locker in lockers)
                                    {
                                        if (!WinTempCleaner.Core.Safety.RestartManagerService.IsProtectedSystemProcess(locker.ProcessName))
                                        {
                                            try
                                            {
                                                using var proc = System.Diagnostics.Process.GetProcessById(locker.ProcessId);
                                                proc.Kill(entireProcessTree: true);
                                                proc.WaitForExit(1000);
                                            }
                                            catch { }
                                        }
                                    }
                                }
                                ok = TryRemoveResidual();
                            }
                            catch { }
                        }

                        if (ok)
                        {
                            freedBytes = initialSize;
                            filesDeleted = initialFiles;
                            foldersDeleted = 1;
                            UiInvoke(() => folder.SizeBytes = 0);
                            UiInvoke(() => folder.FileCount = 0);
                            UiInvoke(() => folder.StatusMessage = recycleResidual ? "Moved to Recycle Bin (Undoable)" : "Deleted");
                            logAction(recycleResidual
                                ? $"Safely recycled residual folder '{folder.FolderPath}' to Windows Recycle Bin ({TargetFolderInfo.FormatBytes(initialSize)})"
                                : $"Permanently deleted residual folder '{folder.FolderPath}' ({TargetFolderInfo.FormatBytes(initialSize)})",
                                LogLevel.Success);
                        }
                        else
                        {
                            UiInvoke(() => folder.StatusMessage = "In Use or Locked");
                            logAction($"Could not {(recycleResidual ? "recycle" : "delete")} '{folder.FolderPath}'. File may be in use or require admin rights.", LogLevel.Warning);
                            filesFailed++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    UiInvoke(() => folder.StatusMessage = "Error");
                    UiInvoke(() => folder.HasError = true);
                    UiInvoke(() => folder.ErrorMessage = ex.Message);
                    logAction($"Error cleaning residual folder '{folder.FolderPath}': {ex.Message}", LogLevel.Error);
                    filesFailed++;
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

            // Phase timing. A cleanup that takes minutes must be able to say WHERE the
            // minutes went - without this, a slow run is indistinguishable from a
            // slow disk. One line per scope, in the app's own log, so the next slow
            // run names its own culprit instead of guessing.
            var phaseWatch = System.Diagnostics.Stopwatch.StartNew();
            double planMs = 0;
            double executeMs = 0;
            double purgeMs = 0;

            var plan = CleanupPlanner.CreatePlan(
                scopeId: folder.Id,
                scopeName: folder.Name,
                directories: directoriesToClean,
                category: folder.Category,
                apply24HourShield: applyShield,
                sendToRecycleBin: sendToRecycle,
                ct: ct);
            planMs = phaseWatch.Elapsed.TotalMilliseconds;

            // Explain a zero-byte result instead of leaving it as a silent dead end.
            // The scan total includes every file, but the safety engine withholds
            // Protected and ReviewRequired bytes from deletion; publish that split
            // so the card can explain itself.
            var protectedBytes = plan.Actions
                .Where(a => a.Action == IntendedCleanupAction.SkipProtected)
                .Sum(a => a.SizeBytes);
            var reviewRequiredBytes = plan.Actions
                .Where(a => a.Action == IntendedCleanupAction.SkipReviewRequired)
                .Sum(a => a.SizeBytes);
            UiInvoke(() => folder.ProtectedBytes = protectedBytes);
            UiInvoke(() => folder.ReviewRequiredBytes = reviewRequiredBytes);

            var txResult = await CleanupExecutor.ExecutePlanAsync(
                plan,
                directoriesToClean,
                logAction,
                progressReport,
                ct).ConfigureAwait(false);
            executeMs = phaseWatch.Elapsed.TotalMilliseconds - planMs;

            freedBytes = txResult.TotalFreedBytes;
            filesDeleted = txResult.TotalItemsFreed;
            // FailedCount is a real failure signal, not a skip. Counting it inside
            // filesSkipped (as this line previously did) is what let a run where every
            // deletion errored still report "files protected: N" and exit successfully.
            filesFailed = txResult.FailedCount;
            filesSkipped = txResult.SkippedCount + txResult.ReviewRequiredCount + txResult.UnknownCount;

            // Clean empty subdirectories safely (Targeted bottom-up pruning from affected directories)
            double purgeStart = phaseWatch.Elapsed.TotalMilliseconds;
            var candidateDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool aggressivePrune = SettingsService.Current.AggressiveEmptyFolderPrune;

            if (txResult.AffectedParentDirectories.Count > 0 && !aggressivePrune)
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
                // Aggressive mode or fallback: inspect entire target directory trees for empty folders
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

            // Empty-folder purge. This spent FOUR filesystem round-trips per candidate
            // folder: Directory.Exists, then ProtectionPolicy.IsProtected (which stats
            // the path again to rule out a reparse point), then
            // PathSecurity.IsReparsePointOrLink (a third stat), then a fresh
            // DirectoryInfo for its Attributes (a fourth). On a machine where one
            // attribute read costs ~50us - which is what Defender's on-access filter
            // does to a real system volume - 3,123 folders meant 3,123 x 4 stats, all
            // of them serial, before a single RemoveDirectoryW.
            //
            // Now ONE DirectoryInfo per folder: its attributes answer the existence
            // question AND the reparse question, and the policy is handed those
            // attributes instead of re-reading them.
            //
            // The loop stays SEQUENTIAL and deepest-first (longer path first, so a
            // child is removed before its parent is tried). Running it in parallel
            // would break that order: a parent tried before its child still exists is
            // not empty, so it would be skipped and never revisited - and the folder
            // count the user sees would silently drop.
            foreach (var dirPath in candidateDirs.OrderByDescending(p => p.Length))
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var dirInfo = new DirectoryInfo(dirPath);
                    if (!dirInfo.Exists) continue;

                    FileAttributes attributes = dirInfo.Attributes;
                    if ((attributes & FileAttributes.ReparsePoint) != 0) continue;

                    // The policy can be told the attributes we already read instead of
                    // stat-ing the path a second time to rule out a link.
                    if (ProtectionPolicy.IsProtected(dirPath, out _, attributes)) continue;

                    if (dirInfo.EnumerateFileSystemInfos("*", subCheckOptions).Any())
                    {
                        continue;
                    }

                    if ((attributes & FileAttributes.ReadOnly) != 0)
                    {
                        try { dirInfo.Attributes = FileAttributes.Normal; }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Deltempo.Cleaner] Normalizing attributes failed: {ex.Message}");
                        }
                    }

                    if (RemoveDirectoryW(dirInfo.FullName) || !dirInfo.Exists)
                    {
                        foldersDeleted++;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
                }
            }

            purgeMs = phaseWatch.Elapsed.TotalMilliseconds - purgeStart - executeMs;

            // One line per scope saying where the time went. A cleanup that takes
            // minutes must be able to name its own slow phase - plan, delete or
            // folder purge - instead of leaving the user with a total and a guess.
            System.Diagnostics.Trace.WriteLine(
                $"[Deltempo] Clean phase timing for '{folder.Id}': " +
                $"plan {planMs:N0}ms, delete {executeMs:N0}ms, folder purge {purgeMs:N0}ms, " +
                $"{plan.Actions.Count:N0} planned, {foldersDeleted:N0} folders purged.");

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

            // Execute Windows Plug-and-Play Driver Maintenance if Device Driver Packages is cleaned
            if (folder.Id == "DeviceDriverPackages" && ElevationService.IsRunAsAdmin())
            {
                try
                {
                    var driverResult = await WindowsDriverMaintenanceService.RunPnpDriverCleanAsync(logAction, ct).ConfigureAwait(false);
                    if (driverResult.BytesFreed > 0)
                    {
                        freedBytes += driverResult.BytesFreed;
                    }
                    if (driverResult.PackagesPurged > 0)
                    {
                        filesDeleted += driverResult.PackagesPurged;
                    }
                }
                catch (Exception ex)
                {
                    logAction?.Invoke($"Note during PnP driver maintenance: {ex.Message}", LogLevel.Warning);
                }
            }


            folder.LastTransactionResult = txResult;

            UiInvoke(() => folder.SizeBytes = Math.Max(0, folder.SizeBytes - freedBytes));
            UiInvoke(() => folder.FileCount = Math.Max(0, folder.FileCount - filesDeleted));

            if (freedBytes > 0)
            {
                UiInvoke(() => folder.StatusMessage = $"Reclaimed: {TargetFolderInfo.FormatBytes(freedBytes)}");
                logAction?.Invoke($"Cleaned {folder.Name}: {TargetFolderInfo.FormatBytes(freedBytes)} reclaimed ({filesDeleted:N0} deleted, {txResult.InUseLockedCount:N0} in use, {txResult.RecentShieldedCount:N0} shielded <24h, {txResult.PolicyProtectedCount:N0} protected)", LogLevel.Success);
            }
            else if (filesSkipped > 0)
            {
                if (txResult.InUseLockedCount > 0 && txResult.RecentShieldedCount == 0 && txResult.PolicyProtectedCount == 0)
                {
                    UiInvoke(() => folder.StatusMessage = $"In Use ({txResult.InUseLockedCount:N0} locked)");
                    logAction?.Invoke($"Notice {folder.Name}: {txResult.InUseLockedCount:N0} files currently held open by active running applications", LogLevel.Info);
                }
                else if (txResult.RecentShieldedCount > 0)
                {
                    UiInvoke(() => folder.StatusMessage = $"Shielded ({txResult.RecentShieldedCount:N0} <24h)");
                    logAction?.Invoke($"Protected {folder.Name}: {txResult.RecentShieldedCount:N0} files preserved by 24h safety shield", LogLevel.Info);
                }
                else
                {
                    UiInvoke(() => folder.StatusMessage = $"Protected ({filesSkipped:N0} items)");
                    logAction?.Invoke($"Protected {folder.Name}: {filesSkipped:N0} files preserved by safety policy or verification", LogLevel.Info);
                }
            }
            else
            {
                UiInvoke(() => folder.StatusMessage = "Already Clean (0 B)");
                logAction?.Invoke($"Checked {folder.Name}: Already clean (0 bytes)", LogLevel.Info);
            }

            UiInvoke(() => folder.IsCleaning = false);
        }, ct);

        return (freedBytes, filesDeleted, foldersDeleted, filesSkipped, filesFailed);
    }

    /// <summary>Upper bound on rulepack-supplied cleanup roots, so a malformed or hostile
    /// rulepack cannot inflate the allowed-root set without limit.</summary>
    private const int MaxOverrideRootCount = 512;

    /// <summary>
    /// Filters a user-rulepack-supplied root list before it becomes the allowedRoots of the
    /// planner and the executor.
    /// </summary>
    /// <remarks>
    /// These roots arrive from %APPDATA%\Deltempo\rules\*.json and were previously returned
    /// verbatim. Per-file ProtectionPolicy still ran downstream, but the ROOT itself was
    /// never checked - so a rulepack could nominate C:\Windows or the MSI source caches as a
    /// cleanup root and rely on nothing but the safety engine to notice. Roots are now
    /// validated once, here, and an unsafe root is dropped rather than merely made harder to
    /// reach. ForceDeleteGate - the strictest policy in the codebase - is applied because
    /// this is the only place a rulepack root passes through it.
    /// </remarks>
    private static List<string> ValidateOverrideRoots(List<string> roots)
    {
        var accepted = new List<string>(Math.Min(roots.Count, MaxOverrideRootCount));

        foreach (string root in roots)
        {
            if (accepted.Count >= MaxOverrideRootCount)
            {
                System.Diagnostics.Trace.WriteLine(
                    $"[Deltempo] Rulepack root list truncated at {MaxOverrideRootCount} entries.");
                break;
            }

            if (string.IsNullOrWhiteSpace(root)) continue;

            string canonical = WinTempCleaner.Core.Safety.PathSecurity.NormalizeCanonicalPath(root);
            if (string.IsNullOrEmpty(canonical))
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Rulepack root rejected (not canonicalizable): '{root}'");
                continue;
            }

            if (WinTempCleaner.Core.Safety.PathSecurity.HasPathTraversalSequences(root))
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Rulepack root rejected (traversal): '{root}'");
                continue;
            }

            if (WinTempCleaner.Core.Safety.ProtectionPolicy.IsProtected(canonical, out string protectedReason))
            {
                System.Diagnostics.Trace.WriteLine(
                    $"[Deltempo] Rulepack root rejected (protected): '{canonical}' - {protectedReason}");
                continue;
            }

            if (WinTempCleaner.Core.Safety.ForceDeleteGate.Evaluate(canonical).Tier == WinTempCleaner.Core.Safety.ForceDeleteTier.AbsoluteBlock)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Rulepack root rejected (absolute block): '{canonical}'");
                continue;
            }

            accepted.Add(canonical);
        }

        return accepted.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> ResolveDirectoriesForFolder(TargetFolderInfo folder)
    {
        if (folder.ResolvedDirectoriesOverride != null && folder.ResolvedDirectoriesOverride.Count > 0)
        {
            return ValidateOverrideRoots(folder.ResolvedDirectoriesOverride);
        }

        var dirs = new List<string>();

        if (folder.Id == "WinUpgradeLeftovers") dirs.AddRange(GetUpgradeLeftoverDirectories());
        else if (folder.Id == "WinUpdateCleanup") { /* DISM component-store scope: no filesystem root to enumerate. */ }
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

        // Prune nested duplicates: resolver tables frequently contribute both a parent and
        // one of its own subdirectories (e.g. Cache + Cache\Data, or a logs root plus its
        // per-app subfolders). Recursion already covers the child, so keeping both counted
        // every file twice - inflating the scan total AND planning duplicate deletion
        // actions that the executor then silently re-skips.
        return PruneNestedDirectories(dirs);
    }

    /// <summary>
    /// Removes any directory that is contained inside another directory of the same list.
    /// Comparison is ordinal and case-insensitive, matching Windows path semantics; exact
    /// duplicates collapse too. Runs on already-resolved absolute paths, so no
    /// canonicalization is required here (callers canonicalize roots downstream).
    /// </summary>
    private static List<string> PruneNestedDirectories(List<string> dirs)
    {
        if (dirs.Count <= 1) return dirs;

        var unique = new List<string>(dirs.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in dirs)
        {
            if (!string.IsNullOrWhiteSpace(dir) && seen.Add(dir))
            {
                unique.Add(dir);
            }
        }

        if (unique.Count <= 1) return unique;

        var nested = new List<bool>(unique.Count);
        for (int i = 0; i < unique.Count; i++)
        {
            bool contained = false;
            for (int j = 0; j < unique.Count && !contained; j++)
            {
                if (i != j && PathSecurity.IsSubpathOf(unique[i], unique[j]))
                {
                    contained = true;
                }
            }
            nested.Add(contained);
        }

        var pruned = new List<string>(unique.Count);
        for (int i = 0; i < unique.Count; i++)
        {
            if (!nested[i]) pruned.Add(unique[i]);
        }

        if (pruned.Count != dirs.Count)
        {
            System.Diagnostics.Trace.WriteLine(
                $"[Deltempo] Pruned {dirs.Count - pruned.Count} nested/duplicate director" +
                (dirs.Count - pruned.Count == 1 ? "y" : "ies") + " from scope.");
        }

        return pruned;
    }

    /// <summary>
    /// Public wrapper for dry-run planning. Returns resolved directories for a target folder.
    /// </summary>
    public static List<string> ResolveDirectoriesForFolderPublic(TargetFolderInfo folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        return ResolveDirectoriesForFolder(folder);
    }

    /// <summary>
    /// Permanent removal of a vetted orphaned-app residual folder, used when the user has
    /// Send-to-Recycle-Bin turned OFF. The recycle path moves the folder into the Recycle
    /// Bin without following links; the permanent path must provide the same guarantee,
    /// so this mirrors the recursive-delete guards RootLeftoverPurgeService applies
    /// (protected-descendant refusal, attribute stripping) and verifies the outcome.
    /// internal rather than private so Deltempo.Tests (InternalsVisibleTo) can pin the
    /// toggle-respecting behaviour with real filesystem cases.
    /// </summary>
    internal static bool DeleteOrphanResidualPermanently(string folderPath, Action<string, LogLevel> logAction)
    {
        try
        {
            if (!Directory.Exists(folderPath)) return false;

            // Never delete a link itself as if it were a folder - same rationale as the
            // recycle path's reparse refusal.
            if (WinTempCleaner.Core.Safety.PathSecurity.IsReparsePointOrLink(folderPath))
            {
                logAction($"Safety shield kept '{folderPath}': it is a junction or symlink, not a real folder.", LogLevel.Warning);
                return false;
            }

            // TOCTOU revalidation against the protection policy (canonicalization, reparse,
            // attribute and denylist checks). Unrestricted roots: the orphan resolver
            // already vetted this folder against the AppModel repository before proposing it.
            if (!WinTempCleaner.Core.Cleaning.CleanupExecutor.RevalidateBeforeDeletion(
                    folderPath, allowedRoots: null, expectedSize: null, expectedTimestamp: null,
                    out _, out string failureReason, out _))
            {
                logAction($"Safety shield kept '{folderPath}': {failureReason}", LogLevel.Warning);
                return false;
            }

            // Directory.Delete removes a whole subtree in one syscall, so the per-file
            // policy check never runs for descendants. Refuse when anything beneath is
            // durable AppData application state (OneDrive sync DB, Office licences,
            // credential vaults, key stores).
            if (ProtectionPolicy.TryGetProtectedDescendant(folderPath, out string protectedSubtree))
            {
                logAction(
                    $"Safety shield kept '{folderPath}': it contains protected application data ('{protectedSubtree}'). " +
                    "Remove or move that data manually, then retry.",
                    LogLevel.Warning);
                return false;
            }

            // Strip ReadOnly/Hidden/System so Directory.Delete is not refused by an
            // attribute alone. Walk the tree WITHOUT descending into links - links are
            // detached below, not traversed.
            DetachLinksAndStripAttributes(folderPath);

            // Directory.Delete(recursive: true) handles links differently per runtime:
            // on .NET 11 it removes a nested junction itself, on .NET 10 it throws
            // UnauthorizedAccessException on it, and on .NET Framework it FOLLOWED the
            // link. Neither behaviour is acceptable here, so every nested link has
            // already been detached explicitly above and the recursive delete now runs
            // on a plain, link-free tree. Verified by the junction-survival test.
            Directory.Delete(folderPath, recursive: true);
            return !Directory.Exists(folderPath);
        }
        catch (Exception ex)
        {
            logAction($"Could not permanently delete '{folderPath}': {ex.Message}", LogLevel.Warning);
            System.Diagnostics.Trace.WriteLine(
                $"[Deltempo] DeleteOrphanResidualPermanently failed for '{folderPath}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Walks <paramref name="dir"/> and every real subdirectory without descending into
    /// links. Junctions and symlinks are deleted AS LINKS - Directory.Delete(recursive:
    /// false) / File.Delete remove the link itself and never touch the target, verified
    /// by probe on .NET 10 and 11. Real files and directories get ReadOnly/Hidden/System
    /// stripped so the caller's recursive delete is not refused by an attribute alone.
    /// Individual failures are swallowed: the caller's final Directory.Delete still
    /// fails visibly on leftovers.
    /// </summary>
    private static void DetachLinksAndStripAttributes(string dir)
    {
        // Strip a bad attribute on this real directory itself (never on a link - the
        // ReparsePoint check at the call site above already routed links elsewhere).
        try
        {
            var dirAttrs = File.GetAttributes(dir);
            if ((dirAttrs & FileAttributes.ReparsePoint) == 0 &&
                (dirAttrs & (FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System)) != 0)
            {
                File.SetAttributes(dir, FileAttributes.Directory);
            }
        }
        catch { }

        foreach (string child in Directory.EnumerateFileSystemEntries(dir))
        {
            FileAttributes attrs;
            try
            {
                attrs = File.GetAttributes(child);
            }
            catch
            {
                continue; // unreadable entry: the caller's Directory.Delete reports it
            }

            if ((attrs & FileAttributes.ReparsePoint) != 0)
            {
                // Never traverse a link: destroy the link, never the data behind it.
                try
                {
                    if ((attrs & FileAttributes.Directory) != 0)
                        Directory.Delete(child, recursive: false);
                    else
                        File.Delete(child);
                }
                catch { }
            }
            else
            {
                if ((attrs & FileAttributes.Directory) != 0)
                {
                    DetachLinksAndStripAttributes(child);
                }
                else if ((attrs & (FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System)) != 0)
                {
                    try { File.SetAttributes(child, FileAttributes.Normal); } catch { }
                }
            }
        }
    }


    public static readonly string[] CommunicationAppKeywords =
    {
        "whatsapp", "telegram", "msteams", "teams", "discord", "slack", "signal",
        "skype", "zoom", "viber", "element", "wechat", "line", "kakao", "messenger",
        "session", "threema", "wire", "icq", "mattermost", "webex", "cisco-spark", "ciscospark",
        "ringcentral", "thunderbird", "outlook", "rocketchat", "keybase", "zulip", "chime", "flock",
        "matrix", "accountscontrol", "aad", "cloudexperiencehost", "bioenrollment", "auth"
    };

    // NOTE: two thin wrappers used to live here - IsProtectedSessionOrCredentialFile and
    // IsProtectedFile. Each was a one-line passthrough to ProtectionPolicy.IsProtected
    // and neither had a product caller, so the rule was only ever exercised through a
    // dead API. The policy itself is enforced at nine call sites (CleanupExecutor,
    // FileSafetyEngine, ForceDeleteGate, RestartManagerService, DuplicateFileService,
    // StoreAppOrphanResolver and CleanerService), and the test now pins THAT directly.

    public static string GenerateAuditReport(IEnumerable<TargetFolderInfo> targets, CleanSummary summary, bool safeMode)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(summary);

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
