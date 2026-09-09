using WinTempCleaner.Models;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

public class UpdateServiceTests
{
    [Fact]
    public void BuildInfo_ReturnsValidBaseVersion()
    {
        Assert.NotNull(BuildInfo.BaseVersion);
        Assert.False(string.IsNullOrWhiteSpace(BuildInfo.CommitSha));
        Assert.Contains(BuildInfo.BaseVersion.ToString(3), BuildInfo.VersionDisplay);
    }

    [Theory]
    [InlineData("v1.3.4", 1, 3, 4)]
    [InlineData("v1.4.0", 1, 4, 0)]
    [InlineData("2.0.1", 2, 0, 1)]
    [InlineData("v3.1", 3, 1, 0)]
    [InlineData("invalid", 1, 0, 0)]
    public void ParseReleaseVersion_ParsesTagCorrectly(string tag, int expectedMajor, int expectedMinor, int expectedBuild)
    {
        var ver = UpdateService.ParseReleaseVersion(tag);
        Assert.Equal(expectedMajor, ver.Major);
        Assert.Equal(expectedMinor, ver.Minor);
        Assert.Equal(expectedBuild, ver.Build);
    }

    [Fact]
    public void NormalizeVersion_HandlesNegativeParts()
    {
        var v = new Version(2, 1);
        var norm = UpdateService.NormalizeVersion(v);
        Assert.Equal(2, norm.Major);
        Assert.Equal(1, norm.Minor);
        Assert.Equal(0, norm.Build);
    }

    [Fact]
    public void ReleaseInfo_DefaultsAndInitializers()
    {
        var release = new ReleaseInfo
        {
            TagName = "v1.3.5",
            ReleaseName = "Deltempo v1.3.5",
            Body = "Performance optimizations",
            DownloadUrl = "https://github.com/Beso1227/Deltempo/releases/download/v1.3.5/Deltempo.exe",
            FileSizeBytes = 52000000,
            IsNewer = true,
            VersionString = "1.3.5",
            CheckSucceeded = true
        };

        Assert.Equal("v1.3.5", release.TagName);
        Assert.True(release.IsNewer);
        Assert.True(release.CheckSucceeded);
        Assert.Equal("1.3.5", release.VersionString);
    }

    [Fact]
    public async Task DownloadAndApplyUpdateAsync_RejectsUntrustedHosts()
    {
        var progress = new Progress<double>();

        // HTTP instead of HTTPS
        await Assert.ThrowsAsync<System.Security.SecurityException>(async () =>
        {
            await UpdateService.DownloadAndApplyUpdateAsync("http://github.com/Beso1227/Deltempo/releases/download/v1.3.5/Deltempo.exe", progress);
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

    [Fact]
    public void SettingsService_PersistsDismissedVersion()
    {
        string origDismissed = SettingsService.Current.DismissedVersion;

        try
        {
            SettingsService.Current.DismissedVersion = "1.3.5";
            SettingsService.SaveSettings();
            SettingsService.LoadSettings();

            Assert.Equal("1.3.5", SettingsService.Current.DismissedVersion);
        }
        finally
        {
            SettingsService.Current.DismissedVersion = origDismissed;
            SettingsService.SaveSettings();
        }
    }
}
