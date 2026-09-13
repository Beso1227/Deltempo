using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WinTempCleaner.Models;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

public class StartupIntelligenceTests
{
    [Theory]
    [InlineData("Steam", StartupDisableVerdict.SafeToDisable)]
    [InlineData("Discord", StartupDisableVerdict.SafeToDisable)]
    [InlineData("Spotify", StartupDisableVerdict.SafeToDisable)]
    [InlineData("OneDrive", StartupDisableVerdict.Caution)]
    [InlineData("GoogleDrive", StartupDisableVerdict.Caution)]
    [InlineData("Dropbox", StartupDisableVerdict.Caution)]
    [InlineData("SecurityHealth", StartupDisableVerdict.DoNotDisable)]
    [InlineData("Realtek Audio Universal Service", StartupDisableVerdict.Caution)]
    [InlineData("WavesMaxxAudio", StartupDisableVerdict.DoNotDisable)]
    public void Catalog_RecognizesCoreStartupAppsAndAccurateVerdicts(string startupName, StartupDisableVerdict expectedVerdict)
    {
        bool found = StartupIntelligenceCatalog.TryGetKnownStartup(startupName, out var entry);

        Assert.True(found, $"Catalog should recognize startup: {startupName}");
        Assert.NotNull(entry);
        Assert.Equal(expectedVerdict, entry.Verdict);
        Assert.False(string.IsNullOrWhiteSpace(entry.Description));
        Assert.False(string.IsNullOrWhiteSpace(entry.DisableImpact));
        Assert.False(string.IsNullOrWhiteSpace(entry.Recommendation));
    }

    [Theory]
    [InlineData("AcmeUpdater.exe", "Acme Corp", "C:\\Program Files\\Acme\\updater.exe", StartupDisableVerdict.SafeToDisable)]
    [InlineData("UnknownDriveSync", "CloudSync LLC", "C:\\Users\\User\\AppData\\Local\\Sync.exe", StartupDisableVerdict.Caution)]
    [InlineData("MouseLightRGB", "Razer Inc", "C:\\Program Files\\Razer\\Synapse.exe", StartupDisableVerdict.Caution)]
    [InlineData("WindowsSecurityTray", "Microsoft Corporation", "C:\\Windows\\System32\\SecurityHealthSystray.exe", StartupDisableVerdict.DoNotDisable)]
    [InlineData("GameOverlayHook", "GameCorp", "C:\\Program Files\\Games\\overlay.exe", StartupDisableVerdict.SafeToDisable)]
    public void HeuristicClassifier_CategorizesUnknownStartupAccurately(string name, string publisher, string path, StartupDisableVerdict expectedVerdict)
    {
        var (desc, verdict, impact, rec) = StartupIntelligenceCatalog.ClassifyUnknownStartup(name, publisher, path);

        Assert.Equal(expectedVerdict, verdict);
        Assert.False(string.IsNullOrWhiteSpace(desc));
        Assert.False(string.IsNullOrWhiteSpace(impact));
        Assert.False(string.IsNullOrWhiteSpace(rec));
    }

    [Fact]
    public void StartupItem_ProvidesConsistentVerdictBadgesAndColors()
    {
        var safeItem = new StartupItem { DisableVerdict = StartupDisableVerdict.SafeToDisable };
        Assert.Equal("Safe to Disable", safeItem.DisableVerdictDisplay);
        Assert.Equal("#10B981", safeItem.DisableBadgeColor);
        Assert.Equal("#0D2818", safeItem.DisableBadgeBackground);

        var cautionItem = new StartupItem { DisableVerdict = StartupDisableVerdict.Caution };
        Assert.Equal("Caution / Sync", cautionItem.DisableVerdictDisplay);
        Assert.Equal("#F59E0B", cautionItem.DisableBadgeColor);
        Assert.Equal("#2A1E0D", cautionItem.DisableBadgeBackground);

        var keepItem = new StartupItem { DisableVerdict = StartupDisableVerdict.DoNotDisable };
        Assert.Equal("Essential / Keep", keepItem.DisableVerdictDisplay);
        Assert.Equal("#EF4444", keepItem.DisableBadgeColor);
        Assert.Equal("#2A0E0E", keepItem.DisableBadgeBackground);
    }

    [Fact]
    public async Task StartupIntelligenceService_GeneratesOfflineFallbackReportWhenNoApiKey()
    {
        bool origOnline = SettingsService.Current.EnableOnlineAiSafety;
        try
        {
            SettingsService.Update(s => s.EnableOnlineAiSafety = false);

            var item = new StartupItem
            {
                Name = "RandomAppLauncher",
                FriendlyName = "Random App Launcher",
                Publisher = "Random Vendor",
                ExePath = @"C:\Program Files\Random\launcher.exe",
                Command = @"C:\Program Files\Random\launcher.exe --start"
            };

            var report = await StartupIntelligenceService.AnalyzeStartupItemAsync(item, forceOnline: true);

            Assert.NotNull(report);
            Assert.False(string.IsNullOrWhiteSpace(report.Description));
            Assert.False(string.IsNullOrWhiteSpace(report.DisableImpact));
            Assert.False(string.IsNullOrWhiteSpace(report.Recommendation));
            Assert.True(report.ProviderUsed.Contains("Catalog") || report.ProviderUsed.Contains("Local"));
        }
        finally
        {
            SettingsService.Update(s => s.EnableOnlineAiSafety = origOnline);
        }
    }

    [Fact]
    public void PopulateStartupIntelligence_FillsKnownStartupData()
    {
        var steamItem = new StartupItem
        {
            Name = "Steam",
            FriendlyName = "Steam Client Bootstrapper",
            Publisher = "Valve Corporation",
            Command = "\"C:\\Program Files (x86)\\Steam\\steam.exe\" -silent"
        };

        StartupManagerService.PopulateStartupIntelligence(steamItem);

        Assert.Equal(StartupDisableVerdict.SafeToDisable, steamItem.DisableVerdict);
        Assert.False(string.IsNullOrWhiteSpace(steamItem.Description));
        Assert.False(string.IsNullOrWhiteSpace(steamItem.DisableImpact));
        Assert.Equal("Built-in Catalog", steamItem.AiProviderUsed);
    }
}
