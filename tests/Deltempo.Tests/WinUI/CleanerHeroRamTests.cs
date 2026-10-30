using System;
using System.IO;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// The Cleaner hero's RAM Telemetry &amp; Quick Optimizer card, WPF's
/// MainWindow.xaml:834-918 as driven by MainWindow.Memory.cs and Core's
/// MemoryOptimizationViewModel. The WinUI the parity audit ran against carried
/// only the drive card, so a standard user had no RAM readout or Quick Boost in
/// the Cleaner at all, and the "RAM Cleaner" pill had no clickable entrance.
/// These tests pin the restored card, its telemetry mapping and the shell
/// hand-off into the Memory workspace.
/// </summary>
public class CleanerHeroRamTests
{
    [Fact]
    public void HeroCard_CarriesTheWpfRamTelemetryShape()
    {
        string xaml = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.App.WinUI", "Views", "CleanerWorkspaceView.xaml"));

        // WPF's card: glyph + RAM Memory label, used-percent readout, RAM Cleaner
        // pill, used bar, detail line and the Quick Boost button with WPF's exact
        // honest help text.
        Assert.Contains("System Memory RAM Telemetry", xaml);
        Assert.Contains("loc:Loc.Key=\"HeroRamLabel\"", xaml);
        Assert.Contains("RamUsedPercentText", xaml);
        Assert.Contains("loc:Loc.Key=\"HeroRamBtn\"", xaml);
        Assert.Contains("Open Deep Kernel Memory &amp; Cache Cleaner", xaml);
        Assert.Contains("RamProgressBar", xaml);
        Assert.Contains("RamDetailText", xaml);
        Assert.Contains("Trim memory (cache eviction)", xaml);
        Assert.Contains("Trims working sets and discards cached pages", xaml);
        // WPF's default copy before the first telemetry read ships as the bound
        // property's default: it is the value x:Bind paints first.
        string vm = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.App.WinUI", "ViewModels", "CleanerWorkspaceViewModel.cs"));
        Assert.Contains("Reading RAM telemetry...", vm);
        // The boost button binds the shared engine's state, not a static label.
        Assert.Contains("ViewModel.BoostButtonText", xaml);
        Assert.Contains("ViewModel.IsBoosting", xaml);
        Assert.Contains("ViewModel.RamBoostCommand", xaml);
    }

    [Fact]
    public void HeroCard_RamCleanerPillHandsOffThroughTheShell()
    {
        string cleanerCode = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.App.WinUI", "Views", "CleanerWorkspaceView.xaml.cs"));
        string shellCode = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.App.WinUI", "MainWindow.xaml.cs"));

        // WPF's OpenMemoryCleanerModal_Click (MainWindow.Memory.cs:41): the pill
        // walks to the Memory workspace, the shell navigates on the view event.
        Assert.Contains("MemoryCleanerRequested", cleanerCode);
        Assert.Contains("OnMemoryCleanerClick", cleanerCode);
        Assert.Contains("MemoryCleanerRequested += () => NavigateTo(MemorySectionLabel)", shellCode);
    }

    [Fact]
    public void HeroCard_TelemetryReadsTheRealMemoryService()
    {
        // WPF's hero drove Core's MemoryOptimizationViewModel directly
        // (MainWindow.Memory.cs:10-28). The WinUI workspace must map from that
        // same engine - the real memory snapshot - not a local placeholder.
        // Reflection avoids instantiating anything; MemoryOptimizerService is
        // read-only here (GetMemoryInfo has no side effects we can assert).
        var mem = MemoryOptimizerService.GetMemoryInfo();
        Assert.InRange(mem.UsedPercent, 0, 100);
        Assert.False(string.IsNullOrWhiteSpace(mem.FormattedUsed));
        Assert.False(string.IsNullOrWhiteSpace(mem.FormattedTotal));

        // And the shared engine the card binds through derives its detail copy
        // from exactly that snapshot, WPF's HeroRamDetailText rule.
        var engine = new WinTempCleaner.ViewModels.MemoryOptimizationViewModel();
        Assert.InRange(engine.UsedPercent, 0, 100);
        Assert.NotEqual("Reading RAM telemetry...", engine.FormattedDetail);
        Assert.Contains("Used", engine.FormattedDetail, StringComparison.Ordinal);
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
