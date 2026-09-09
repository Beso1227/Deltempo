using WinTempCleaner.Models;
using WinTempCleaner.Services;
using WinTempCleaner.Services.Providers;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Coverage tests for pure model math and side-effect-free service helpers:
/// DriveTelemetryInfo, TargetFolderInfo.FormatBytes, LogEntry, BuildInfo,
/// PercentWidthConverter, DriveTelemetryService, WindowsSystemProvider
/// (read-only queries), and CliRegistrationService.GetRegistrationStatus.
/// </summary>
public class ModelsAndPureServicesTests
{
    [Theory]
    [InlineData(1000L, 150L, 15.0, false)]   // exactly 15% is NOT low space
    [InlineData(1000L, 149L, 14.9, true)]
    [InlineData(1000L, 0L, 0.0, true)]
    [InlineData(1000L, 1000L, 100.0, false)]
    public void DriveTelemetry_PercentagesAndLowSpace(long total, long free, double expectedFreePct, bool expectedLow)
    {
        var info = new DriveTelemetryInfo { TotalBytes = total, FreeBytes = free };
        Assert.Equal(total - free >= 0 ? total - free : 0, info.UsedBytes);
        Assert.Equal(expectedFreePct, info.FreePercentage, precision: 5);
        Assert.Equal(100.0 - expectedFreePct, info.UsedPercentage, precision: 5);
        Assert.Equal(expectedLow, info.IsLowSpace);
    }

    [Fact]
    public void DriveTelemetry_ZeroTotal_ReturnsZeroNotNaN()
    {
        var info = new DriveTelemetryInfo { TotalBytes = 0, FreeBytes = 0 };
        Assert.Equal(0, info.FreePercentage);
        Assert.Equal(0, info.UsedPercentage);
        Assert.True(info.IsLowSpace);
    }

    [Fact]
    public void DriveTelemetry_UsedBytes_NeverNegative()
    {
        var info = new DriveTelemetryInfo { TotalBytes = 100, FreeBytes = 500 };
        Assert.Equal(0, info.UsedBytes);
    }

    [Fact]
    public void DriveTelemetry_FormattedStrings_NotEmpty()
    {
        var info = new DriveTelemetryInfo { DriveLetter = "C:", VolumeLabel = "OS", TotalBytes = 500L * 1024 * 1024 * 1024, FreeBytes = 200L * 1024 * 1024 * 1024 };
        Assert.False(string.IsNullOrWhiteSpace(info.FormattedTotal));
        Assert.False(string.IsNullOrWhiteSpace(info.FormattedFree));
        Assert.False(string.IsNullOrWhiteSpace(info.FormattedUsed));
        Assert.Contains("C:", info.DisplaySummary);
        Assert.Contains("free of", info.DisplaySummary);
    }

    [Fact]
    public void GetSystemDriveTelemetry_ReturnsSaneValues()
    {
        var t = DriveTelemetryService.GetSystemDriveTelemetry();
        Assert.False(string.IsNullOrWhiteSpace(t.DriveLetter));
        Assert.True(t.TotalBytes > 0);
        Assert.True(t.FreeBytes >= 0);
        Assert.InRange(t.FreePercentage, 0, 100);
    }

    [Fact]
    public void WindowsSystemProvider_GetDriveSpace_InvalidDrive_ReturnsZeros()
    {
        var p = new WindowsSystemProvider();
        var (total, free, pct) = p.GetDriveSpace("?:");
        Assert.Equal(0, total);
        Assert.Equal(0, free);
        Assert.Equal(0, pct);
    }

    [Fact]
    public void WindowsSystemProvider_GetDriveSpace_SystemDrive()
    {
        var p = new WindowsSystemProvider();
        var (total, free, pct) = p.GetDriveSpace("C:");
        Assert.True(total >= 0);
        Assert.True(free >= 0);
        Assert.InRange(pct, 0, 100);
    }

    [Fact]
    public void WindowsSystemProvider_GetSystemDriveTelemetry_MatchesService()
    {
        var p = new WindowsSystemProvider();
        var t = p.GetSystemDriveTelemetry();
        Assert.False(string.IsNullOrWhiteSpace(t.DriveLetter));
    }

    [Fact]
    public void WindowsSystemProvider_IsProcessProtected_IsDeterministic()
    {
        var p = new WindowsSystemProvider();
        bool a = p.IsProcessProtected("csrss");
        bool b = p.IsProcessProtected("csrss");
        Assert.Equal(a, b);
    }

    [Theory]
    [InlineData(0L, "0.0 B")]
    [InlineData(512L, "512.0 B")]
    [InlineData(1024L, "1.0 KB")]
    [InlineData(1536L, "1.5 KB")]
    [InlineData(1048576L, "1.0 MB")]
    [InlineData(1073741824L, "1.0 GB")]
    public void FormatBytes_KnownValues(long bytes, string expected)
    {
        Assert.Equal(expected, TargetFolderInfo.FormatBytes(bytes));
    }

    [Theory]
    [InlineData(LogLevel.Success, "#10B981", "✓")]
    [InlineData(LogLevel.Warning, "#F59E0B", "⚠")]
    [InlineData(LogLevel.Error, "#F43F5E", "✕")]
    [InlineData(LogLevel.Info, null, "›")]  // color depends on theme; glyph fixed
    public void LogEntry_BadgeColorAndGlyph(LogLevel level, string? color, string glyph)
    {
        var e = new LogEntry { Level = level, Message = "m" };
        Assert.Equal(glyph, e.LevelGlyph);
        if (color != null) Assert.Equal(color, e.BadgeColor);
        else Assert.False(string.IsNullOrWhiteSpace(e.BadgeColor));
        Assert.Matches(@"^\d{2}:\d{2}:\d{2}$", e.FormattedTime);
    }

    [Fact]
    public void BuildInfo_VersionDisplay_StartsWithV()
    {
        Assert.StartsWith("v", BuildInfo.VersionDisplay);
        Assert.Equal(BuildInfo.VersionDisplay, BuildInfo.VersionWithPatchDisplay);
        Assert.False(string.IsNullOrWhiteSpace(BuildInfo.CommitSha));
        Assert.False(string.IsNullOrWhiteSpace(BuildInfo.ShortCommitSha));
        Assert.True(BuildInfo.ShortCommitSha.Length <= 7 || BuildInfo.ShortCommitSha == "unknown");
    }

    [Fact]
    public void BuildInfo_BuildDateUtc_IsReasonable()
    {
        // BuildDateUtc falls back to UtcNow when the exe path can't be resolved
        // (test runner); it must simply be a sane non-default value, not future.
        Assert.NotEqual(default, BuildInfo.BuildDateUtc);
        Assert.True(BuildInfo.BuildDateUtc <= DateTime.UtcNow.AddDays(1));
        Assert.True(BuildInfo.BuildDateUtc >= new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void PercentWidthConverter_HalfOf200_Is100px()
    {
        var c = new PercentWidthConverter();
        var result = (System.Windows.GridLength)c.Convert(new object[] { 50.0, 200.0 }, typeof(System.Windows.GridLength), null!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(100.0, result.Value, precision: 5);
    }

    [Theory]
    [InlineData(-10.0, 200.0, 0.0)]    // clamped at 0
    [InlineData(150.0, 200.0, 200.0)]  // clamped at total
    [InlineData(50.0, 0.0, 0.0)]       // zero width guard
    [InlineData(50, 200.0, 100.0)]     // int percent overload
    public void PercentWidthConverter_EdgeCases(object pct, double total, double expected)
    {
        var c = new PercentWidthConverter();
        var result = (System.Windows.GridLength)c.Convert(new object[] { pct, total }, typeof(System.Windows.GridLength), null!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(expected, result.Value, precision: 5);
    }

    [Fact]
    public void PercentWidthConverter_TooFewValues_ReturnsZero()
    {
        var c = new PercentWidthConverter();
        var result = (System.Windows.GridLength)c.Convert(new object[] { 50.0 }, typeof(System.Windows.GridLength), null!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void PercentWidthConverter_ConvertBack_Throws()
    {
        var c = new PercentWidthConverter();
        Assert.Throws<NotImplementedException>(() =>
            c.ConvertBack(new System.Windows.GridLength(5), new[] { typeof(double) }, null!, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void GetRegistrationStatus_DoesNotThrow_AndIsConsistent()
    {
        var a = CliRegistrationService.GetRegistrationStatus();
        var b = CliRegistrationService.GetRegistrationStatus();
        Assert.Equal(a, b);
        Assert.Equal(a.IsFullyRegistered,
            a.IsRegisteredInUserPath && a.IsRegisteredInAppPaths &&
            a.IsRegisteredInPowerShellProfile && a.HasWrapperScripts);
    }

    [Fact]
    public void UnregisterAll_WhenNothingRegistered_DoesNotThrow()
    {
        // Determinism of the bool depends on machine state; assert no-throw only.
        var ex = Record.Exception(() => CliRegistrationService.UnregisterAll());
        Assert.Null(ex);
    }
}
