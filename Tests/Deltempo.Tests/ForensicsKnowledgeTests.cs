using System;
using System.Collections.Generic;
using System.IO;
using WinTempCleaner.Models;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Safe offline coverage for FileForensics + Knowledge Report engines.
/// Uses synthetic profiles and temp files only — no network, no AI keys.
/// </summary>
public class ForensicsKnowledgeTests : IDisposable
{
    private readonly string _sandbox;

    public ForensicsKnowledgeTests()
    {
        _sandbox = Path.Combine(Path.GetTempPath(), "Deltempo_For_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_sandbox);
    }

    public void Dispose()
    {
        try { Directory.Delete(_sandbox, true); } catch { }
    }

    private static FileForensicProfile Profile(string name, string dir, string? eco, string? item, long size = 1024)
    {
        return new FileForensicProfile
        {
            FilePath = Path.Combine(dir, name),
            FileName = name,
            Extension = Path.GetExtension(name).ToLowerInvariant(),
            DirectoryPath = dir,
            SizeBytes = size,
            LastModified = DateTime.UtcNow,
            DetectedEcosystem = eco,
            EcosystemItemName = item
        };
    }

    [Fact]
    public void AnalyzeFile_NonExistentPath_ReturnsProfileWithMetadata()
    {
        string fake = Path.Combine(_sandbox, "ghost_big_file.tmp");
        var p = FileForensicsService.AnalyzeFile(fake);
        Assert.Equal("ghost_big_file.tmp", p.FileName);
        Assert.Equal(".tmp", p.Extension);
        Assert.Equal(0, p.SizeBytes);
        Assert.False(p.IsDigitallySigned);
    }

    [Fact]
    public void AnalyzeFile_RealFile_CapturesSize()
    {
        string f = Path.Combine(_sandbox, "real.bin");
        File.WriteAllBytes(f, new byte[4096]);
        var p = FileForensicsService.AnalyzeFile(f);
        Assert.Equal(4096, p.SizeBytes);
        Assert.False(string.IsNullOrWhiteSpace(p.DirectoryPath));
    }

    [Fact]
    public void AnalyzeFile_MagicHeader_PdfDetected()
    {
        string f = Path.Combine(_sandbox, "doc.dat");
        byte[] pdf = { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };
        File.WriteAllBytes(f, pdf);
        var p = FileForensicsService.AnalyzeFile(f);
        Assert.NotNull(p.DetectedMagicType);
        Assert.Contains("PDF", p.DetectedMagicType, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnalyzeFile_MagicHeader_ZipDetected()
    {
        string f = Path.Combine(_sandbox, "arc.dat");
        File.WriteAllBytes(f, new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00 });
        var p = FileForensicsService.AnalyzeFile(f);
        Assert.NotNull(p.DetectedMagicType);
    }

    [Fact]
    public void AnalyzeFile_EcosystemDetection_Paths()
    {
        var cases = new Dictionary<string, string>
        {
            [@"C:\Riot Games\VALORANT\live\VALORANT.exe"] = "Riot Games Installation",
            [@"C:\Program Files\Epic Games\Fortnite\FortniteGame\Binaries\game.exe"] = "Epic Games Store Title",
            [@"C:\kube\wsl\ext4.vhdx"] = "Virtual Machine Disk Image",
        };
        foreach (var kv in cases)
        {
            var p = FileForensicsService.AnalyzeFile(kv.Key);
            Assert.Equal(kv.Value, p.DetectedEcosystem);
        }
    }

    [Fact]
    public void AnalyzeFile_OllamaExtension_DetectsAiEcosystem()
    {
        var p = FileForensicsService.AnalyzeFile(@"D:\x\model.gguf");
        Assert.Equal("AI / Machine Learning Model Weights", p.DetectedEcosystem);
    }

    [Fact]
    public void AnonymizedSummary_FullProfile_ContainsFacts()
    {
        var p = Profile("ntdll.dll", @"C:\Windows\System32", null, null);
        p.CompanyName = "Microsoft";
        p.ProductName = "Windows";
        p.FileDescription = "NT Layer DLL";
        p.DetectedMagicType = "PE32+";
        string s = p.BuildAnonymizedSummary();
        Assert.Contains("ntdll.dll", s);
        Assert.Contains("Microsoft", s);
        Assert.Contains("Windows", s);
        Assert.Contains("Magic Bytes", s);
    }

    [Fact]
    public void AnonymizedSummary_EcosystemWithItem()
    {
        var p = Profile("blob", @"D:\m", "Steam Game Asset", "Game X");
        string s = p.BuildAnonymizedSummary();
        Assert.Contains("Steam Game Asset", s);
        Assert.Contains("Game X", s);
    }

    [Fact]
    public void AnonymizedSummary_MinimalProfile_NoThrow()
    {
        var p = new FileForensicProfile { FileName = "x.tmp", Extension = ".tmp" };
        string s = p.BuildAnonymizedSummary();
        Assert.Contains("x.tmp", s);
    }

    [Theory]
    [InlineData("game.pak", @"C:\SteamLibrary\steamapps\common\Doom\Content", "Steam Game Asset", "Doom")]
    [InlineData("game.xxx", @"C:\Program Files\Epic Games\Rocket\Binaries", "Epic Games Store Title", "Rocket League")]
    [InlineData("model.gguf", @"D:\AI\models", "AI / Machine Learning Model Weights", "llama3")]
    [InlineData("disk.vhdx", @"D:\VMs\Ubuntu", "Virtual Machine Disk Image", "Ubuntu")]
    public void KnowledgeReport_EcosystemBranches(string name, string dir, string eco, string item)
    {
        var r = OnlineFileIntelligenceService.GenerateBuiltInKnowledgeReport(Profile(name, dir, eco, item, 1L << 30));
        Assert.False(string.IsNullOrWhiteSpace(r.WhatIsIt));
        Assert.False(string.IsNullOrWhiteSpace(r.Recommendation));
        Assert.NotEqual(OnlineSafetyVerdict.ReviewRequired, r.Verdict);
    }

    [Fact]
    public void KnowledgeReport_WindowsSystem_Critical()
    {
        var r = OnlineFileIntelligenceService.GenerateBuiltInKnowledgeReport(
            Profile("ntdll.dll", @"C:\Windows\System32\", null, null));
        Assert.Equal(OnlineSafetyVerdict.CriticalDoNotDelete, r.Verdict);
        Assert.Equal(0, r.SafetyScore);
    }

    [Fact]
    public void KnowledgeReport_Iso_SafeIfUnused()
    {
        var r = OnlineFileIntelligenceService.GenerateBuiltInKnowledgeReport(
            Profile("win11.iso", @"D:\ISOs", null, null));
        Assert.Equal(OnlineSafetyVerdict.SafeIfUnused, r.Verdict);
        Assert.Equal(80, r.SafetyScore);
    }

    [Fact]
    public void KnowledgeReport_UnknownFile_ReviewRequired()
    {
        var r = OnlineFileIntelligenceService.GenerateBuiltInKnowledgeReport(
            Profile("mystery.xyz", @"D:\Random", null, null));
        Assert.Equal(OnlineSafetyVerdict.ReviewRequired, r.Verdict);
        Assert.Equal(50, r.SafetyScore);
    }

    [Fact]
    public void KnowledgeReport_AdobeCache_SafeToDelete()
    {
        var r = OnlineFileIntelligenceService.GenerateBuiltInKnowledgeReport(
            Profile("peak.pek", @"C:\Users\t\AppData\Roaming\Adobe\Common", "Adobe Premiere Cache", null));
        Assert.Equal(OnlineSafetyVerdict.SafeToDelete, r.Verdict);
        Assert.True(r.SafetyScore >= 90);
    }
}