using System;
using System.IO;
using System.Threading.Tasks;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

public class OnlineFileIntelligenceTests
{
    [Fact]
    public void FileForensics_SteamGamePath_DetectsSteamEcosystem()
    {
        string fakeSteamPath = @"C:\Program Files (x86)\Steam\steamapps\common\Baldurs Gate 3\Data\Patch4.pak";
        var profile = FileForensicsService.AnalyzeFile(fakeSteamPath);

        Assert.Equal("Patch4.pak", profile.FileName);
        Assert.Equal(".pak", profile.Extension);
        Assert.Equal("Steam Game Asset", profile.DetectedEcosystem);
        Assert.Equal("Baldurs Gate 3", profile.EcosystemItemName);

        var report = OnlineFileIntelligenceService.GenerateBuiltInKnowledgeReport(profile);
        Assert.Equal(OnlineSafetyVerdict.SafeIfUnused, report.Verdict);
        Assert.Contains("Baldurs Gate 3", report.Origin);
        Assert.Contains("re-download", report.ImpactIfDeleted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("uninstall", report.Recommendation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FileForensics_VirtualMachineDisk_IdentifiedAsCriticalDoNotDelete()
    {
        string fakeVmPath = @"D:\VirtualBox VMs\UbuntuServer\UbuntuServer.vdi";
        var profile = FileForensicsService.AnalyzeFile(fakeVmPath);

        Assert.Equal("Virtual Machine Disk Image", profile.DetectedEcosystem);
        Assert.Equal("Oracle VirtualBox VM", profile.EcosystemItemName);

        var report = OnlineFileIntelligenceService.GenerateBuiltInKnowledgeReport(profile);
        Assert.Equal(OnlineSafetyVerdict.CriticalDoNotDelete, report.Verdict);
        Assert.Contains("virtual machine", report.WhatIsIt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DO NOT DELETE", report.Recommendation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FileForensics_AiModelWeights_IdentifiedWithReDownloadOption()
    {
        string fakeModelPath = @"C:\Users\tester\.ollama\models\blobs\sha256-4c9b98665.gguf";
        var profile = FileForensicsService.AnalyzeFile(fakeModelPath);

        Assert.Equal("AI / Machine Learning Model Weights", profile.DetectedEcosystem);

        var report = OnlineFileIntelligenceService.GenerateBuiltInKnowledgeReport(profile);
        Assert.Equal(OnlineSafetyVerdict.SafeIfUnused, report.Verdict);
        Assert.Contains("Neural network", report.WhatIsIt, StringComparison.OrdinalIgnoreCase);
        Assert.True(report.SafetyScore >= 70);
    }

    [Fact]
    public void FileForensics_AdobeMediaCache_IdentifiedAsSafeToDelete()
    {
        string fakeAdobePath = @"C:\Users\tester\AppData\Roaming\Adobe\Common\Media Cache Files\Project1.cfa";
        var profile = FileForensicsService.AnalyzeFile(fakeAdobePath);

        Assert.Equal("Adobe Premiere / After Effects Media Cache", profile.DetectedEcosystem);

        var report = OnlineFileIntelligenceService.GenerateBuiltInKnowledgeReport(profile);
        Assert.Equal(OnlineSafetyVerdict.SafeToDelete, report.Verdict);
        Assert.Contains("Safe to delete", report.Recommendation, StringComparison.OrdinalIgnoreCase);
        Assert.True(report.SafetyScore >= 90);
    }

    [Fact]
    public void FileForensics_BuildAnonymizedSummary_HidesPersonalUsername()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string fakeUserFilePath = Path.Combine(userProfile, "Downloads", "big_test_file.iso");

        var profile = FileForensicsService.AnalyzeFile(fakeUserFilePath);
        string summary = profile.BuildAnonymizedSummary();

        Assert.DoesNotContain(userProfile, summary);
        Assert.Contains("%USERPROFILE%\\Downloads", summary);
        Assert.Contains("big_test_file.iso", summary);
    }

    [Fact]
    public void OnlineSafetyReport_ApplyBadgeColors_AssignsConsistentColorTokens()
    {
        var safeReport = new OnlineSafetyReport { Verdict = OnlineSafetyVerdict.SafeToDelete };
        safeReport.ApplyBadgeColors();
        Assert.Equal("#10B981", safeReport.BadgeColor);
        Assert.Equal("SAFE TO CLEAN", safeReport.VerdictDisplay);

        var criticalReport = new OnlineSafetyReport { Verdict = OnlineSafetyVerdict.CriticalDoNotDelete };
        criticalReport.ApplyBadgeColors();
        Assert.Equal("#EF4444", criticalReport.BadgeColor);
        Assert.Equal("DO NOT DELETE", criticalReport.VerdictDisplay);

        var unusedReport = new OnlineSafetyReport { Verdict = OnlineSafetyVerdict.SafeIfUnused };
        unusedReport.ApplyBadgeColors();
        Assert.Equal("#06B6D4", unusedReport.BadgeColor);
        Assert.Equal("SAFE IF UNUSED", unusedReport.VerdictDisplay);
    }

    [Fact]
    public void LargeFileInfo_ApplyOnlineReport_UpdatesPropertiesAndTriggersNotify()
    {
        var item = new LargeFileInfo
        {
            FilePath = @"C:\Games\Game1\bigfile.pak",
            FileName = "bigfile.pak",
            SizeBytes = 10L * 1024 * 1024 * 1024
        };

        var report = new OnlineSafetyReport
        {
            Verdict = OnlineSafetyVerdict.SafeIfUnused,
            SafetyScore = 40,
            Origin = "Steam Game",
            WhatIsIt = "Game textures",
            ImpactIfDeleted = "Re-download required",
            Recommendation = "Uninstall via Steam",
            ProviderUsed = "Google Gemini (gemini-2.0-flash)"
        };
        report.ApplyBadgeColors();

        item.ApplyOnlineReport(report);

        Assert.True(item.IsAiOnlineVerified);
        Assert.False(item.IsAiAnalyzing);
        Assert.Equal("SAFE IF UNUSED", item.AiVerdict);
        Assert.Equal(40, item.SafetyScore);
        Assert.Equal("Steam Game", item.AiOrigin);
        Assert.Equal("Uninstall via Steam", item.AiRecommendation);
        Assert.Equal("Google Gemini (gemini-2.0-flash)", item.AiProviderLabel);
    }

    [Fact]
    public async Task OnlineFileIntelligence_AnalyzeRealSystemFile_ReturnsValidReport()
    {
        string notepadPath = Path.Combine(Environment.SystemDirectory, "notepad.exe");
        if (!File.Exists(notepadPath)) return;

        var report = await OnlineFileIntelligenceService.AnalyzeFileAsync(notepadPath);

        Assert.NotNull(report);
        Assert.Equal("notepad.exe", report.FileName);
        Assert.True(report.SafetyScore < 50, "System32 executable should not be marked as safe to clean.");
    }
}
