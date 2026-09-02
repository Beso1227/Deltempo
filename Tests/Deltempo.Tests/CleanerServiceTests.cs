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
        catch { }
    }

    [Fact]
    public void GetDefaultTargets_ReturnsAll21StandardCategories()
    {
        // Act
        var targets = CleanerService.GetDefaultTargets();

        // Assert
        Assert.NotNull(targets);
        Assert.True(targets.Count >= 21, $"Expected at least 21 targets, but found {targets.Count}");
        Assert.Contains(targets, t => t.Id == "DeviceDriverPackages");
        Assert.Contains(targets, t => t.Id == "DefenderAntivirus");
        Assert.Contains(targets, t => t.Id == "WinSystemLogs");
        Assert.Contains(targets, t => t.Id == "TemporaryInternetFiles");
        Assert.Contains(targets, t => t.Id == "SystemUsageTraces");
        Assert.Contains(targets, t => t.Id == "RecycleBin");
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
}
