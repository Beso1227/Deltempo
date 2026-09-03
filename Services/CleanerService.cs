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

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, ref long lpLuid);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges, ref TokenPrivileges NewState, int BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct TokenPrivileges
    {
        public int Count;
        public long Luid;
        public int Attr;
    }

    private const int PrivilegeAttributeEnabled = 2;
    private const int TokenAdjustPrivileges = 0x0020;
    private const int TokenQuery = 0x0008;

    private const string SeBackupName = "SeBackupPrivilege";
    private const string SeRestoreName = "SeRestorePrivilege";
    private const string SeTakeOwnershipName = "SeTakeOwnershipPrivilege";
    private const string SeSecurityName = "SeSecurityPrivilege";

    public static void EnableFileManagementPrivileges()
    {
        SetIncreasePrivilege(SeBackupName);
        SetIncreasePrivilege(SeRestoreName);
        SetIncreasePrivilege(SeTakeOwnershipName);
        SetIncreasePrivilege(SeSecurityName);
    }

    private static bool SetIncreasePrivilege(string privilegeName)
    {
        try
        {
            if (OpenProcessToken(System.Diagnostics.Process.GetCurrentProcess().Handle, TokenAdjustPrivileges | TokenQuery, out IntPtr tokenHandle))
            {
                try
                {
                    var tp = new TokenPrivileges { Count = 1, Attr = PrivilegeAttributeEnabled };
                    if (LookupPrivilegeValue(null, privilegeName, ref tp.Luid))
                    {
                        return AdjustTokenPrivileges(tokenHandle, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
                    }
                }
                finally
                {
                    CloseHandle(tokenHandle);
                }
            }
        }
        catch { }
        return false;
    }

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
                IsSelected = isAdmin
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
                IsSelected = isAdmin
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
                IsSelected = isAdmin
            },

            // 5. Windows Upgrade & Setup Leftovers (PC Manager Deep Match)
            new TargetFolderInfo
            {
                Id = "WinUpgradeLeftovers",
                Name = "Windows Upgrade & Setup Leftovers",
                Category = "System & OS",
                CategoryColor = "#F43F5E",
                SafetyBadge = "🟢 100% Safe Leftovers",
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
                SafetyBadge = "🟢 100% Safe Cache",
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
                SafetyBadge = "🟢 100% Safe Cache",
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
                SafetyBadge = "🟢 100% Safe Drivers",
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
                SafetyBadge = "🟢 100% Safe Logs",
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
                SafetyBadge = "🟢 100% Safe Logs",
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
                SafetyBadge = "🟢 100% Safe Dumps",
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
                SafetyBadge = "🟢 100% Safe Cache",
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
                SafetyBadge = "🟢 100% Safe Cache",
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
                SafetyBadge = "🟢 100% Safe Cache",
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
                SafetyBadge = "🟢 100% Safe Cache",
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
                SafetyBadge = "🟢 100% Safe Cache",
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
                SafetyBadge = "🟢 100% Safe Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Temporary LocalCache & INetCache across Windows Store packages (New Teams, Xbox, WhatsApp, etc.)",
                FolderPath = "Windows Store App Packages Pool",
                IconGlyph = "\uE719",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            },

            // 18. Web Browser Caches (Multi-Profile Engine)
            new TargetFolderInfo
            {
                Id = "BrowserCaches",
                Name = "Web Browsers Cache Pool",
                Category = "User Cache",
                CategoryColor = "#F97316",
                SafetyBadge = "🟢 100% Safe Cache",
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
                SafetyBadge = "🟢 100% Safe Cache",
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
                SafetyBadge = "🟢 100% Safe Cache",
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
                SafetyBadge = "🟢 100% Safe Logs",
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
                SafetyBadge = "🟢 100% Safe Cache",
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
                SafetyBadge = "🟢 100% Safe Privacy",
                SafetyBadgeColor = "#10B981",
                Description = "Recent items shortcuts, AutomaticDestinations, and CustomDestinations Jump Lists",
                FolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Recent"),
                IconGlyph = "\uE81C",
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            },

            // 24. Windows Recycle Bin
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

        // 25. Orphaned Uninstalled AppData Leftovers
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
                folder.IsScanning = false;
                return;
            }

            if (folder.Id == "WinDeliveryOpt")
            {
                var dirs = GetDeliveryOptimizationDirectories();
                ScanDirectoryList(folder, dirs, "Windows Delivery Optimization", logAction, ct);
                folder.IsScanning = false;
                return;
            }

            if (folder.Id == "WinUpgradeLeftovers")
            {
                ScanUpgradeLeftovers(folder, logAction, ct);
                folder.IsScanning = false;
                return;
            }

            if (folder.Id == "WinComponentCaches")
            {
                ScanComponentCaches(folder, logAction, ct);
                folder.IsScanning = false;
                return;
            }

            if (folder.Id == "WinStoreAppCaches")
            {
                ScanStoreAppCaches(folder, logAction, ct);
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

                bool applySafeTimeCheck = safeMode24Hours && (folder.Id is "UserTemp" or "WinTemp" or "SandboxTest");
                var cutoffTime = DateTime.Now - TimeSpan.FromHours(24);

                foreach (var file in dirInfo.EnumerateFiles("*", enumOptions))
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        if (applySafeTimeCheck && file.LastWriteTime > cutoffTime) continue;
                        if (IsProtectedFile(file.FullName)) continue;

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

    #region Directory Resolvers (Single Source of Truth for Scan & Clean)

    public static List<string> GetUpgradeLeftoverDirectories()
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var rootDrive = Path.GetPathRoot(winDir) ?? @"C:\";
        var dirs = new List<string>
        {
            Path.Combine(rootDrive, "$WINDOWS.~BT"),
            Path.Combine(rootDrive, "$WINDOWS.~WS"),
            Path.Combine(rootDrive, "$WinREAgent", "Scratch"),
            Path.Combine(rootDrive, "ESD"),
            Path.Combine(rootDrive, "ESD", "Download"),
            Path.Combine(rootDrive, "Windows.old"),
            Path.Combine(rootDrive, "$SysReset"),
            Path.Combine(rootDrive, "$GetCurrent"),
            Path.Combine(winDir, "Panther"),
            Path.Combine(winDir, "Logs", "MoSetup"),
            Path.Combine(winDir, "System32", "Sysprep", "Panther")
        };
        return dirs;
    }

    public static List<string> GetComponentCacheDirectories()
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var dirs = new List<string>
        {
            Path.Combine(winDir, "ServiceProfiles", "LocalService", "AppData", "Local", "FontCache"),
            Path.Combine(winDir, "ServiceProfiles", "NetworkService", "AppData", "Local", "FontCache"),
            Path.Combine(winDir, "SystemTemp"),
            Path.Combine(winDir, "Downloaded Program Files"),
            Path.Combine(winDir, "SoftwareDistribution", "ScanFile"),
            Path.Combine(winDir, "SoftwareDistribution", "DataStore", "Logs"),
            Path.Combine(winDir, "SoftwareDistribution", "PostRebootEventCache"),
            Path.Combine(winDir, "SoftwareDistribution", "AuthCabs"),
            Path.Combine(winDir, "Installer", "$PatchCache$"),
            Path.Combine(progData, "Package Cache"),
            Path.Combine(winDir, "ServiceProfiles", "NetworkService", "AppData", "Local", "PeerDistPub"),
            Path.Combine(winDir, "ServiceProfiles", "NetworkService", "AppData", "Local", "PeerDistSub"),
            Path.Combine(localAppData, "FontCache")
        };
        return dirs;
    }

    public static List<string> GetStoreAppCacheDirectories()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var packagesRoot = Path.Combine(localAppData, "Packages");
        var dirs = new List<string>();

        if (Directory.Exists(packagesRoot))
        {
            try
            {
                foreach (var pkg in Directory.EnumerateDirectories(packagesRoot))
                {
                    dirs.Add(Path.Combine(pkg, "LocalCache"));
                    dirs.Add(Path.Combine(pkg, "LocalState", "Cache"));
                    dirs.Add(Path.Combine(pkg, "AC", "INetCache"));
                    dirs.Add(Path.Combine(pkg, "AC", "Temp"));
                    dirs.Add(Path.Combine(pkg, "TempState"));
                    dirs.Add(Path.Combine(pkg, "CrashDump"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        }

        return dirs;
    }

    public static List<string> GetDeviceDriverDirectories()
    {
        var progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var rootDrive = Path.GetPathRoot(winDir) ?? @"C:\";

        return new List<string>
        {
            Path.Combine(progData, "NVIDIA Corporation", "NVIDIA App", "UpdateFramework", "ota-artifacts"),
            Path.Combine(progData, "NVIDIA Corporation", "Downloader"),
            Path.Combine(progData, "NVIDIA", "Updates"),
            Path.Combine(progData, "NVIDIA Corporation", "NetService"),
            Path.Combine(progData, "AMD"),
            Path.Combine(progData, "Intel"),
            Path.Combine(rootDrive, "NVIDIA", "DisplayDriver"),
            Path.Combine(rootDrive, "AMD", "Packages"),
            Path.Combine(rootDrive, "Intel", "Logs"),
            Path.Combine(localAppData, "AMD", "DxCache"),
            Path.Combine(localAppData, "AMD", "DVR"),
            Path.Combine(winDir, "System32", "DriverStore", "Temp"),
            Path.Combine(winDir, "System32", "DriverState")
        };
    }

    public static List<string> GetDefenderDirectories()
    {
        var progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return new List<string>
        {
            Path.Combine(progData, "Microsoft", "Windows Defender", "Support"),
            Path.Combine(progData, "Microsoft", "Windows Defender", "Definition Updates", "Backup"),
            Path.Combine(progData, "Microsoft", "Windows Defender", "Scans", "History", "Results", "Quick"),
            Path.Combine(progData, "Microsoft", "Windows Defender", "Scans", "History", "Results", "Resource"),
            Path.Combine(progData, "Microsoft", "Windows Defender", "Scans", "History", "Store")
        };
    }

    public static List<string> GetWinSystemLogDirectories()
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return new List<string>
        {
            Path.Combine(winDir, "Logs"),
            Path.Combine(winDir, "Logs", "CBS"),
            Path.Combine(winDir, "Logs", "DISM"),
            Path.Combine(winDir, "Logs", "NetSetup"),
            Path.Combine(winDir, "Logs", "WindowsUpdate"),
            Path.Combine(winDir, "Logs", "MoSetup"),
            Path.Combine(winDir, "Panther"),
            Path.Combine(winDir, "Debug"),
            Path.Combine(winDir, "System32", "LogFiles"),
            Path.Combine(winDir, "tracing"),
            Path.Combine(progData, "Microsoft", "Diagnosis", "ETLLogs"),
            Path.Combine(progData, "Microsoft", "Diagnosis", "SoftLanding")
        };
    }

    public static List<string> GetSystemDumpDirectories()
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return new List<string>
        {
            Path.Combine(winDir, "Minidump"),
            Path.Combine(winDir, "LiveKernelReports"),
            Path.Combine(winDir, "System32", "CrashDump"),
            Path.Combine(localAppData, "CrashDumps"),
            Path.Combine(progData, "Microsoft", "Windows", "WER", "ReportArchive"),
            Path.Combine(progData, "Microsoft", "Windows", "WER", "ReportQueue"),
            Path.Combine(progData, "Microsoft", "Windows", "WER", "Temp"),
            Path.Combine(localAppData, "Microsoft", "Windows", "WER", "ReportArchive"),
            Path.Combine(localAppData, "Microsoft", "Windows", "WER", "ReportQueue"),
            Path.Combine(localAppData, "Microsoft", "Windows", "WER", "Temp")
        };
    }

    public static List<string> GetTemporaryInternetDirectories()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new List<string>
        {
            Path.Combine(localAppData, "Microsoft", "Windows", "INetCache"),
            Path.Combine(localAppData, "Microsoft", "Windows", "WebCache"),
            Path.Combine(localAppData, "Microsoft", "Windows", "Caches"),
            Path.Combine(userProfile, "AppData", "LocalLow", "Microsoft", "CryptnetUrlCache")
        };
    }

    public static List<string> GetDeliveryOptimizationDirectories()
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new List<string>
        {
            Path.Combine(winDir, "ServiceProfiles", "NetworkService", "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization", "Cache"),
            Path.Combine(winDir, "SoftwareDistribution", "DeliveryOptimization"),
            Path.Combine(localAppData, "Microsoft", "Windows", "DeliveryOptimization")
        };
    }

    public static List<string> GetGpuShaderDirectories()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new List<string>
        {
            Path.Combine(localAppData, "NVIDIA", "DXCache"),
            Path.Combine(localAppData, "NVIDIA", "GLCache"),
            Path.Combine(userProfile, "AppData", "LocalLow", "NVIDIA", "PerDriverVersion", "DXCache"),
            Path.Combine(localAppData, "AMD", "DxCache"),
            Path.Combine(localAppData, "D3DSCache"),
            Path.Combine(localAppData, "Intel", "ShaderCache")
        };
    }

    public static List<string> GetGamingLauncherDirectories()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        return new List<string>
        {
            Path.Combine(progFilesX86, "Steam", "downloading"),
            Path.Combine(progFilesX86, "Steam", "shadercache"),
            Path.Combine(progFilesX86, "Steam", "appcache", "httpcache"),
            Path.Combine(localAppData, "Steam", "htmlcache"),
            Path.Combine(localAppData, "EpicGamesLauncher", "Saved", "webcache"),
            Path.Combine(localAppData, "EpicGamesLauncher", "Saved", "webcache_4430"),
            Path.Combine(localAppData, "EpicGamesLauncher", "Saved", "Logs"),
            Path.Combine(localAppData, "Battle.net", "Cache"),
            Path.Combine(localAppData, "Battle.net", "Logs"),
            Path.Combine(localAppData, "Blizzard Entertainment", "Battle.net", "Cache"),
            Path.Combine(localAppData, "Electronic Arts", "EA Desktop", "Logs"),
            Path.Combine(localAppData, "Electronic Arts", "EA Desktop", "cache"),
            Path.Combine(localAppData, "Origin", "Logs"),
            Path.Combine(localAppData, "Origin", "ThinSetup"),
            Path.Combine(localAppData, "Ubisoft Game Launcher", "cache"),
            Path.Combine(localAppData, "Ubisoft Game Launcher", "spool"),
            Path.Combine(localAppData, "Riot Games", "Riot Client", "Logs"),
            Path.Combine(localAppData, "Riot Games", "Riot Client", "Data", "Caches"),
            Path.Combine(localAppData, "VALORANT", "saved", "crashes"),
            Path.Combine(localAppData, "VALORANT", "saved", "logs"),
            Path.Combine(localAppData, "GOG.com", "Galaxy", "webcache"),
            Path.Combine(localAppData, "GOG.com", "Galaxy", "logs"),
            Path.Combine(localAppData, "Roblox", "logs"),
            Path.Combine(localAppData, "Roblox", "Downloads")
        };
    }

    public static List<string> GetMediaCreatorDirectories()
    {
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var dirs = new List<string>
        {
            Path.Combine(roamingAppData, "Adobe", "Common", "Media Cache Files"),
            Path.Combine(roamingAppData, "Adobe", "Common", "Media Cache"),
            Path.Combine(roamingAppData, "Adobe", "Common", "Peak Files"),
            Path.Combine(localAppData, "Adobe", "Photoshop", "AutoRecover"),
            Path.Combine(roamingAppData, "Blackmagic Design", "DaVinci Resolve", "Support", "logs"),
            Path.Combine(roamingAppData, "Blackmagic Design", "DaVinci Resolve", "Cache"),
            Path.Combine(roamingAppData, "obs-studio", "logs"),
            Path.Combine(roamingAppData, "obs-studio", "crashes"),
            Path.Combine(roamingAppData, "slobs-client", "cache"),
            Path.Combine(roamingAppData, "Blender Foundation", "Blender", "temp"),
            Path.Combine(localAppData, "CapCut", "User Data", "Cache"),
            Path.Combine(localAppData, "audacity", "SessionData")
        };

        // Adobe After Effects disk cache
        var adobeLocal = Path.Combine(localAppData, "Adobe", "After Effects");
        if (Directory.Exists(adobeLocal))
        {
            try
            {
                foreach (var verDir in Directory.EnumerateDirectories(adobeLocal))
                {
                    dirs.Add(Path.Combine(verDir, "Disk Cache"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        }

        return dirs;
    }

    public static List<string> GetMobileDevDirectories()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        return new List<string>
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
    }

    public static List<string> GetAppCacheDirectories()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var dirs = new List<string>
        {
            Path.Combine(localAppData, "Spotify", "Data"),
            Path.Combine(localAppData, "Spotify", "Storage"),
            Path.Combine(localAppData, "Spotify", "Browser", "Cache"),
            Path.Combine(roamingAppData, "discord", "Cache"),
            Path.Combine(roamingAppData, "discord", "Code Cache"),
            Path.Combine(roamingAppData, "discord", "GPUCache"),
            Path.Combine(roamingAppData, "discordcanary", "Cache"),
            Path.Combine(roamingAppData, "discordptb", "Cache"),
            Path.Combine(roamingAppData, "Slack", "Cache"),
            Path.Combine(roamingAppData, "Slack", "GPUCache"),
            Path.Combine(roamingAppData, "Slack", "Service Worker", "CacheStorage"),
            Path.Combine(roamingAppData, "Code", "Cache"),
            Path.Combine(roamingAppData, "Code", "CachedData"),
            Path.Combine(roamingAppData, "Code", "CachedExtensions"),
            Path.Combine(roamingAppData, "Code", "GPUCache"),
            Path.Combine(roamingAppData, "Code", "logs"),
            Path.Combine(roamingAppData, "Cursor", "Cache"),
            Path.Combine(roamingAppData, "Cursor", "CachedData"),
            Path.Combine(roamingAppData, "Cursor", "GPUCache"),
            Path.Combine(roamingAppData, "Windsurf", "Cache"),
            Path.Combine(roamingAppData, "Windsurf", "GPUCache"),
            Path.Combine(roamingAppData, "Notion", "Cache"),
            Path.Combine(roamingAppData, "Notion", "GPUCache"),
            Path.Combine(roamingAppData, "Notion", "Code Cache"),
            Path.Combine(localAppData, "Microsoft", "Teams", "Cache"),
            Path.Combine(roamingAppData, "Microsoft", "Teams", "Cache"),
            Path.Combine(roamingAppData, "Telegram Desktop", "tdata", "user_data", "cache"),
            Path.Combine(roamingAppData, "WhatsApp", "Cache"),
            Path.Combine(roamingAppData, "Zoom", "data"),
            Path.Combine(localAppData, "Zoom", "temp"),
            Path.Combine(localAppData, "CapCut", "User Data", "Cache")
        };

        // JetBrains IDE caches
        var jbRoot = Path.Combine(localAppData, "JetBrains");
        if (Directory.Exists(jbRoot))
        {
            try
            {
                foreach (var ideDir in Directory.EnumerateDirectories(jbRoot))
                {
                    dirs.Add(Path.Combine(ideDir, "caches"));
                    dirs.Add(Path.Combine(ideDir, "log"));
                    dirs.Add(Path.Combine(ideDir, "tmp"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        }

        return dirs;
    }

    public static List<string> GetBrowserCacheDirectories()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dirs = new List<string>();

        // Chromium browser User Data roots
        var chromiumRoots = new (string BaseDir, string FallbackSubDir)[]
        {
            (Path.Combine(localAppData, "Google", "Chrome", "User Data"), "Default"),
            (Path.Combine(localAppData, "Microsoft", "Edge", "User Data"), "Default"),
            (Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data"), "Default"),
            (Path.Combine(localAppData, "Vivaldi", "User Data"), "Default"),
            (Path.Combine(localAppData, "Arc", "User Data"), "Default"),
            (Path.Combine(localAppData, "Yandex", "YandexBrowser", "User Data"), "Default"),
            (Path.Combine(localAppData, "Opera Software", "Opera Stable"), ""),
            (Path.Combine(localAppData, "Opera Software", "Opera GX Stable"), "")
        };

        foreach (var (baseDir, fallbackSub) in chromiumRoots)
        {
            if (!Directory.Exists(baseDir)) continue;

            var profileDirs = new List<string>();

            // Find all profiles (Default, Profile 1, Profile 2, etc.)
            try
            {
                foreach (var subDir in Directory.EnumerateDirectories(baseDir))
                {
                    var dirName = Path.GetFileName(subDir);
                    if (dirName.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
                        dirName.StartsWith("Profile", StringComparison.OrdinalIgnoreCase) ||
                        dirName.Equals("Guest Profile", StringComparison.OrdinalIgnoreCase) ||
                        dirName.Equals("System Profile", StringComparison.OrdinalIgnoreCase))
                    {
                        profileDirs.Add(subDir);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }

            // If no profile subdirectories matched, treat baseDir itself as the container (e.g. Opera)
            if (profileDirs.Count == 0)
            {
                profileDirs.Add(baseDir);
            }

            foreach (var prof in profileDirs)
            {
                dirs.Add(Path.Combine(prof, "Cache"));
                dirs.Add(Path.Combine(prof, "Cache", "Cache_Data"));
                dirs.Add(Path.Combine(prof, "Code Cache"));
                dirs.Add(Path.Combine(prof, "GPUCache"));
                dirs.Add(Path.Combine(prof, "DawnCache"));
                dirs.Add(Path.Combine(prof, "ShaderCache"));
                dirs.Add(Path.Combine(prof, "GrShaderCache"));
                dirs.Add(Path.Combine(prof, "Service Worker", "CacheStorage"));
                dirs.Add(Path.Combine(prof, "Crashpad", "reports"));
                dirs.Add(Path.Combine(prof, "blob_storage"));
            }
        }

        // Gecko / Firefox-based browsers
        var geckoRoots = new string[]
        {
            Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles"),
            Path.Combine(localAppData, "Floorp", "Profiles"),
            Path.Combine(localAppData, "Waterfox", "Profiles"),
            Path.Combine(localAppData, "LibreWolf", "Profiles"),
            Path.Combine(localAppData, "zen", "Profiles")
        };

        foreach (var gRoot in geckoRoots)
        {
            if (!Directory.Exists(gRoot)) continue;
            try
            {
                foreach (var prof in Directory.EnumerateDirectories(gRoot))
                {
                    dirs.Add(Path.Combine(prof, "cache2"));
                    dirs.Add(Path.Combine(prof, "startupCache"));
                    dirs.Add(Path.Combine(prof, "thumbnails"));
                    dirs.Add(Path.Combine(prof, "jumpListCache"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        }

        return dirs;
    }

    public static List<string> GetDevPackageDirectories()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        return new List<string>
        {
            Path.Combine(localAppData, "pip", "cache"),
            Path.Combine(localAppData, "npm-cache"),
            Path.Combine(roamingAppData, "npm-cache"),
            Path.Combine(localAppData, "Yarn", "Cache"),
            Path.Combine(localAppData, "pnpm", "store", "v3"),
            Path.Combine(localAppData, "pnpm-cache"),
            Path.Combine(localAppData, "NuGet", "v3-cache"),
            Path.Combine(localAppData, "NuGet", "plugins-cache"),
            Path.Combine(userProfile, ".cache"),
            Path.Combine(userProfile, ".gradle", "caches"),
            Path.Combine(userProfile, ".cargo", "registry", "cache"),
            Path.Combine(userProfile, ".cargo", "git", "db"),
            Path.Combine(userProfile, ".rustup", "downloads"),
            Path.Combine(userProfile, ".rustup", "tmp"),
            Path.Combine(userProfile, ".bun", "install", "cache"),
            Path.Combine(localAppData, "deno", "deps"),
            Path.Combine(localAppData, "go-build"),
            Path.Combine(localAppData, "Microsoft", "dotnet"),
            Path.Combine(localAppData, "Temp", ".net"),
            Path.Combine(userProfile, ".m2", "repository", ".cache"),
            Path.Combine(userProfile, ".m2", "temp"),
            Path.Combine(userProfile, ".nuget", "packages", "temp")
        };
    }

    #endregion

    private static void ScanUpgradeLeftovers(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var dirs = GetUpgradeLeftoverDirectories();
        ScanDirectoryList(folder, dirs, "Windows Upgrade & Setup Leftovers", logAction, ct);
    }

    private static void ScanComponentCaches(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var dirs = GetComponentCacheDirectories();
        ScanDirectoryList(folder, dirs, "Windows Component & Font Caches", logAction, ct);
    }

    private static void ScanStoreAppCaches(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var dirs = GetStoreAppCacheDirectories();
        ScanDirectoryList(folder, dirs, "Windows Store & UWP App Caches", logAction, ct);
    }

    private static void ScanDeviceDriverPackages(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var dirs = GetDeviceDriverDirectories();
        ScanDirectoryList(folder, dirs, "Device Driver Packages & GPU Updates", logAction, ct);
    }

    private static void ScanDefenderAntivirus(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var dirs = GetDefenderDirectories();
        ScanDirectoryList(folder, dirs, "Microsoft Defender Antivirus Support", logAction, ct);
    }

    private static void ScanWinSystemLogs(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var dirs = GetWinSystemLogDirectories();
        ScanDirectoryList(folder, dirs, "Windows Diagnostic Logs", logAction, ct);
    }

    private static void ScanSystemDumps(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var dirs = GetSystemDumpDirectories();
        ScanDirectoryList(folder, dirs, "BSOD Minidumps & Kernel Reports", logAction, ct);
    }

    private static void ScanTemporaryInternetFiles(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var dirs = GetTemporaryInternetDirectories();
        ScanDirectoryList(folder, dirs, "Temporary Internet Files & WebCache", logAction, ct);
    }

    private static void ScanSystemUsageTraces(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string[] traceDirs = { Path.Combine(roamingAppData, "Microsoft", "Windows", "Recent") };
        ScanDirectoryList(folder, traceDirs, "System & Explorer Usage Traces", logAction, ct);
    }

    private static void ScanGpuShaderPools(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var dirs = GetGpuShaderDirectories();
        ScanDirectoryList(folder, dirs, "GPU Shaders", logAction, ct);
    }

    private static void ScanGamingLauncherPools(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var dirs = GetGamingLauncherDirectories();
        ScanDirectoryList(folder, dirs, "Game Launchers & Shaders", logAction, ct);
    }

    private static void ScanMediaCreatorPools(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var dirs = GetMediaCreatorDirectories();
        ScanDirectoryList(folder, dirs, "Media & Creator Render Caches", logAction, ct);
    }

    private static void ScanMobileDevPools(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var dirs = GetMobileDevDirectories();
        ScanDirectoryList(folder, dirs, "Mobile & Dev Residuals", logAction, ct);
    }

    private static void ScanAppCachePools(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var dirs = GetAppCacheDirectories();
        ScanDirectoryList(folder, dirs, "Desktop Apps Caches", logAction, ct);
    }

    private static void ScanBrowserCachePools(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var dirs = GetBrowserCacheDirectories();
        ScanDirectoryList(folder, dirs, "Web Browsers Cache Pool", logAction, ct);
    }

    private static void ScanDevPackageCaches(TargetFolderInfo folder, Action<string, LogLevel> logAction, CancellationToken ct)
    {
        var dirs = GetDevPackageDirectories();
        ScanDirectoryList(folder, dirs, "Developer Caches", logAction, ct);
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
            EnableFileManagementPrivileges();

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

            if (folder.IsOrphanedAppFolder)
            {
                try
                {
                    if (Directory.Exists(folder.FolderPath))
                    {
                        long initialSize = folder.SizeBytes;
                        int initialFiles = folder.FileCount;
                        bool ok = LargeFileHunterService.MoveToRecycleBin(folder.FolderPath);
                        if (ok)
                        {
                            freedBytes = initialSize;
                            filesDeleted = initialFiles;
                            foldersDeleted = 1;
                            folder.SizeBytes = 0;
                            folder.FileCount = 0;
                            folder.StatusMessage = "Moved to Recycle Bin (Undoable)";
                            logAction($"Safely recycled residual folder '{folder.FolderPath}' to Windows Recycle Bin ({TargetFolderInfo.FormatBytes(initialSize)})", LogLevel.Success);
                        }
                        else
                        {
                            folder.StatusMessage = "In Use or Locked";
                            logAction($"Could not recycle '{folder.FolderPath}'. File may be in use or require admin rights.", LogLevel.Warning);
                        }
                    }
                }
                catch (Exception ex)
                {
                    folder.StatusMessage = "Error";
                    logAction($"Error cleaning residual folder '{folder.FolderPath}': {ex.Message}", LogLevel.Error);
                }
                finally
                {
                    folder.IsCleaning = false;
                }
                return;
            }

            var directoriesToClean = new List<string>();

            if (folder.Id == "WinUpgradeLeftovers")
            {
                directoriesToClean.AddRange(GetUpgradeLeftoverDirectories());
            }
            else if (folder.Id == "WinComponentCaches")
            {
                directoriesToClean.AddRange(GetComponentCacheDirectories());
            }
            else if (folder.Id == "WinStoreAppCaches")
            {
                directoriesToClean.AddRange(GetStoreAppCacheDirectories());
            }
            else if (folder.Id == "DeviceDriverPackages")
            {
                directoriesToClean.AddRange(GetDeviceDriverDirectories());
            }
            else if (folder.Id == "DefenderAntivirus")
            {
                directoriesToClean.AddRange(GetDefenderDirectories());
            }
            else if (folder.Id == "WinSystemLogs")
            {
                directoriesToClean.AddRange(GetWinSystemLogDirectories());
            }
            else if (folder.Id == "SystemDumps")
            {
                directoriesToClean.AddRange(GetSystemDumpDirectories());
            }
            else if (folder.Id == "TemporaryInternetFiles")
            {
                directoriesToClean.AddRange(GetTemporaryInternetDirectories());
            }
            else if (folder.Id == "SystemUsageTraces")
            {
                directoriesToClean.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Recent"));
            }
            else if (folder.Id == "GpuShaderCaches")
            {
                directoriesToClean.AddRange(GetGpuShaderDirectories());
            }
            else if (folder.Id == "GamingLaunchers")
            {
                directoriesToClean.AddRange(GetGamingLauncherDirectories());
            }
            else if (folder.Id == "MediaCreatorCaches")
            {
                directoriesToClean.AddRange(GetMediaCreatorDirectories());
            }
            else if (folder.Id == "MobileDevResiduals")
            {
                directoriesToClean.AddRange(GetMobileDevDirectories());
            }
            else if (folder.Id == "AppCacheSweeper")
            {
                directoriesToClean.AddRange(GetAppCacheDirectories());
            }
            else if (folder.Id == "BrowserCaches")
            {
                directoriesToClean.AddRange(GetBrowserCacheDirectories());
            }
            else if (folder.Id == "DevPackageCaches")
            {
                directoriesToClean.AddRange(GetDevPackageDirectories());
            }
            else if (folder.Id == "WinDeliveryOpt")
            {
                directoriesToClean.AddRange(GetDeliveryOptimizationDirectories());
            }
            else
            {
                directoriesToClean.Add(folder.FolderPath);
            }

            bool applySafeTimeCheck = safeMode24Hours && (folder.Id is "UserTemp" or "WinTemp" or "SandboxTest");
            var cutoffTime = DateTime.Now - TimeSpan.FromHours(24);
            long lastProgressTime = 0;

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

                        if (applySafeTimeCheck && file.LastWriteTime > cutoffTime)
                        {
                            filesSkipped++;
                            continue;
                        }

                        // AI safety gate: protect files the heuristic engine flags as high-risk
                        var aiResult = AiFileSafetyService.AnalyzeFile(file.FullName, file.Name, "File", file.Length, file.LastWriteTime);
                        if (aiResult.Tier == AiSafetyTier.HighRiskKeep)
                        {
                            filesSkipped++;
                            continue;
                        }

                        try
                        {
                            long fileLen = file.Length;
                            string path = file.FullName;

                            // 1. Clear ReadOnly / Hidden / System attributes to prevent deletion failures
                            if ((file.Attributes & (FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System)) != 0)
                            {
                                try { file.Attributes = FileAttributes.Normal; } catch { }
                            }

                            // 2. Primary native Win32 deletion
                            if (DeleteFileW(path))
                            {
                                freedBytes += fileLen;
                                filesDeleted++;
                            }
                            else
                            {
                                // 3. Fallback standard .NET delete
                                try
                                {
                                    file.Delete();
                                    freedBytes += fileLen;
                                    filesDeleted++;
                                }
                                catch
                                {
                                    // 4. POSIX delete attempt for files shared with delete permission
                                    bool sharedDeleted = false;
                                    try
                                    {
                                        using (var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
                                        {
                                        }
                                        if (DeleteFileW(path))
                                        {
                                            freedBytes += fileLen;
                                            filesDeleted++;
                                            sharedDeleted = true;
                                        }
                                    }
                                    catch { }

                                    if (!sharedDeleted)
                                    {
                                        filesSkipped++;
                                    }
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
                        var subCheckOptions = new EnumerationOptions { IgnoreInaccessible = true };
                        foreach (var subDir in dirInfo.EnumerateDirectories("*", enumOptions).OrderByDescending(d => d.FullName.Length))
                        {
                            if (ct.IsCancellationRequested) break;
                            try
                            {
                                if (!subDir.EnumerateFileSystemInfos("*", subCheckOptions).Any())
                                {
                                    if ((subDir.Attributes & FileAttributes.ReadOnly) != 0)
                                    {
                                        try { subDir.Attributes = FileAttributes.Normal; } catch { }
                                    }

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

            // Flush Explorer thumbnail databases if cleaned
            if (folder.Id == "Thumbnails")
            {
                try
                {
                    SHChangeNotify(0x08000000 /* SHCNE_ASSOCCHANGED */, 0x0000 /* SHCNF_IDLIST */, IntPtr.Zero, IntPtr.Zero);
                }
                catch { }
            }

            // Deep Windows Component Store / WinSxS scavenger cleanup (DISM)
            if ((folder.Id == "WinComponentCaches" || folder.Id == "WinUpdateCache") && ElevationService.IsRunAsAdmin())
            {
                try
                {
                    var dismTask = RunDismComponentCleanupAsync(logAction, ct);
                    dismTask.GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    logAction($"DISM Component Store note: {ex.Message}", LogLevel.Info);
                }
            }

            folder.SizeBytes = Math.Max(0, folder.SizeBytes - freedBytes);
            folder.FileCount = Math.Max(0, folder.FileCount - filesDeleted);

            if (freedBytes > 0)
            {
                folder.StatusMessage = $"Reclaimed: {TargetFolderInfo.FormatBytes(freedBytes)}";
                logAction($"Cleaned {folder.Name}: {TargetFolderInfo.FormatBytes(freedBytes)} reclaimed ({filesDeleted:N0} files deleted, {filesSkipped:N0} in use/protected)", LogLevel.Success);
            }
            else if (filesSkipped > 0)
            {
                folder.StatusMessage = $"Protected ({filesSkipped:N0} in use)";
                logAction($"Protected {folder.Name}: {filesSkipped:N0} files in active use by running apps or protected by Safety Shield", LogLevel.Info);
            }
            else
            {
                folder.StatusMessage = "Already Clean (0 B)";
                logAction($"Checked {folder.Name}: Already clean (0 bytes)", LogLevel.Info);
            }

            folder.IsCleaning = false;
        }, ct);

        return (freedBytes, filesDeleted, foldersDeleted, filesSkipped);
    }

    public static async Task<(bool Success, string Message)> RunDismComponentCleanupAsync(Action<string, LogLevel>? logAction = null, CancellationToken ct = default)
    {
        if (!ElevationService.IsRunAsAdmin())
        {
            return (false, "Administrator privileges required to run Component Store cleanup.");
        }

        return await Task.Run(() =>
        {
            try
            {
                logAction?.Invoke("Initiating Windows Component Store (WinSxS) deep cleanup via DISM (purging superseded updates)...", LogLevel.Info);

                string dismPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "dism.exe");
                if (!File.Exists(dismPath)) return (false, "dism.exe not found");

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dismPath,
                    Arguments = "/Online /Cleanup-Image /StartComponentCleanup /NoRestart",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc == null) return (false, "Could not launch dism.exe");

                proc.WaitForExit(180_000); // 3-minute timeout

                if (proc.ExitCode == 0)
                {
                    logAction?.Invoke("Windows Component Store deep cleanup completed! Superseded Windows update packages purged.", LogLevel.Success);
                    return (true, "Component store cleaned successfully.");
                }
                else
                {
                    logAction?.Invoke($"DISM Component cleanup completed with exit code {proc.ExitCode}.", LogLevel.Info);
                    return (true, $"DISM exit code: {proc.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"DISM execution note: {ex.Message}", LogLevel.Warning);
                return (false, ex.Message);
            }
        }, ct);
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
                filePath.Contains("logs", StringComparison.OrdinalIgnoreCase) ||
                filePath.Contains("$windows.~", StringComparison.OrdinalIgnoreCase) ||
                filePath.Contains("esd", StringComparison.OrdinalIgnoreCase))
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
        sb.AppendLine("Deltempo - Windows Cleaner and Memory Optimizer (MIT Licensed)");
        return sb.ToString();
    }
}
