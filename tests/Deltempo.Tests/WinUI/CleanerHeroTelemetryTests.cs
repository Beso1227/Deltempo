using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Deltempo.App.WinUI.ViewModels;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// The cleaner workspace's hero drive-telemetry card and the hero action strip
/// state the WinUI shell added with the hero + hotkey batch.
///
/// The pull principle mirrors the WPF shell: the card renders read-only
/// DriveInfo plus the S.M.A.R.T. heuristics, and the session-freed card starts
/// hidden and prices nothing. These tests cover exactly the safe side: what the
/// user sees before any click, and that nothing at rest claims an action ran.
///
/// Deliberately not covered here: the read benchmark, Game Boost and the CTT
/// WinUtil launch, because they are mutating or external (an elevated process)
/// and the suite never touches them - see the destructive-path gate tests.
/// </summary>
public class CleanerHeroTelemetryTests
{
    private static CleanerWorkspaceViewModel NewViewModel()
    {
        // The ctor's own RefreshDriveTelemetry() is exercised, which is the real
        // path a live shell takes: no extra call inside the test.
        return new CleanerWorkspaceViewModel();
    }

    [Fact]
    public void DriveTelemetry_AtRest_RendersBadgeAndFreeReadout()
    {
        CleanerWorkspaceViewModel vm = NewViewModel();

        Assert.False(string.IsNullOrEmpty(vm.DriveLabel),
            "the drive card must never render an empty label");

        Assert.Matches(@"^\d+(\.\d+)?% Free$", vm.DriveFreeText);
        var usedMatch = Regex.Match(vm.DriveFreeText, @"^(\d+(\.\d+)?)%");
        double freePercent = double.Parse(usedMatch.Groups[1].Value);
        // Not equal to DriveUsedPercent - one is free %, one is used % - but
        // together they fill the bar.
        Assert.True(vm.DriveUsedPercent > 0 && vm.DriveUsedPercent < 100);
        Assert.Equal(100.0, freePercent + vm.DriveUsedPercent, precision: 1);

        Assert.False(string.IsNullOrEmpty(vm.DriveDetailsText),
            "details line must show the free-of-total breakdown");

        Assert.StartsWith("SSD ", vm.DriveHealthText);
        Assert.Matches(@"^SSD \d+(\.\d+)?%$", vm.DriveHealthText);

        Assert.Matches(@"^#[0-9A-Fa-f]{6}$", vm.DriveHealthColor);
    }

    [Fact]
    public void SessionCard_AtRest_IsHiddenAndPricesZero()
    {
        CleanerWorkspaceViewModel vm = NewViewModel();

        Assert.False(vm.HasSessionFreed, "no run has happened, so the session card must be hidden");
        Assert.Equal("0.0 B", vm.SessionFreedText);
    }

    [Fact]
    public void HeroActions_AtRest_BoostIsIdleAndNotBusy()
    {
        CleanerWorkspaceViewModel vm = NewViewModel();

        Assert.Equal(LocalizationService.Get("HeroGameBoost"), vm.GameBoostText);
        // Gray on purpose: WPF's XAML accidentally painted the icon green before
        // any refresh, and the port did not copy that defect.
        Assert.Equal("#94A3B8", vm.GameBoostIconColor);
        Assert.False(vm.IsGameBoostBusy);
        Assert.Equal("Speed Test", vm.BenchmarkText);
        Assert.False(vm.IsBenchmarking);
    }

    [Fact]
    public void Xaml_HeroActions_MirrorTheWpfOrder()
    {
        var xaml = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.App.WinUI", "Views", "CleanerWorkspaceView.xaml"));

        var order = new[] { "ScanCommand", "DeepCleanCommand", "AppDataReport", "OnGameBoostClick", "OnWinUtilClick", "CleanSelectedCommand" };
        var positions = order.Select(k => xaml.IndexOf(k, StringComparison.Ordinal)).ToList();

        Assert.False(positions.Any(p => p < 0),
            "the hero strip must offer all six actions, WPF parity");
        // Scan, Deep Clean, AppData Report, Game Boost, CTT WinUtil, Clean Selected.
        Assert.True(positions.SequenceEqual(positions.OrderBy(p => p).ToList()),
            "hero action strip order drifted from the WPF hero: " +
            $"{string.Join(',', order)} expected, got " +
            $"{string.Join(',', positions.Select((p, i) => (order[i], p)).OrderBy(t => t.p).Select(t => t.Item1))}");
    }

    /// <summary>
    /// Root of the repo the way the other parity tests find it: ascend until the sln appears.
    /// </summary>
    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 12; i++)
        {
            if (File.Exists(Path.Combine(dir, "deltempo.sln"))) return dir;
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }

        throw new InvalidOperationException("could not locate the repository root from " + AppContext.BaseDirectory);
    }
}
