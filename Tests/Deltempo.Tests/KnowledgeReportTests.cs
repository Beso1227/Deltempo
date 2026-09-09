using System.IO;
using WinTempCleaner.Models;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Round 2 coverage: pure branches of the knowledge-report engine
/// (offline, no deletes, no network).
/// </summary>
public class KnowledgeReportTests
{
    private static FileForensicProfile Profile(
        string fileName, string dir, string ecosystem = "", string item = "")
        => new()
        {
            FilePath = Path.Combine(dir, fileName),
            FileName = fileName,
            Extension = Path.GetExtension(fileName),
            SizeBytes = 10L * 1024 * 1024,
            LastModified = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DirectoryPath = dir,
            DetectedEcosystem = ecosystem,
            EcosystemItemName = item
        };

    [Theory]
    [InlineData("game.pak", @"C:\Program Files (x86)\Steam\steamapps\common\Hades II", "Steam Game Asset", "Hades II", "Steam", 40)]
    [InlineData("chunk.ucas", @"D:\Epic\EOSDK\Fortnite", "Epic Games Store Title", "Fortnite", "Epic", 40)]
    [InlineData("disk.vhdx", @"D:\VMs\Dev", "Virtual Machine Disk Image", "DevVM", "Virtualization", 10)]
    [InlineData("avd.img", @"C:\Users\t\.android\avd", "Android Developer SDK / Emulator", "", "Android", 65)]
    public void KnowledgeReport_EcosystemBranches(string file, string dir, string eco, string item, string originFrag, int score)
    {
        var r = OnlineFileIntelligenceService.GenerateBuiltInKnowledgeReport(Profile(file, dir, eco, item));
        Assert.Contains(originFrag, r.Origin);
        Assert.Equal(score, r.SafetyScore);
        Assert.False(string.IsNullOrWhiteSpace(r.WhatIsIt));
        Assert.False(string.IsNullOrWhiteSpace(r.Recommendation));
    }

    [Fact]
    public void KnowledgeReport_AiModel_SafeIfUnused()
    {
        var r = OnlineFileIntelligenceService.GenerateBuiltInKnowledgeReport(
            Profile("model.gguf", @"C:\Users\t\.ollama\models", "AI / Machine Learning Model Weights", "llama"));
        Assert.Equal(OnlineSafetyVerdict.SafeIfUnused, r.Verdict);
        Assert.Equal(75, r.SafetyScore);
    }

    [Fact]
    public void KnowledgeReport_WindowsSystem_Critical()
    {
        var r = OnlineFileIntelligenceService.GenerateBuiltInKnowledgeReport(
            Profile("ntdll.dll", @"C:\Windows\System32\", "", ""));
        Assert.Equal(OnlineSafetyVerdict.CriticalDoNotDelete, r.Verdict);
        Assert.Equal(0, r.SafetyScore);
    }

    [Fact]
    public void KnowledgeReport_Iso_SafeIfUnused()
    {
        var r = OnlineFileIntelligenceService.GenerateBuiltInKnowledgeReport(
            Profile("win11.iso", @"D:\ISOs", "", ""));
        Assert.Equal(OnlineSafetyVerdict.SafeIfUnused, r.Verdict);
        Assert.Equal(80, r.SafetyScore);
    }

    [Fact]
    public void KnowledgeReport_UnknownFile_ReviewRequired()
    {
        var r = OnlineFileIntelligenceService.GenerateBuiltInKnowledgeReport(
            Profile("mystery.xyz", @"D:\Random", "", ""));
        Assert.Equal(OnlineSafetyVerdict.ReviewRequired, r.Verdict);
        Assert.Equal(50, r.SafetyScore);
    }

    [Fact]
    public void KnowledgeReport_AdobeCache_SafeToDelete()
    {
        var r = OnlineFileIntelligenceService.GenerateBuiltInKnowledgeReport(
            Profile("peak.pek", @"C:\Users\t\AppData\Roaming\Adobe\Common", "Adobe Premiere Cache", ""));
        Assert.Equal(OnlineSafetyVerdict.SafeToDelete, r.Verdict);
        Assert.True(r.SafetyScore >= 90);
    }

    [Fact]
    public void KnowledgeCache_ClearAndCount_RoundTrip()
    {
        OnlineFileIntelligenceService.ClearCache();
        Assert.Equal(0, OnlineFileIntelligenceService.GetCacheCount());
    }
}
