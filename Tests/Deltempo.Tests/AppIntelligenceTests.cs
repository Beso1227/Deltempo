using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WinTempCleaner.Models;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

[Collection("SettingsService")]
public class AppIntelligenceTests
{
    [Theory]
    [InlineData("Google Chrome", "Browsers")]
    [InlineData("Visual Studio Code", "Development")]
    [InlineData("Steam", "Gaming")]
    [InlineData("Microsoft Visual C++ 2015-2022 Redistributable (x64)", "Runtimes")]
    [InlineData("Spotify", "Media")]
    [InlineData("Discord", "Gaming")]
    [InlineData("Docker Desktop", "Development")]
    [InlineData("7-Zip 23.01 (x64)", "Utilities")]
    public void Catalog_RecognizesProminentWindowsApps(string appName, string expectedCategory)
    {
        bool found = AppDescriptionCatalog.TryGetKnownApp(appName, out var entry);

        Assert.True(found, $"Catalog should recognize: {appName}");
        Assert.NotNull(entry);
        Assert.Equal(expectedCategory, entry.Category);
        Assert.False(string.IsNullOrWhiteSpace(entry.Description));
    }

    [Fact]
    public void Catalog_FlagsCoreRuntimesAsHighRisk()
    {
        bool found = AppDescriptionCatalog.TryGetKnownApp("Microsoft Visual C++ 2019 Redistributable", out var entry);

        Assert.True(found);
        Assert.NotNull(entry);
        Assert.True(entry.IsCoreRuntime);
        Assert.Equal("High Risk", entry.SafetyVerdict);
    }

    [Theory]
    [InlineData("Apex Legends", "Electronic Arts", "D:\\Games\\Apex", "Gaming")]
    [InlineData("Cyberpunk 2077", "CD Projekt RED", "C:\\Games\\Cyberpunk", "Gaming")]
    [InlineData("Python 3.12.0 (64-bit)", "Python Software Foundation", "", "Development")]
    [InlineData("Brave Browser", "Brave Software Inc", "", "Browsers")]
    [InlineData("VLC Media Player 3.0", "VideoLAN", "", "Media & Audio")]
    [InlineData("NordVPN", "Nord Security", "", "Networking & Security")]
    [InlineData("LibreOffice 7.5", "The Document Foundation", "", "Productivity")]
    public void HeuristicClassifier_CategorizesUnknownSoftwareAccurately(string appName, string publisher, string installLocation, string expectedCategory)
    {
        var (description, category, _, _) = AppDescriptionCatalog.ClassifyUnknownApp(appName, publisher, installLocation);

        Assert.Equal(expectedCategory, category);
        Assert.False(string.IsNullOrWhiteSpace(description));
    }

    [Fact]
    public void InstalledAppItem_ProvidesCorrectBadgeColors()
    {
        var safeBrowser = new InstalledAppItem
        {
            DisplayName = "Test Browser",
            Category = "Browsers",
            SafetyVerdict = "Safe"
        };

        Assert.Equal("#06B6D4", safeBrowser.CategoryBadgeColor);
        Assert.Equal("#10B981", safeBrowser.SafetyBadgeColor);

        var runtimeApp = new InstalledAppItem
        {
            DisplayName = "Test Runtime",
            Category = "Runtimes",
            SafetyVerdict = "High Risk",
            IsCoreRuntime = true
        };

        Assert.Equal("#EF4444", runtimeApp.CategoryBadgeColor);
        Assert.Equal("#EF4444", runtimeApp.SafetyBadgeColor);
    }

    [Fact]
    public async Task AppIntelligenceService_GeneratesOfflineFallbackReportWhenNoApiKey()
    {
        bool origOnline = SettingsService.Current.EnableOnlineAiSafety;
        try
        {
            SettingsService.Update(s => s.EnableOnlineAiSafety = false);

            var app = new InstalledAppItem
            {
                DisplayName = "Sublime Text 4",
                Publisher = "Sublime HQ Pty Ltd",
                InstallLocation = "C:\\Program Files\\Sublime Text"
            };

            var report = await AppIntelligenceService.AnalyzeAppAsync(app, forceOnline: true);

            Assert.NotNull(report);
            Assert.Equal("Development", report.Category);
            Assert.Contains("editor", report.Description, StringComparison.OrdinalIgnoreCase);
            Assert.False(report.IsCoreRuntime);
        }
        finally
        {
            SettingsService.Update(s => s.EnableOnlineAiSafety = origOnline);
        }
    }

    [Theory]
    [InlineData("Browsers", "\uE774")]
    [InlineData("Gaming", "\uE7FC")]
    [InlineData("Development", "\uEBE8")]
    [InlineData("Runtimes", "\uE90F")]
    [InlineData("Utilities", "\uE713")]
    [InlineData("UnknownCategory", "\uE71D")]
    public void InstalledAppItem_ReturnsCorrectFallbackGlyphPerCategory(string category, string expectedGlyph)
    {
        var item = new InstalledAppItem
        {
            Category = category
        };

        Assert.Equal(expectedGlyph, item.FallbackGlyph);
        Assert.False(item.HasAppIcon);
    }

    [Fact]
    public void AppIconService_HandlesMissingOrInvalidPathsWithoutThrowing()
    {
        var icon1 = AppIconService.GetAppIcon("non_existent.exe,0", "", "DummyApp");
        var icon2 = AppIconService.GetAppIcon("", "C:\\NonExistentPath\\12345", "DummyApp");
        var icon3 = AppIconService.GetAppIcon("", "", "");

        Assert.Null(icon1);
        Assert.Null(icon2);
        Assert.Null(icon3);
    }

    /// <summary>
    /// Guards the real extraction path, not just the failure paths.
    ///
    /// <see cref="AppIconService"/> used to return a WPF ImageSource; it now
    /// returns PNG-encoded bytes so WinUI 3 can render the same icons. The
    /// existing test only asserted that bad paths return null, which means a
    /// completely broken encoder would have passed. This asserts that a real
    /// system binary yields bytes with a valid PNG signature.
    /// </summary>
    [Fact]
    public void AppIconService_ExtractsRealIconAsPngBytes()
    {
        string exe = Path.Combine(Environment.SystemDirectory, "notepad.exe");
        Assert.True(File.Exists(exe), $"Expected a known system binary at {exe}");

        byte[]? icon = AppIconService.GetAppIcon($"{exe},0", "", "Notepad");

        Assert.NotNull(icon);
        Assert.True(icon.Length > 0, "Extracted icon should not be empty.");

        // PNG signature: 89 50 4E 47 0D 0A 1A 0A
        byte[] expected = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        Assert.True(icon.Length >= expected.Length, "Icon bytes are too short to be a PNG.");

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.True(
                icon[i] == expected[i],
                $"PNG signature mismatch at byte {i}: expected 0x{expected[i]:X2}, got 0x{icon[i]:X2}.");
        }
    }

    /// <summary>
    /// The app item must report an icon only when it actually has usable bytes,
    /// not merely a non-null array.
    /// </summary>
    [Fact]
    public void InstalledAppItem_EmptyIconBytesDoNotCountAsAnIcon()
    {
        var item = new InstalledAppItem { AppIcon = [] };

        Assert.False(item.HasAppIcon);

        item.AppIcon = [0x89, 0x50];

        Assert.True(item.HasAppIcon);
    }
}
