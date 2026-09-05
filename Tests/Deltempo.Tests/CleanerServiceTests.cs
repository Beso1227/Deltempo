using System.IO;
using WinTempCleaner.Models;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

public class CleanerServiceTests : IDisposable
{
    private readonly string _testSandboxDir;
    private readonly CleanerService _cleanerService;

    public CleanerServiceTests()
    {
        _testSandboxDir = Path.Combine(Path.GetTempPath(), "Deltempo_xUnit_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testSandboxDir);
        _cleanerService = new CleanerService();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testSandboxDir))
            {
                Directory.Delete(_testSandboxDir, true);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }

    [Fact]
    public void GetDefaultTargets_ReturnsAllDeepScanStandardCategories()
    {
        // Act
        var targets = CleanerService.GetDefaultTargets();

        // Assert
        Assert.NotNull(targets);
        Assert.True(targets.Count >= 25, $"Expected at least 25 targets, but found {targets.Count}");
        Assert.Contains(targets, t => t.Id == "WinUpgradeLeftovers");
        Assert.Contains(targets, t => t.Id == "WinStoreAppCaches");
        Assert.Contains(targets, t => t.Id == "MessagingAppCaches");
        Assert.Contains(targets, t => t.Id == "WinComponentCaches");
        Assert.Contains(targets, t => t.Id == "DeviceDriverPackages");
        Assert.Contains(targets, t => t.Id == "DefenderAntivirus");
        Assert.Contains(targets, t => t.Id == "WinSystemLogs");
        Assert.Contains(targets, t => t.Id == "TemporaryInternetFiles");
        Assert.Contains(targets, t => t.Id == "SystemUsageTraces");
        Assert.Contains(targets, t => t.Id == "SystemRestorePoints");
        Assert.Contains(targets, t => t.Id == "RecycleBin");
    }

    [Theory]
    [InlineData("1024 B", 1024)]
    [InlineData("50 KB", 50 * 1024)]
    [InlineData("100.5 MB", (long)(100.5 * 1024 * 1024))]
    [InlineData("8.5 GB", (long)(8.5 * 1024 * 1024 * 1024))]
    [InlineData("2 TB", 2L * 1024 * 1024 * 1024 * 1024)]
    public void ParseSizeStringToBytes_ConvertsVariousUnitsAccurately(string input, long expectedApprox)
    {
        long actual = CleanerService.ParseSizeStringToBytes(input);
        Assert.Equal(expectedApprox, actual);
    }

    [Fact]
    public void DirectoryResolvers_ReturnValidNonEmptyTargetLists()
    {
        var upgradeDirs = CleanerService.GetUpgradeLeftoverDirectories();
        Assert.NotEmpty(upgradeDirs);

        var compDirs = CleanerService.GetComponentCacheDirectories();
        Assert.NotEmpty(compDirs);

        var devDirs = CleanerService.GetDevPackageDirectories();
        Assert.NotEmpty(devDirs);
        Assert.Contains(devDirs, d => d.Contains("pip", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(devDirs, d => d.Contains("npm", StringComparison.OrdinalIgnoreCase));

        var appDirs = CleanerService.GetAppCacheDirectories();
        Assert.NotEmpty(appDirs);
        Assert.Contains(appDirs, d => d.Contains("Code", StringComparison.OrdinalIgnoreCase));

        var messagingDirs = CleanerService.GetMessagingAppCacheDirectories();
        Assert.NotEmpty(messagingDirs);
        Assert.Contains(messagingDirs, d => d.Contains("discord", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(messagingDirs, d => d.Contains("whatsapp", StringComparison.OrdinalIgnoreCase));

        var browserDirs = CleanerService.GetBrowserCacheDirectories();
        Assert.NotNull(browserDirs);
    }

    [Fact]
    public async Task CleanFolderAsync_DeletesOldFiles_AndProtectsRecentFilesUnder24Hours()
    {
        // Arrange
        var oldFile = Path.Combine(_testSandboxDir, "stale_cache.tmp");
        File.WriteAllText(oldFile, "stale cache data");
        File.SetLastWriteTime(oldFile, DateTime.Now - TimeSpan.FromHours(48));

        var recentFile = Path.Combine(_testSandboxDir, "active_installer.tmp");
        File.WriteAllText(recentFile, "active installer payload");
        File.SetLastWriteTime(recentFile, DateTime.Now - TimeSpan.FromMinutes(10));

        var target = new TargetFolderInfo
        {
            Id = "SandboxTest",
            Name = "Sandbox Test Target",
            FolderPath = _testSandboxDir,
            IsSafeModeEligible = true
        };

        // Act - Scan
        await _cleanerService.ScanFolderAsync(target, (msg, lvl) => { }, CancellationToken.None);
        Assert.Equal(2, target.FileCount);

        // Act - Clean with 24-hour Safe Mode ENABLED
        var (freedBytes, filesDeleted, foldersDeleted, filesSkipped) = await _cleanerService.CleanFolderAsync(
            target,
            safeMode24Hours: true,
            logAction: (msg, lvl) => { },
            progressReport: p => { },
            ct: CancellationToken.None);

        // Assert
        Assert.False(File.Exists(oldFile), "Old file (>24h) should be purged");
        Assert.True(File.Exists(recentFile), "Recent file (<24h) must be protected by Safety Shield");
        Assert.Equal(1, filesDeleted);
        Assert.Equal(1, filesSkipped);
        Assert.True(freedBytes > 0);
    }

    [Fact]
    public async Task CleanFolderAsync_PureCacheScopes_PurgesRecentCacheFilesEvenWithSafeModeEnabled()
    {
        // Arrange - Cache file created 5 minutes ago in a pure cache target
        var cacheFile = Path.Combine(_testSandboxDir, "recent_browser_cache.data");
        File.WriteAllText(cacheFile, "pure disposable cache data");
        File.SetLastWriteTime(cacheFile, DateTime.Now - TimeSpan.FromMinutes(5));

        var target = new TargetFolderInfo
        {
            Id = "CustomCacheScope",
            Name = "Custom Pure Cache Scope",
            FolderPath = _testSandboxDir,
            IsSafeModeEligible = false
        };

        // Act - Clean with SafeMode enabled (pure caches should NOT be blocked)
        var (freedBytes, filesDeleted, foldersDeleted, filesSkipped) = await _cleanerService.CleanFolderAsync(
            target,
            safeMode24Hours: true,
            logAction: (msg, lvl) => { },
            progressReport: p => { },
            ct: CancellationToken.None);

        // Assert
        Assert.False(File.Exists(cacheFile), "Pure cache file under 24h should be cleaned directly");
        Assert.Equal(1, filesDeleted);
        Assert.Equal(0, filesSkipped);
        Assert.True(freedBytes > 0);
    }

    [Fact]
    public async Task CleanFolderAsync_HandlesLockedFilesInUseWithoutCrashing()
    {
        // Arrange
        var lockedFile = Path.Combine(_testSandboxDir, "locked_process.dat");
        File.WriteAllText(lockedFile, "locked content");

        using var fileStream = new FileStream(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var target = new TargetFolderInfo
        {
            Id = "LockTest",
            Name = "Lock Test Target",
            FolderPath = _testSandboxDir
        };

        // Act & Assert (Should complete safely without unhandled exceptions)
        var (freedBytes, filesDeleted, foldersDeleted, filesSkipped) = await _cleanerService.CleanFolderAsync(
            target,
            safeMode24Hours: false,
            logAction: (msg, lvl) => { },
            progressReport: p => { },
            ct: CancellationToken.None);

        Assert.True(File.Exists(lockedFile), "Locked file should remain intact");
        Assert.Equal(1, filesSkipped);
        Assert.Equal(0, filesDeleted);
    }

    [Fact]
    public void GenerateAuditReport_ContainsAccurateSummaryMetrics()
    {
        // Arrange
        var targets = new List<TargetFolderInfo>
        {
            new TargetFolderInfo { Id = "T1", Name = "User Temp", Category = "User Cache", StatusMessage = "Reclaimed: 15.0 MB" }
        };

        var summary = new CleanSummary
        {
            TotalFreedBytes = 15L * 1024 * 1024,
            TotalFilesDeleted = 42,
            TotalFoldersDeleted = 3,
            TotalFilesSkipped = 5
        };

        // Act
        var report = CleanerService.GenerateAuditReport(targets, summary, safeMode: true);

        // Assert
        Assert.Contains("DELTEMPO SYSTEM PURGE & AUDIT REPORT", report);
        Assert.Contains("Safety Shield    : ENABLED", report);
        Assert.Contains("42", report);
        Assert.Contains("15.0 MB", report);
    }

    [Fact]
    public void LocalizationService_LocalizesAllEnhancedTargetsWithoutErrors()
    {
        var targets = CleanerService.GetDefaultTargets();
        foreach (var lang in new[] { "en", "ar", "es", "fr", "de" })
        {
            LocalizationService.CurrentLanguage = lang;
            foreach (var t in targets)
            {
                LocalizationService.LocalizeTarget(t);
                Assert.False(string.IsNullOrWhiteSpace(t.Name));
                Assert.False(string.IsNullOrWhiteSpace(t.Category));
                Assert.False(string.IsNullOrWhiteSpace(t.Description));
            }
        }
        LocalizationService.CurrentLanguage = "en";
    }

    [Fact]
    public void MemoryOptimizer_GetMemoryInfo_ReturnsValidPositiveBytes()
    {
        var mem = MemoryOptimizerService.GetMemoryInfo();
        Assert.True(mem.TotalPhysicalBytes > 0, "Total physical memory should be positive");
        Assert.True(mem.AvailablePhysicalBytes >= 0, "Available physical memory should be non-negative");
        Assert.True(mem.UsedPercent >= 0.0 && mem.UsedPercent <= 100.0, "Used percent should be between 0 and 100");
    }

    [Fact]
    public void MemoryOptimizer_GetMemoryAreaSnapshots_ReturnsAllEightZonesWithBadges()
    {
        var snapshots = MemoryOptimizerService.GetMemoryAreaSnapshots();
        Assert.NotNull(snapshots);
        Assert.Equal(8, snapshots.Count);
        Assert.All(snapshots, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(s.Description));
            Assert.False(string.IsNullOrWhiteSpace(s.SafetyBadge));
            Assert.False(string.IsNullOrWhiteSpace(s.IconGlyph));
            Assert.True(s.TotalBytes > 0);
        });
    }

    [Fact]
    public void MemoryOptimizer_AllTargetTypes_ContainsExpectedTargets()
    {
        var targets = MemoryOptimizerService.AllTargetTypes();
        Assert.Equal(8, targets.Length);
        Assert.Contains(MemoryTargetType.StandbyList, targets);
        Assert.Contains(MemoryTargetType.WorkingSet, targets);
        Assert.Contains(MemoryTargetType.SystemFileCache, targets);
        Assert.Contains(MemoryTargetType.CombinedPageList, targets);
        Assert.Contains(MemoryTargetType.ModifiedFileCache, targets);
        Assert.Contains(MemoryTargetType.RegistryCache, targets);
    }

    [Fact]
    public void MemoryOptimizer_DefaultActiveTargetTypes_MatchesWinMemoryCleanerSevenZones()
    {
        var active = MemoryOptimizerService.DefaultActiveTargetTypes();
        Assert.Equal(7, active.Length);
        Assert.Contains(MemoryTargetType.WorkingSet, active);
        Assert.Contains(MemoryTargetType.StandbyList, active);
        Assert.Contains(MemoryTargetType.SystemFileCache, active);
        Assert.Contains(MemoryTargetType.ModifiedPageList, active);
        Assert.Contains(MemoryTargetType.CombinedPageList, active);
        Assert.Contains(MemoryTargetType.ModifiedFileCache, active);
        Assert.Contains(MemoryTargetType.RegistryCache, active);
    }

    [Fact]
    public async Task MemoryOptimizer_OptimizeAreaAsync_WorkingSet_ExecutesSafely()
    {
        var result = await MemoryOptimizerService.OptimizeAreaAsync(MemoryTargetType.WorkingSet);
        Assert.NotNull(result);
        Assert.Equal(MemoryTargetType.WorkingSet, result.Target);
        Assert.True(result.BytesFreed >= 0);
    }

    [Fact]
    public async Task MemoryOptimizer_OptimizeRamAsync_WinMemoryCleanerEngine_ReturnsValidResult()
    {
        var result = await MemoryOptimizerService.OptimizeRamAsync();
        Assert.NotNull(result);
        Assert.True(result.ExecutionTimeMs >= 0);
        Assert.True(result.ReclaimedBytes >= 0);
        Assert.NotEmpty(result.AreaResults);
        Assert.Equal(7, result.AreaResults.Count);
    }

    [Fact]
    public async Task StartupManager_GetStartupItemsAsync_ReturnsListWithoutExceptions()
    {
        var items = await StartupManagerService.GetStartupItemsAsync();
        Assert.NotNull(items);
    }

    [Theory]
    [InlineData("\"C:\\Program Files\\App\\app.exe\" --arg1", "C:\\Program Files\\App\\app.exe")]
    [InlineData("\"D:\\Games\\launcher.exe\"", "D:\\Games\\launcher.exe")]
    [InlineData("C:\\Windows\\system32\\cmd.exe /c start", "C:\\Windows\\system32\\cmd.exe")]
    [InlineData("", "")]
    public void StartupManager_ExtractExecutablePath_ParsesPathsCorrectly(string rawCmd, string expectedPrefix)
    {
        string extracted = StartupManagerService.ExtractExecutablePath(rawCmd);
        if (string.IsNullOrEmpty(expectedPrefix))
        {
            Assert.True(string.IsNullOrEmpty(extracted));
        }
        else
        {
            Assert.StartsWith(expectedPrefix, extracted, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void StartupManager_LocationDisplay_ProvidesClearLocations()
    {
        var item1 = new StartupItem { Location = "HKCU" };
        var item2 = new StartupItem { Location = "HKLM" };
        var item3 = new StartupItem { Location = "WOW64_HKLM" };
        var item4 = new StartupItem { Location = "Startup Folder" };

        Assert.Equal("Registry (Current User)", item1.LocationDisplay);
        Assert.Equal("Registry (All Users)", item2.LocationDisplay);
        Assert.Equal("Registry (32-bit All Users)", item3.LocationDisplay);
        Assert.Equal("Startup Folder (User)", item4.LocationDisplay);
    }

    [Fact]
    public void ProcessOptimizer_TrimProcessMemoryEx_HandlesInvalidPidsGracefully()
    {
        var (success, freed) = ProcessOptimizerService.TrimProcessMemoryEx(new[] { 99999999 });
        Assert.False(success);
        Assert.Equal(0, freed);
    }

    [Fact]
    public async Task ProcessOptimizer_GetHeavyProcessesAsync_ExcludesProtectedProcesses()
    {
        var procs = await ProcessOptimizerService.GetHeavyProcessesAsync(1024);
        Assert.NotNull(procs);
        Assert.DoesNotContain(procs, p => ProcessOptimizerService.IsProtectedProcess(p.ProcessName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ProcessOptimizer_IsProtectedProcess_HandlesEmptyOrNullSafely(string? processName)
    {
        bool isProtected = ProcessOptimizerService.IsProtectedProcess(processName!);
        Assert.True(isProtected, "Null or empty process name should fail-safe to protected");
    }

    [Fact]
    public void OrphanedAppService_GetComprehensiveActiveAppKeywords_ContainsCoreVendors()
    {
        var keywords = OrphanedAppService.GetComprehensiveActiveAppKeywords();
        Assert.NotNull(keywords);
        Assert.Contains("Microsoft", keywords);
        Assert.Contains("Windows", keywords);
        Assert.Contains("Temp", keywords);
    }

    [Fact]
    public async Task CleanFolderAsync_RemovesEmptySubdirectoriesSafely()
    {
        // Arrange
        var emptySubDir = Path.Combine(_testSandboxDir, "empty_temp_subdir");
        Directory.CreateDirectory(emptySubDir);

        var target = new TargetFolderInfo
        {
            Id = "SubDirTest",
            Name = "SubDir Test Target",
            FolderPath = _testSandboxDir
        };

        // Act
        var (_, _, foldersDeleted, _) = await _cleanerService.CleanFolderAsync(
            target,
            safeMode24Hours: false,
            logAction: (msg, lvl) => { },
            progressReport: p => { },
            ct: CancellationToken.None);

        // Assert
        Assert.False(Directory.Exists(emptySubDir), "Empty subdirectory should be removed");
        Assert.True(foldersDeleted >= 1, "At least one folder should be deleted");
    }

    [Theory]
    [InlineData(".iso", "Installer / ISO", "\uE8B7")]
    [InlineData(".pak", "Game Asset / Pak", "\uE7FC")]
    [InlineData(".safetensors", "AI Model / Weights", "\uE943")]
    [InlineData(".mp4", "Video / Media", "\uE714")]
    [InlineData(".zip", "Archive", "\uF012")]
    [InlineData(".vmdk", "Virtual Disk / Image", "\uEDA2")]
    [InlineData(".exe", "Application / Binary", "\uE756")]
    public void LargeFileHunter_ClassifyFileCategory_CategorizesAccurately(string ext, string expectedCat, string expectedIcon)
    {
        var (category, icon) = LargeFileHunterService.ClassifyFileCategory(ext);
        Assert.Equal(expectedCat, category);
        Assert.Equal(expectedIcon, icon);
    }

    [Fact]
    public void LargeFileHunter_GetAvailableDrives_ReturnsValidDrives()
    {
        var drives = LargeFileHunterService.GetAvailableDrives();
        Assert.NotEmpty(drives);
        Assert.Contains(drives, d => d.Contains(':'));
    }

    [Fact]
    public async Task LargeFileHunter_ScanLargeFilesAsync_DetectsFileAboveThreshold()
    {
        // Arrange
        string testFile = Path.Combine(_testSandboxDir, "big_test_asset.pak");
        long targetSize = 55L * 1024 * 1024; // 55 MB
        using (var fs = new FileStream(testFile, FileMode.Create, FileAccess.Write))
        {
            fs.SetLength(targetSize);
        }

        string smallFile = Path.Combine(_testSandboxDir, "small_test_asset.txt");
        using (var fs = new FileStream(smallFile, FileMode.Create, FileAccess.Write))
        {
            fs.SetLength(1024);
        }

        try
        {
            // Act
            var results = await LargeFileHunterService.ScanLargeFilesAsync(
                minSizeBytes: 50L * 1024 * 1024,
                targetScope: _testSandboxDir);

            // Assert
            Assert.Contains(results, f => f.FileName == "big_test_asset.pak");
            Assert.DoesNotContain(results, f => f.FileName == "small_test_asset.txt");

            var match = results.First(f => f.FileName == "big_test_asset.pak");
            Assert.Equal("Game Asset / Pak", match.Category);
            Assert.Equal("\uE7FC", match.CategoryIcon);
            Assert.Equal(targetSize, match.SizeBytes);
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
            if (File.Exists(smallFile)) File.Delete(smallFile);
        }
    }

    [Fact]
    public void OrphanedAppService_ScanVerifiedOrphanedFolders_NeverFlagsProtectedSystemFolders()
    {
        var orphans = OrphanedAppService.ScanVerifiedOrphanedFolders();
        Assert.NotNull(orphans);

        // Crucial security invariant: Windows, Microsoft, Common Files, etc. MUST never appear in orphans
        Assert.DoesNotContain(orphans, o => o.Name.Contains("Windows", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(orphans, o => o.Name.Contains("Microsoft", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(orphans, o => o.FolderPath.Contains("Common Files", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(orphans, o => o.FolderPath.Contains("Package Cache", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void OrphanedAppService_AllDiscoveredOrphans_AreUncheckedByDefault()
    {
        var orphans = OrphanedAppService.ScanVerifiedOrphanedFolders();
        foreach (var orphan in orphans)
        {
            Assert.False(orphan.IsSelected, $"Orphaned folder '{orphan.Name}' must be unselected by default for safety.");
            Assert.True(orphan.IsOrphanedAppFolder);
        }
    }

    [Fact]
    public void AiFileSafetyService_DownloadsInstaller_ClassifiesAsSafeToClean()
    {
        string path = @"C:\Users\JohnDoe\Downloads\Win11_23H2_English_x64.iso";
        var result = AiFileSafetyService.AnalyzeFile(path, "Win11_23H2_English_x64.iso", "Installer / ISO", 5L * 1024 * 1024 * 1024, DateTime.Now.AddDays(-30));

        Assert.Equal(AiSafetyTier.SafeToClean, result.Tier);
        Assert.True(result.IsSafeToAutoClean);
        Assert.True(result.SafetyScore >= 90);
        Assert.Contains("SAFE", result.Verdict);
    }

    [Fact]
    public void AiFileSafetyService_CrashDump_ClassifiesAsSafeToClean()
    {
        string path = @"C:\ProgramData\CrashDumps\app_crash.dmp";
        var result = AiFileSafetyService.AnalyzeFile(path, "app_crash.dmp", "Dump / Temp / Download", 800L * 1024 * 1024, DateTime.Now.AddDays(-5));

        Assert.Equal(AiSafetyTier.SafeToClean, result.Tier);
        Assert.True(result.IsSafeToAutoClean);
        Assert.True(result.SafetyScore >= 95);
    }

    [Fact]
    public void AiFileSafetyService_SteamGameAsset_ClassifiesAsHighRisk()
    {
        string path = @"D:\SteamLibrary\steamapps\common\Palworld\Pal\Content\Paks\Pal-Windows.pak";
        var result = AiFileSafetyService.AnalyzeFile(path, "Pal-Windows.pak", "Game Asset / Pak", 18L * 1024 * 1024 * 1024, DateTime.Now.AddDays(-10));

        Assert.Equal(AiSafetyTier.HighRiskKeep, result.Tier);
        Assert.False(result.IsSafeToAutoClean);
        Assert.True(result.SafetyScore <= 30);
        Assert.Contains("Steam", result.Origin);
        Assert.Contains("PROTECTED", result.Verdict);
    }

    [Fact]
    public void AiFileSafetyService_ProjectModelWeights_ClassifiesAsHighRisk()
    {
        string path = @"D:\Projects\deltempo\ai_models\weights.safetensors";
        var result = AiFileSafetyService.AnalyzeFile(path, "weights.safetensors", "AI Model / Weights", 4L * 1024 * 1024 * 1024, DateTime.Now.AddDays(-2));

        Assert.Equal(AiSafetyTier.HighRiskKeep, result.Tier);
        Assert.False(result.IsSafeToAutoClean);
        Assert.True(result.SafetyScore <= 20);
        Assert.Contains("PROTECTED", result.Verdict);
    }

    [Theory]
    [InlineData(@"C:\Windows\Prefetch\NOTEPAD.EXE-1234.pf", "NOTEPAD.EXE-1234.pf")]
    [InlineData(@"C:\Windows\SoftwareDistribution\Download\abc\update.cab", "update.cab")]
    [InlineData(@"C:\Windows\Minidump\minidump.dmp", "minidump.dmp")]
    [InlineData(@"C:\Windows\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache\f123", "f123")]
    [InlineData(@"C:\Users\username\AppData\Local\D3DSCache\test.bin", "test.bin")]
    [InlineData(@"C:\Users\username\AppData\Local\Google\Chrome\User Data\Default\Cache\Cache_Data\data_0", "data_0")]
    public void AiFileSafetyService_SystemDisposableFolders_CorrectlyMarkedSafeToClean(string path, string filename)
    {
        var result = AiFileSafetyService.AnalyzeFile(path, filename, "System Cache Chunk", 5 * 1024 * 1024, DateTime.Now.AddDays(-5));

        Assert.Equal(AiSafetyTier.SafeToClean, result.Tier);
        Assert.True(result.IsSafeToAutoClean);
        Assert.True(result.SafetyScore >= 90);
        Assert.Contains("SAFE", result.Verdict);
    }

    [Fact]
    public void CleanerService_GetDeliveryOptimizationDirectories_ReturnsValidPaths()
    {
        var dirs = CleanerService.GetDeliveryOptimizationDirectories();
        Assert.NotNull(dirs);
        Assert.True(dirs.Count >= 2);
        Assert.Contains(dirs, d => d.Contains("DeliveryOptimization", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CleanerService_GetComponentCacheDirectories_IncludesPatchCacheAndPackageCache()
    {
        var dirs = CleanerService.GetComponentCacheDirectories();
        Assert.NotNull(dirs);
        Assert.Contains(dirs, d => d.Contains("$PatchCache$", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dirs, d => d.Contains("Package Cache", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CleanerService_GetWinSystemLogDirectories_IncludesDiagnosticLogsAndPanther()
    {
        var dirs = CleanerService.GetWinSystemLogDirectories();
        Assert.NotNull(dirs);
        Assert.Contains(dirs, d => d.Contains("Panther", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dirs, d => d.Contains("ETLLogs", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CleanerService_GetUpgradeLeftoverDirectories_IncludesModernSetupPaths()
    {
        var dirs = CleanerService.GetUpgradeLeftoverDirectories();
        Assert.NotNull(dirs);
        Assert.Contains(dirs, d => d.Contains("MoSetup", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dirs, d => d.Contains("$WINDOWS.~BT", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CleanerService_GetMessagingAppCacheDirectories_ReturnsSafePaths()
    {
        var dirs = CleanerService.GetMessagingAppCacheDirectories();
        Assert.NotNull(dirs);
        Assert.NotEmpty(dirs);
        Assert.Contains(dirs, d => d.Contains("whatsapp", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dirs, d => d.Contains("telegram", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dirs, d => d.Contains("discord", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dirs, d => d.Contains("slack", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CleanerService_GetStoreAppCacheDirectories_DoesNotContainRawLocalCacheRoots()
    {
        var dirs = CleanerService.GetStoreAppCacheDirectories();
        Assert.NotNull(dirs);
        // None of the directories should be a raw LocalCache root
        foreach (var dir in dirs)
        {
            var trimmed = dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Assert.False(trimmed.EndsWith(@"\LocalCache", StringComparison.OrdinalIgnoreCase),
                $"Path '{dir}' is a raw LocalCache root which would wipe app sessions!");
            Assert.False(trimmed.EndsWith(@"\LocalCache\Roaming", StringComparison.OrdinalIgnoreCase),
                $"Path '{dir}' is a raw LocalCache\\Roaming root!");
            Assert.False(trimmed.EndsWith(@"\LocalCache\Local", StringComparison.OrdinalIgnoreCase),
                $"Path '{dir}' is a raw LocalCache\\Local root!");
            Assert.False(trimmed.EndsWith(@"\LocalCache\EBWebView", StringComparison.OrdinalIgnoreCase),
                $"Path '{dir}' is a raw EBWebView root!");
            Assert.False(trimmed.EndsWith(@"\LocalCache\EBWebView\Default", StringComparison.OrdinalIgnoreCase),
                $"Path '{dir}' is a raw EBWebView Default profile root!");
        }
    }

    [Theory]
    [InlineData(@"C:\Users\user\AppData\Local\Packages\5319275A.WhatsAppDesktop_cv1g1gvanyjgm\LocalCache\EBWebView\Default\Login Data", true)]
    [InlineData(@"C:\Users\user\AppData\Local\Packages\5319275A.WhatsAppDesktop_cv1g1gvanyjgm\LocalCache\EBWebView\Default\Cookies", true)]
    [InlineData(@"C:\Users\user\AppData\Local\Packages\5319275A.WhatsAppDesktop_cv1g1gvanyjgm\LocalCache\EBWebView\Default\IndexedDB\https_web.whatsapp.com_0.indexeddb.leveldb\000005.ldb", true)]
    [InlineData(@"C:\Users\user\AppData\Local\Packages\5319275A.WhatsAppDesktop_cv1g1gvanyjgm\LocalCache\EBWebView\Default\Local Storage\leveldb\000003.log", true)]
    [InlineData(@"C:\Users\user\AppData\Local\Packages\TelegramMessengerLLP.TelegramDesktop_t4vj0pshhgkwm\LocalCache\Roaming\Telegram Desktop UWP\tdata\D877F783D5D3EF8C0", true)]
    [InlineData(@"C:\Users\user\AppData\Local\Packages\TelegramMessengerLLP.TelegramDesktop_t4vj0pshhgkwm\LocalCache\Roaming\Telegram Desktop UWP\tdata\key_datas", true)]
    [InlineData(@"C:\Users\user\AppData\Local\Packages\TelegramMessengerLLP.TelegramDesktop_t4vj0pshhgkwm\LocalCache\Roaming\Telegram Desktop UWP\tdata\settings.dat", true)]
    [InlineData(@"C:\Users\user\AppData\Roaming\Telegram Desktop\tdata\D877F783D5D3EF8C1", true)]
    [InlineData(@"C:\Users\user\AppData\Roaming\Telegram Desktop\tdata\user_data\cache\data_0", false)]
    [InlineData(@"C:\Users\user\AppData\Roaming\Telegram Desktop\tdata\temp\temp_01.tmp", false)]
    [InlineData(@"C:\Users\user\AppData\Local\Packages\5319275A.WhatsAppDesktop_cv1g1gvanyjgm\LocalCache\EBWebView\Default\Cache\Cache_Data\data_1", true)]
    [InlineData(@"C:\Users\user\AppData\Local\Packages\MSTeams_8wekyb3d8bbwe\LocalCache\Local\Microsoft\Identity\msal.cache", true)]
    [InlineData(@"C:\Users\user\AppData\Local\Packages\MSTeams_8wekyb3d8bbwe\LocalCache\Microsoft\MSTeams\previous_session_data.json", true)]
    [InlineData(@"C:\Users\user\AppData\Local\Packages\MSTeams_8wekyb3d8bbwe\LocalCache\Microsoft\MSTeams\app_settings.json", true)]
    [InlineData(@"C:\Users\user\AppData\Local\Packages\MSTeams_8wekyb3d8bbwe\LocalCache\Microsoft\MSTeams\ecs_settings.dat64", true)]
    [InlineData(@"C:\Users\user\AppData\Local\Packages\MSTeams_8wekyb3d8bbwe\LocalCache\Microsoft\MSTeams\EBWebView\Default\IndexedDB\teams.leveldb\000003.ldb", true)]
    [InlineData(@"C:\Users\user\AppData\Local\Packages\MSTeams_8wekyb3d8bbwe\LocalCache\Microsoft\MSTeams\EBWebView\Default\Cache\Cache_Data\data_0", true)]
    [InlineData(@"C:\Users\user\AppData\Roaming\discord\Local Storage\leveldb\000005.ldb", true)]
    [InlineData(@"C:\Users\user\AppData\Roaming\discord\GPUCache\data_0", false)]
    [InlineData(@"C:\Users\user\AppData\Roaming\Slack\Local Storage\leveldb\000003.log", true)]
    [InlineData(@"C:\Users\user\AppData\Roaming\Slack\GPUCache\data_0", false)]
    [InlineData(@"C:\Users\user\AppData\Roaming\Signal\sql\db.sqlite", true)]
    [InlineData(@"C:\Users\user\AppData\Roaming\Signal\GPUCache\data_0", false)]
    [InlineData(@"C:\Users\user\AppData\Local\Zoom\data\zoom_meeting.db", true)]
    [InlineData(@"C:\Users\user\AppData\Local\Zoom\temp\meeting_log.tmp", false)]
    [InlineData(@"C:\Users\user\AppData\Roaming\Mattermost\GPUCache\data_0", false)]
    [InlineData(@"C:\Users\user\AppData\Roaming\Mattermost\Local Storage\leveldb\000001.ldb", true)]
    [InlineData(@"C:\Users\user\AppData\Roaming\Microsoft\Skype for Desktop\GPUCache\data_0", false)]
    [InlineData(@"C:\Users\user\AppData\Roaming\Microsoft\Skype for Desktop\Local Storage\leveldb\000001.ldb", true)]
    [InlineData(@"C:\Users\user\AppData\Local\ViberPC\avatars\user_123.jpg", false)]
    [InlineData(@"C:\Users\user\AppData\Roaming\ViberPC\viber.db", true)]
    [InlineData(@"C:\Users\user\AppData\Roaming\Element\GPUCache\data_0", false)]
    [InlineData(@"C:\Users\user\AppData\Roaming\Element\IndexedDB\matrix.leveldb\000001.ldb", true)]
    [InlineData(@"C:\Users\user\AppData\Roaming\Session\GPUCache\data_0", false)]
    [InlineData(@"C:\Users\user\AppData\Roaming\Session\config.json", true)]
    [InlineData(@"C:\Users\user\AppData\Roaming\Threema\GPUCache\data_0", false)]
    [InlineData(@"C:\Users\user\AppData\Roaming\Threema\threema.db", true)]
    [InlineData(@"C:\Users\user\AppData\Roaming\Wire\GPUCache\data_0", false)]
    [InlineData(@"C:\Users\user\AppData\Roaming\Wire\key_data", true)]
    [InlineData(@"C:\Users\user\AppData\Local\Keybase\cache\file.tmp", false)]
    [InlineData(@"C:\Users\user\AppData\Local\Keybase\keybase.leveldb\000001.ldb", true)]
    [InlineData(@"C:\Users\user\AppData\Local\Cisco-Spark\temp\call.tmp", false)]
    [InlineData(@"C:\Users\user\AppData\Local\Cisco-Spark\database.sqlite", true)]
    [InlineData(@"C:\Users\user\AppData\Local\Packages\Microsoft.SkypeApp_kzf8qxf38zg5c\LocalState\live_session.db", true)]
    [InlineData(@"C:\Users\user\AppData\Local\Packages\Facebook.Messenger_8xx8rvfyw5nnt\LocalCache\auth.json", true)]
    [InlineData(@"C:\Users\user\AppData\Roaming\discord\Local State", true)]
    [InlineData(@"C:\Users\user\AppData\Roaming\Slack\Local State", true)]
    public void CleanerService_IsProtectedSessionOrCredentialFile_AccuratelyDifferentiatesSessionsFromCaches(string path, bool expectedProtected)
    {
        bool actual = CleanerService.IsProtectedSessionOrCredentialFile(path);
        Assert.Equal(expectedProtected, actual);
    }

    [Fact]
    public void AiFileSafetyService_ClassifiesLoginSessionsAndTokensAsHighRiskKeep()
    {
        var whatsAppLoginFile = @"C:\Users\user\AppData\Local\Packages\5319275A.WhatsAppDesktop_cv1g1gvanyjgm\LocalCache\EBWebView\Default\Login Data";
        var result1 = AiFileSafetyService.AnalyzeFile(whatsAppLoginFile, "Login Data", "Store Apps", 524288, DateTime.Now - TimeSpan.FromDays(2));
        Assert.Equal(AiSafetyTier.HighRiskKeep, result1.Tier);
        Assert.Equal(0, result1.SafetyScore);
        Assert.False(result1.IsSafeToAutoClean);

        var telegramKeyFile = @"C:\Users\user\AppData\Local\Packages\TelegramMessengerLLP.TelegramDesktop_t4vj0pshhgkwm\LocalCache\Roaming\Telegram Desktop UWP\tdata\D877F783D5D3EF8C0";
        var result2 = AiFileSafetyService.AnalyzeFile(telegramKeyFile, "D877F783D5D3EF8C0", "Store Apps", 4096, DateTime.Now - TimeSpan.FromDays(5));
        Assert.Equal(AiSafetyTier.HighRiskKeep, result2.Tier);
        Assert.Equal(0, result2.SafetyScore);
        Assert.False(result2.IsSafeToAutoClean);

        var indexedDbFile = @"C:\Users\user\AppData\Local\Packages\5319275A.WhatsAppDesktop_cv1g1gvanyjgm\LocalCache\EBWebView\Default\IndexedDB\https_web.whatsapp.com_0.indexeddb.leveldb\000005.ldb";
        var result3 = AiFileSafetyService.AnalyzeFile(indexedDbFile, "000005.ldb", "Store Apps", 1048576, DateTime.Now - TimeSpan.FromDays(1));
        Assert.Equal(AiSafetyTier.HighRiskKeep, result3.Tier);
        Assert.Equal(0, result3.SafetyScore);
        Assert.False(result3.IsSafeToAutoClean);
    }

    [Fact]
    public void CacheResolvers_AllReturnValidConfiguredTargetLists()
    {
        var storeAppDirs = WinTempCleaner.Services.Providers.CacheResolvers.StoreAppCacheResolver.Resolve();
        Assert.NotNull(storeAppDirs);

        var msgAppDirs = WinTempCleaner.Services.Providers.CacheResolvers.MessagingAppCacheResolver.Resolve();
        Assert.NotNull(msgAppDirs);
        Assert.True(msgAppDirs.Count >= 10);

        var browserDirs = WinTempCleaner.Services.Providers.CacheResolvers.BrowserCacheResolver.Resolve();
        Assert.NotNull(browserDirs);

        var devDirs = WinTempCleaner.Services.Providers.CacheResolvers.DevPackageCacheResolver.Resolve();
        Assert.NotNull(devDirs);
        Assert.True(devDirs.Count >= 10);

        var upgradeDirs = WinTempCleaner.Services.Providers.CacheResolvers.SystemCacheResolver.ResolveUpgradeLeftovers();
        Assert.NotNull(upgradeDirs);
        Assert.True(upgradeDirs.Count >= 5);

        var componentDirs = WinTempCleaner.Services.Providers.CacheResolvers.SystemCacheResolver.ResolveComponentCaches();
        Assert.NotNull(componentDirs);
        Assert.True(componentDirs.Count >= 5);

        var appCacheDirs = WinTempCleaner.Services.Providers.CacheResolvers.SystemCacheResolver.ResolveAppCacheDirectories();
        Assert.NotNull(appCacheDirs);
        Assert.True(appCacheDirs.Count >= 15);
    }

    [Fact]
    public void SmartClean_TargetsOnly100PercentSafeCaches()
    {
        var allTargets = CleanerService.GetDefaultTargets();
        var smartTargets = allTargets
            .Where(t => t.SafetyBadge.Contains("100% Safe") && !t.IsOrphanedAppFolder)
            .ToList();

        Assert.NotEmpty(smartTargets);
        Assert.All(smartTargets, t =>
        {
            Assert.Contains("100% Safe", t.SafetyBadge);
            Assert.False(t.IsOrphanedAppFolder);
        });
    }

    [Fact]
    public void SettingsService_NewFeatures_DefaultsAndToggleBehavior()
    {
        var settings = SettingsService.Current;
        Assert.NotNull(settings);

        // Low disk alert defaults
        Assert.True(settings.LowDiskAlertEnabled);
        Assert.Equal(10, settings.LowDiskAlertThresholdGb);

        // SendToRecycleBin defaults to false, can be toggled
        bool initial = settings.SendToRecycleBin;
        settings.SendToRecycleBin = !initial;
        Assert.Equal(!initial, settings.SendToRecycleBin);
        settings.SendToRecycleBin = initial; // revert
    }

    [Fact]
    public void TrayService_CheckLowDiskSpaceAndNotify_DoesNotThrowWhenUninitialized()
    {
        var telemetry = new DriveTelemetryInfo
        {
            DriveLetter = "C:",
            VolumeLabel = "Test Disk",
            TotalBytes = 100L * 1024 * 1024 * 1024,
            FreeBytes = 5L * 1024 * 1024 * 1024 // 5 GB < 15 GB threshold
        };

        // Should safely execute without throwing even when UI window is not present
        var ex = Record.Exception(() => TrayService.CheckLowDiskSpaceAndNotify(telemetry));
        Assert.Null(ex);
    }
}




