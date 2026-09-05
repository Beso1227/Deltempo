using WinTempCleaner.Models;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

public class UpdateServiceTests
{
    [Fact]
    public void BuildInfo_ReturnsValidBaseVersionAndShortSha()
    {
        Assert.NotNull(BuildInfo.BaseVersion);
        Assert.False(string.IsNullOrWhiteSpace(BuildInfo.CommitSha));
        Assert.False(string.IsNullOrWhiteSpace(BuildInfo.ShortCommitSha));
        Assert.Contains(BuildInfo.BaseVersion.ToString(3), BuildInfo.VersionWithPatchDisplay);
    }

    [Fact]
    public void ParsePatchManifest_ParsesEmbeddedHtmlCommentCorrectly()
    {
        string body = @"
## ⚡ Deltempo Continuous Patch Build
Automatically compiled on push.

<!-- DELTEMPO_PATCH_MANIFEST
{
  ""commitSha"": ""abc1234567890abcdef"",
  ""shortSha"": ""abc1234"",
  ""commitMessage"": ""fix(ui): improved contrast in dark mode"",
  ""timestamp"": ""2026-09-05T12:00:00Z""
}
-->
";

        var manifest = UpdateService.ParsePatchManifest(body);
        Assert.NotNull(manifest);
        Assert.Equal("abc1234567890abcdef", manifest.CommitSha);
        Assert.Equal("fix(ui): improved contrast in dark mode", manifest.CommitMessage);
    }

    [Fact]
    public void ParsePatchManifest_ParsesJsonCodeBlockCorrectly()
    {
        string body = @"
```json:manifest
{
  ""commitSha"": ""deadbeef123456"",
  ""commitMessage"": ""refactor: zero-touch messaging protection"",
  ""fileSizeBytes"": 65000000
}
```
";

        var manifest = UpdateService.ParsePatchManifest(body);
        Assert.NotNull(manifest);
        Assert.Equal("deadbeef123456", manifest.CommitSha);
        Assert.Equal(65000000, manifest.FileSizeBytes);
    }

    [Fact]
    public void ParsePatchManifest_HandlesMalformedInputGracefully()
    {
        var manifest = UpdateService.ParsePatchManifest("This is just regular markdown without manifest.");
        Assert.Null(manifest);
    }

    [Fact]
    public void ReleaseInfo_ExposesShortCommitSha()
    {
        var release = new ReleaseInfo
        {
            IsPatchUpdate = true,
            CommitSha = "74f4446253be52008c637d40ba5ff8745e87f7f1"
        };

        Assert.Equal("74f4446", release.ShortCommitSha);
    }

    [Fact]
    public void SettingsService_DefaultUpdateChannel_IsPatch()
    {
        Assert.Equal("patch", SettingsService.Current.UpdateChannel);
    }

    [Fact]
    public async Task DownloadAndApplyUpdateAsync_RejectsUntrustedHosts()
    {
        var progress = new Progress<double>();
        
        // HTTP instead of HTTPS
        await Assert.ThrowsAsync<System.Security.SecurityException>(async () =>
        {
            await UpdateService.DownloadAndApplyUpdateAsync("http://github.com/Beso1227/Deltempo/releases/download/patch/Deltempo.exe", progress);
        });

        // Untrusted domain
        await Assert.ThrowsAsync<System.Security.SecurityException>(async () =>
        {
            await UpdateService.DownloadAndApplyUpdateAsync("https://evil-site.com/malware.exe", progress);
        });
    }

    [Fact]
    public void SettingsService_SanitizesAndClampsOutOfRangeValues()
    {
        int origInterval = SettingsService.Current.AutoCleanIntervalHours;
        int origDisk = SettingsService.Current.LowDiskAlertThresholdGb;

        try
        {
            SettingsService.Current.AutoCleanIntervalHours = -50;
            SettingsService.Current.LowDiskAlertThresholdGb = 99999;
            SettingsService.SaveSettings();
            SettingsService.LoadSettings();

            Assert.InRange(SettingsService.Current.AutoCleanIntervalHours, 1, 168);
            Assert.InRange(SettingsService.Current.LowDiskAlertThresholdGb, 1, 500);
        }
        finally
        {
            SettingsService.Current.AutoCleanIntervalHours = origInterval;
            SettingsService.Current.LowDiskAlertThresholdGb = origDisk;
            SettingsService.SaveSettings();
            SettingsService.LoadSettings();
        }
    }
}
