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
}
