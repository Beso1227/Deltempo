using System;
using System.IO;
using WinTempCleaner.Core.Safety;
using Xunit;

namespace Deltempo.Tests;

public class FileSafetyEngineTests
{
    [Fact]
    public void Analyze_NullOrEmptyPath_ReturnsUnknown()
    {
        var result = FileSafetyEngine.Analyze("");
        Assert.Equal(SafetyRiskTier.Unknown, result.Tier);
        Assert.Equal(0, result.SafetyScore);
    }

    [Theory]
    [InlineData(@"C:\Temp\..\Windows\System32\cmd.exe")]
    [InlineData(@"C:\Users\User\AppData\Local\Temp\..\..\..\Sensitive.txt")]
    public void Analyze_TraversalSequences_ReturnsProtected(string maliciousPath)
    {
        var result = FileSafetyEngine.Analyze(maliciousPath);
        Assert.Equal(SafetyRiskTier.Protected, result.Tier);
        Assert.Equal(0, result.SafetyScore);
        Assert.Contains("PROTECTED", result.Verdict);
    }

    [Fact]
    public void Analyze_OutsideAllowedRoot_ReturnsProtected()
    {
        string filePath = @"C:\Users\JohnDoe\AppData\Local\Temp\file.tmp";
        string allowedRoot = @"D:\AllowedSandbox";

        var result = FileSafetyEngine.Analyze(filePath, allowedRoot: allowedRoot);
        Assert.Equal(SafetyRiskTier.Protected, result.Tier);
        Assert.Equal(0, result.SafetyScore);
        Assert.Contains("Out of Scope", result.Verdict);
    }

    [Fact]
    public void Analyze_FileModifiedRecentlyWith24HourShield_ReturnsReviewRequired()
    {
        string filePath = @"C:\Users\JohnDoe\AppData\Local\Temp\active_session.tmp";
        var result = FileSafetyEngine.Analyze(
            filePath,
            fileName: "active_session.tmp",
            category: "Temp",
            sizeBytes: 1024,
            lastModified: DateTime.UtcNow.AddHours(-2), // 2 hours old
            apply24HourThreshold: true);

        Assert.Equal(SafetyRiskTier.ReviewRequired, result.Tier);
        Assert.True(result.SafetyScore < 50);
        Assert.Contains("Modified Recently", result.Verdict);
    }

    [Fact]
    public void Analyze_OlderInstallerInDownloads_ReturnsSafe()
    {
        string filePath = @"C:\Users\JohnDoe\Downloads\setup_bundle.exe";
        var result = FileSafetyEngine.Analyze(
            filePath,
            fileName: "setup_bundle.exe",
            category: "Installer",
            sizeBytes: 50 * 1024 * 1024,
            lastModified: DateTime.UtcNow.AddDays(-10)); // 10 days old

        Assert.Equal(SafetyRiskTier.Safe, result.Tier);
        Assert.True(result.SafetyScore >= 90);
        Assert.Contains("SAFE", result.Verdict);
    }

    [Fact]
    public void Analyze_UnknownFileInUnrecognizedFolder_FallsBackToPreserveUnknown()
    {
        string filePath = @"D:\CustomDirectory\RandomData.dat";
        var result = FileSafetyEngine.Analyze(
            filePath,
            fileName: "RandomData.dat",
            category: "General",
            sizeBytes: 4096,
            lastModified: DateTime.UtcNow.AddDays(-10));

        Assert.Equal(SafetyRiskTier.Unknown, result.Tier);
        Assert.Contains("Preserved", result.Verdict);
        Assert.Contains("Preserved by default", result.Explanation);
    }

    [Fact]
    public void Analyze_FileInDesignatedTempDirectory_ReturnsSafe()
    {
        string filePath = @"C:\Users\JohnDoe\AppData\Local\Temp\scoped_dir123\cache.dat";
        var result = FileSafetyEngine.Analyze(
            filePath,
            fileName: "cache.dat",
            category: "User Cache",
            sizeBytes: 8192,
            lastModified: DateTime.UtcNow.AddDays(-2),
            apply24HourThreshold: true);

        Assert.Equal(SafetyRiskTier.Safe, result.Tier);
        Assert.True(result.SafetyScore >= 90);
        Assert.Equal("DesignatedCacheFileRule", result.MatchedRule);
    }

    [Theory]
    [InlineData(@"C:\Users\JohnDoe\AppData\Local\Temp\scoped_dir123\install_log.txt", "install_log.txt")]
    [InlineData(@"C:\Users\JohnDoe\AppData\Local\Temp\scoped_dir123\setup_manifest.xml", "setup_manifest.xml")]
    [InlineData(@"C:\Users\JohnDoe\AppData\Local\Temp\scoped_dir123\state_dump.json", "state_dump.json")]
    [InlineData(@"C:\Users\JohnDoe\AppData\Local\Temp\scoped_dir123\temp_archive.cab", "temp_archive.cab")]
    [InlineData(@"C:\Windows\Temp\scoped_dir123\setup.ini", "setup.ini")]
    [InlineData(@"C:\Users\JohnDoe\AppData\Local\Temp\scoped_dir123\{3A75C0FA-4F1E-4B02-8692-06E5BEF6B750}", "{3A75C0FA-4F1E-4B02-8692-06E5BEF6B750}")]
    public void Analyze_StaleTempFilesWithCommonExtensions_ReturnsSafe(string filePath, string fileName)
    {
        var result = FileSafetyEngine.Analyze(
            filePath,
            fileName: fileName,
            category: "General",
            sizeBytes: 8192,
            lastModified: DateTime.UtcNow.AddDays(-3),
            apply24HourThreshold: true);

        Assert.Equal(SafetyRiskTier.Safe, result.Tier);
        Assert.True(result.SafetyScore >= 90);
        Assert.Equal("DesignatedCacheFileRule", result.MatchedRule);
    }

    [Fact]
    public void Analyze_WithMultipleAllowedRoots_AcceptsValidSubpath()
    {
        string filePath = @"D:\PackageCaches\npm\chunk.bin";
        var roots = new[] { @"C:\Temp", @"D:\PackageCaches\npm" };

        var result = FileSafetyEngine.Analyze(
            filePath,
            fileName: "chunk.bin",
            category: "Dev Cache",
            sizeBytes: 16384,
            lastModified: DateTime.UtcNow.AddDays(-5),
            allowedRoots: roots);

        Assert.Equal(SafetyRiskTier.Safe, result.Tier);
    }

    [Fact]
    public void Analyze_WithMultipleAllowedRoots_RejectsOutOfScope()
    {
        string filePath = @"D:\OtherFolder\Sensitive.doc";
        var roots = new[] { @"C:\Temp", @"D:\PackageCaches\npm" };

        var result = FileSafetyEngine.Analyze(
            filePath,
            fileName: "Sensitive.doc",
            category: "Dev Cache",
            sizeBytes: 16384,
            lastModified: DateTime.UtcNow.AddDays(-5),
            allowedRoots: roots);

        Assert.Equal(SafetyRiskTier.Protected, result.Tier);
        Assert.Contains("Out of Scope", result.Verdict);
    }

    [Theory]
    [InlineData(@"C:\Users\JohnDoe\AppData\Local\Microsoft\Windows\Explorer\thumbcache_256.db", "thumbcache_256.db")]
    [InlineData(@"C:\Users\JohnDoe\AppData\Local\Microsoft\Windows\Explorer\thumbcache_idx.db", "thumbcache_idx.db")]
    [InlineData(@"C:\Users\JohnDoe\AppData\Local\Microsoft\Windows\Explorer\iconcache_idx.db", "iconcache_idx.db")]
    public void Analyze_WindowsExplorerThumbcache_ReturnsSafe(string path, string fileName)
    {
        var result = FileSafetyEngine.Analyze(
            path,
            fileName: fileName,
            category: "Media Cache",
            sizeBytes: 1024 * 512,
            lastModified: DateTime.UtcNow.AddDays(-3));

        Assert.Equal(SafetyRiskTier.Safe, result.Tier);
        Assert.Equal("ExplorerThumbnailCacheRule", result.MatchedRule);
        Assert.Contains("Thumbnail", result.Verdict);
    }

    [Theory]
    [InlineData(@"C:\Program Files\NVIDIA Corporation\Installer2\Display.Driver\nvdisp.nvi")]
    [InlineData(@"C:\ProgramData\NVIDIA Corporation\Downloader\latest_driver.exe")]
    [InlineData(@"C:\Windows\System32\DriverStore\Temp\scratch.tmp")]
    public void FileSafetyEngine_DriverPackageStaging_ClassifiedAsSafe(string path)
    {
        var result = FileSafetyEngine.Analyze(
            path,
            fileName: Path.GetFileName(path),
            category: "System & Drivers",
            sizeBytes: 1024 * 1024,
            lastModified: DateTime.UtcNow.AddDays(-3),
            allowedRoot: Path.GetDirectoryName(path));

        Assert.Equal(SafetyRiskTier.Safe, result.Tier);
        Assert.StartsWith("SAFE (Hardware Driver Package Staging)", result.Verdict);
        Assert.Equal("DriverStagingPackageRule", result.MatchedRule);
    }
}

