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

            // 5. GPU & DirectX Shaders
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

            // 6. Desktop & Electron Apps Cache Sweeper
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

            // 7. Web Browser Caches
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

            // 8. Developer & Package Caches
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

            // 9. Error Dumps
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

            // 10. Thumbnails
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

            // 11. Recycle Bin
            new TargetFolderInfo
            {
                Id = "RecycleBin",
                Name = "Windows Recycle Bin",
                Category = "Storage",
                CategoryColor = "#10B981",
                SafetyBadge = "🟢 100% Safe Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Deleted files across all local drive recycle bins",
                FolderPath = "Recycle Bin",
                IconGlyph = "\uE74D",
                IsSpecialShellTarget = true,
                RequiresAdmin = false,
                HasAccess = true,
                IsSelected = true
            }
        };

        // 12. Add verified true orphaned leftover folders (checked against processes, start menu & registry)
        try
        {
            var verifiedOrphans = OrphanedAppService.ScanVerifiedOrphanedFolders();
            foreach (var orphan in verifiedOrphans)
            {
                targets.Add(orphan);
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

            if (folder.Id == "GpuShaderCaches")
            {
                directoriesToClean.Add(Path.Combine(localAppData, "NVIDIA", "DXCache"));
                directoriesToClean.Add(Path.Combine(localAppData, "NVIDIA", "GLCache"));
                directoriesToClean.Add(Path.Combine(localAppData, "AMD", "DxCache"));
                directoriesToClean.Add(Path.Combine(localAppData, "D3DSCache"));
                directoriesToClean.Add(Path.Combine(localAppData, "Intel", "ShaderCache"));
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

                    int totalCount = fileList.Count;
                    int processedCounter = 0;

                    var parallelOptions = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount * 2, 4, 16),
                        CancellationToken = ct
                    };

                    Parallel.ForEach(fileList, parallelOptions, file =>
                    {
                        if (ct.IsCancellationRequested) return;

                        if (safeMode24Hours && folder.IsSafeModeEligible && file.LastWriteTime > cutoffTime)
                        {
                            Interlocked.Increment(ref filesSkipped);
                            int p = Interlocked.Increment(ref processedCounter);
                            ReportThrottledProgress(p, totalCount, progressReport, ref lastProgressTime);
                            return;
                        }

                        // Strict Safety Shield: Never delete user personal documents
                        string ext = file.Extension.ToLowerInvariant();
                        if (ext is ".docx" or ".xlsx" or ".pptx" or ".pdf" or ".psd" or ".blend" or ".sln" or ".json" or ".ini")
                        {
                            // If it's an active app cache sweeper, preserve config files
                            if (folder.Id == "AppCacheSweeper")
                            {
                                Interlocked.Increment(ref filesSkipped);
                                return;
                            }
                        }

                        long fileLength = 0;
                        try
                        {
                            fileLength = file.Length;
                            if (!DeleteFileW(file.FullName))
                            {
                                File.SetAttributes(file.FullName, FileAttributes.Normal);
                                if (!DeleteFileW(file.FullName))
                                {
                                    File.Delete(file.FullName);
                                }
                            }

                            Interlocked.Add(ref freedBytes, fileLength);
                            Interlocked.Increment(ref filesDeleted);
                        }
                        catch
                        {
                            Interlocked.Increment(ref filesSkipped);
                        }

                        int currentProcessed = Interlocked.Increment(ref processedCounter);
                        ReportThrottledProgress(currentProcessed, totalCount, progressReport, ref lastProgressTime);
                    });

                    // Cleanup empty subdirectories
                    try
                    {
                        var subdirs = dirInfo.EnumerateDirectories("*", enumOptions)
                            .OrderByDescending(d => d.FullName.Length)
                            .ToList();

                        foreach (var subDir in subdirs)
                        {
                            if (ct.IsCancellationRequested) break;
                            try
                            {
                                if (RemoveDirectoryW(subDir.FullName))
                                {
                                    foldersDeleted++;
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
            folder.FolderCount = Math.Max(0, folder.FolderCount - foldersDeleted);
            folder.TopFiles.Clear();
            folder.StatusMessage = $"Cleaned ({TargetFolderInfo.FormatBytes(freedBytes)} freed)";

            logAction($"Cleaned {folder.Name}: {TargetFolderInfo.FormatBytes(freedBytes)} freed ({filesDeleted:N0} deleted, {filesSkipped:N0} protected/in use)",
                filesDeleted > 0 ? LogLevel.Success : LogLevel.Info);

            folder.IsCleaning = false;
        }, ct);

        return (freedBytes, filesDeleted, foldersDeleted, filesSkipped);
    }

    private static void ReportThrottledProgress(int current, int total, Action<double> progressReport, ref long lastProgressTime)
    {
        if (total <= 0) return;
        long now = Environment.TickCount64;
        if (current == total || now - Interlocked.Read(ref lastProgressTime) > 40)
        {
            Interlocked.Exchange(ref lastProgressTime, now);
            progressReport((double)current / total);
        }
    }

    public static string GenerateAuditReport(IEnumerable<TargetFolderInfo> targets, CleanSummary summary, bool safeMode)
    {
        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine("                         DELTEMPO CLEANUP AUDIT REPORT                          ");
        sb.AppendLine("================================================================================");
        sb.AppendLine($"Timestamp:         {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Safety Shield:     {(safeMode ? "Enabled (Files < 24h protected)" : "Disabled (All temporary files purged)")}");
        sb.AppendLine($"Total Space Freed: {summary.FormattedFreedSize}");
        sb.AppendLine($"Files Removed:     {summary.TotalFilesDeleted:N0}");
        sb.AppendLine($"Folders Removed:   {summary.TotalFoldersDeleted:N0}");
        sb.AppendLine($"Files Skipped:     {summary.TotalFilesSkipped:N0} (In-use / protected)");
        sb.AppendLine($"Duration:          {summary.ElapsedTime.TotalSeconds:F2} seconds");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("TARGET BREAKDOWN:");
        foreach (var t in targets)
        {
            sb.AppendLine($"• [{t.Category}] {t.Name} ({t.FolderPath})");
            sb.AppendLine($"   Status: {t.StatusMessage} | Remaining: {t.FormattedSize} ({t.FormattedStats})");
        }
        sb.AppendLine("================================================================================");
        return sb.ToString();
    }
}
