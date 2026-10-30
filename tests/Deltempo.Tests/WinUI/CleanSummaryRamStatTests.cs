using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// The clean-summary celebration's fourth stat card. WPF's celebration carried
/// four stats (FILES DELETED / FOLDERS PURGED / RAM RECOVERED / TIME ELAPSED,
/// MainWindow.xaml:2238-2256); the WinUI port dropped the RAM card, so a deep
/// clean reported its standby flush only in a body line. These tests pin the
/// restored 4-card shape, WPF's placeholder rule on category cleans, and the
/// RamRecovered label existing in every language the shell ships.
/// </summary>
public class CleanSummaryRamStatTests
{
    [Fact]
    public void CleanSummaryView_CarriesTheWpfFourCardCelebrationGrid()
    {
        string xaml = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.App.WinUI", "Views", "CleanSummaryView.xaml"));

        // The four cards, WPF's order, and the RAM card keyed to the shared
        // localization table like its three neighbours.
        Assert.Contains("FilesDeleted", xaml);
        Assert.Contains("FoldersPurged", xaml);
        Assert.Contains("loc:Loc.Key=\"RamRecovered\"", xaml);
        Assert.Contains("TimeElapsed", xaml);
        Assert.Contains("RamStatText", xaml);
        // WPF's "-- MB" placeholder ships as the card's default copy.
        Assert.Contains("-- MB", xaml);
    }

    [Fact]
    public void LocalizationService_HasTheRamRecoveredLabelInEveryShippedLanguage()
    {
        // Read-only reflection over the translation table: no language switch,
        // so no process-global state is touched.
        var translations = typeof(LocalizationService).GetField(
            "Translations", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(translations);

        var table = Assert.IsType<Dictionary<string, Dictionary<string, string>>>(
            translations!.GetValue(null));

        Assert.Equal(new[] { "ar", "de", "en", "es", "fr" }, table.Keys.OrderBy(k => k));

        foreach ((string language, var dict) in table.OrderBy(k => k.Key))
        {
            Assert.True(dict.TryGetValue("RamRecovered", out string? label) && label.Length > 0,
                $"{language} is missing the RamRecovered label - the celebration " +
                "card would print the raw key instead.");
            Assert.NotEqual("RamRecovered", label);
        }

        // The public en-only check agrees, without touching CurrentLanguage.
        Assert.True(LocalizationService.ContainsKey("RamRecovered"));
    }

    /// <summary>Ascend until the solution file roots the repo.</summary>
    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 12; i++)
        {
            if (File.Exists(Path.Combine(dir, "deltempo.sln"))) return dir;
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }

        throw new InvalidOperationException("could not locate the repository root");
    }
}
