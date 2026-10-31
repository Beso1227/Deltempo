using System;
using System.IO;
using System.Linq;
using Deltempo.App.WinUI.ViewModels;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// The large-files + process-manager parity batch as shape guards plus pure data:
///
/// - WPF's category filter and sort combos, which the preview's Large Files
///   workspace never grew (the results re-filter and re-sort without re-scanning);
/// - the per-file "Ask AI" button (the command existed but nothing bound it, so
///   online per-file analysis was unreachable) and the batch "Analyze with AI"
///   fallback chain (selected, then never-verified, then everything);
/// - the treemap double-click reveal the WPF canvas had;
/// - the process manager's footer "Trim All Background Memory" press.
///
/// The AI cases are text-level only: the online path is never executed here, the
/// same rule every other suite case honours. The filter/sort semantics run on real
/// LargeFileInfo data through the pure FilterAndSort seam.
/// </summary>
public class WinUIParityBatchBTests
{
    private static string ReadWinUi(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot(), "src", "Deltempo.App.WinUI" }.Concat(parts).ToArray()));

    private static string ReadWpf(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot(), "src", "Deltempo.App.Wpf" }.Concat(parts).ToArray()));

    private static string RepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "deltempo.sln")))
        {
            dir = Directory.GetParent(dir)!.FullName;
        }

        return dir;
    }

    private static LargeFileInfo File1(string name, string category, long size, bool isSafe, int score = 0)
    {
        return new LargeFileInfo
        {
            FileName = name,
            FilePath = @"C:\x\" + name,
            Category = category,
            SizeBytes = size,
            IsSafe = isSafe,
            SafetyScore = score,
        };
    }

    // ------------------------------------------------------------------
    // 6 + 7. Category filter and sort combos.
    // ------------------------------------------------------------------

    [Fact]
    public void WpfLargeFiles_KeepsTheFilterAndSortCombos()
    {
        string wpf = ReadWpf("MainWindow.xaml");
        Assert.Contains("LargeFileCategoryComboBox", wpf);
        Assert.Contains("LargeFileSortComboBox", wpf);
        Assert.Contains("Game Asset / Pak", wpf);
        Assert.Contains("SIZE_DESC", wpf);
    }

    [Fact]
    public void WinUiLargeFiles_CarriesTheSameCombosWithTheSameTags()
    {
        string view = ReadWinUi("Views", "LargeFilesWorkspaceView.xaml");
        Assert.Contains("CategoryFilterCombo", view);
        Assert.Contains("SortModeCombo", view);

        // The shared tags both shells act on.
        foreach (string tag in new[] { "ALL", "SAFE", "RISK", "Installer / ISO", "Game Asset / Pak", "Video / Media", "Archive" })
        {
            Assert.Contains($"Tag=\"{tag}\"", view);
        }

        foreach (string sort in new[] { "SIZE_DESC", "SIZE_ASC", "SAFETY", "NAME" })
        {
            Assert.Contains($"Tag=\"{sort}\"", view);
        }

        string code = ReadWinUi("Views", "LargeFilesWorkspaceView.xaml.cs");
        Assert.Contains("OnCategoryFilterChanged", code);
        Assert.Contains("OnSortModeChanged", code);

        // And the VM semantics exist behind them.
        string vm = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Deltempo.App.WinUI", "ViewModels", "LargeFilesWorkspaceViewModel.cs"));
        Assert.Contains("FilterAndSort(", vm);
    }

    [Fact]
    public void LargeFiles_CategoryFilter_MatchesTheWpfSemantics()
    {
        List<LargeFileInfo> files =
        [
            File1("setup.iso", "Installer / ISO", 4_000_000_000, isSafe: true, score: 90),
            File1("game.pak", "Game Asset / Pak", 3_000_000_000, isSafe: false, score: 10),
            File1("movie.mp4", "Video / Media", 2_000_000_000, isSafe: false, score: 20),
            File1("cache.zip", "Archive", 500_000_000, isSafe: true, score: 80),
            File1("dump.ISO", "Archive", 100_000_000, isSafe: true, score: 70),
        ];

        // SAFE: only the safe verdicts. RISK: only the unsafe ones.
        Assert.Equal(3, LargeFilesWorkspaceViewModel.FilterAndSort(files, "SAFE", "SIZE_DESC").Count);
        Assert.Equal(2, LargeFilesWorkspaceViewModel.FilterAndSort(files, "RISK", "SIZE_DESC").Count);

        // Category names compare OrdinalIgnoreCase, exactly like WPF's filter.
        Assert.Single(LargeFilesWorkspaceViewModel.FilterAndSort(files, "Installer / ISO", "SIZE_DESC"));
        List<LargeFileInfo> archives = LargeFilesWorkspaceViewModel.FilterAndSort(files, "archive", "SIZE_DESC");
        Assert.Equal(2, archives.Count);
        Assert.All(archives, f => Assert.Equal("Archive", f.Category, ignoreCase: true));

        // "ALL" returns every row.
        Assert.Equal(5, LargeFilesWorkspaceViewModel.FilterAndSort(files, "ALL", "SIZE_DESC").Count);
    }

    [Fact]
    public void LargeFiles_SortModes_MatchTheWpfOrderings()
    {
        List<LargeFileInfo> files =
        [
            File1("b.bin", "Other", size: 1_000, isSafe: true, score: 50),
            File1("a.bin", "Other", size: 3_000, isSafe: true, score: 50),
            File1("c.bin", "Other", size: 3_000, isSafe: true, score: 90),
        ];

        // Largest first (the default), smallest first.
        Assert.Equal("a.bin", LargeFilesWorkspaceViewModel.FilterAndSort(files, "ALL", "SIZE_DESC")[0].FileName);
        Assert.Equal("b.bin", LargeFilesWorkspaceViewModel.FilterAndSort(files, "ALL", "SIZE_ASC")[0].FileName);

        // Safety first, size breaks ties descending (WPF's two SortDescriptions).
        List<LargeFileInfo> safety = LargeFilesWorkspaceViewModel.FilterAndSort(files, "ALL", "SAFETY");
        Assert.Equal("c.bin", safety[0].FileName);
        Assert.Equal("a.bin", safety[1].FileName);

        // File name ascending.
        Assert.Equal("a.bin", LargeFilesWorkspaceViewModel.FilterAndSort(files, "ALL", "NAME")[0].FileName);
    }

    // ------------------------------------------------------------------
    // 8 + 9. Per-file and batch online AI.
    // ------------------------------------------------------------------

    [Fact]
    public void WpfLargeFileRows_RaiseTheOnlineAskAiFlows()
    {
        string wpf = ReadWpf("MainWindow.Settings.cs");
        Assert.Contains("AnalyzeItemWithAiAsync", wpf);
        Assert.Contains("BatchAnalyzeWithAiAsync", wpf);
    }

    [Fact]
    public void WinUiLargeFileRow_BindsTheOnlineAskAiButton()
    {
        // The command existed; the deficiency was that no view element bound it.
        string view = ReadWinUi("Views", "LargeFilesWorkspaceView.xaml");
        Assert.Contains("Content=\"Ask AI\"", view);
        // The row button routes through a code-behind Click handler, because an
        // ElementName command binding inside a DataTemplate never fires in WinUI 3.
        Assert.Contains("OnRowAskAiClick", view);
        string code = ReadWinUi("Views", "LargeFilesWorkspaceView.xaml.cs");
        Assert.Contains("ViewModel.AnalyzeItemCommand.Execute", code);

        string vm = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Deltempo.App.WinUI", "ViewModels", "LargeFilesWorkspaceViewModel.cs"));
        Assert.Contains("AnalyzeItemWithAiAsync(item)", vm);
        Assert.Contains("AI analyzed", vm);
    }

    [Fact]
    public void WinUiLargeFilesFooter_OffersTheWpfBatchAskAi()
    {
        string view = ReadWinUi("Views", "LargeFilesWorkspaceView.xaml");
        Assert.Contains("BatchAnalyzeText", view);

        string vm = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Deltempo.App.WinUI", "ViewModels", "LargeFilesWorkspaceViewModel.cs"));

        // WPF's fallback chain (ticked, then never-verified, then everything) now
        // lives in Core as SelectAnalyzeTargets - shared with the post-scan
        // auto-query so one rule cannot drift - and the VM must still delegate to it.
        Assert.Contains("SelectAnalyzeTargets", vm);
        string core = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.Core", "Services", "LargeFileHunterService.cs"));
        Assert.Contains("f => f.IsSelected", core);
        Assert.Contains("!f.IsAiOnlineVerified", core);
        Assert.Contains("BatchAnalyzeWithAiAsync", vm);

        // The batch button warms its disabled state from a latch, never runs twice.
        Assert.Contains("IsBatchAnalyzing", vm);
    }

    // ------------------------------------------------------------------
    // 10. Treemap double-click reveal.
    // ------------------------------------------------------------------

    [Fact]
    public void WpfTreemap_RevealsInTheExplorerOnDoubleClick()
    {
        string wpf = ReadWpf("MainWindow.LargeFiles.cs");
        Assert.Contains("ev.ClickCount == 2", wpf);
        Assert.Contains("RevealFileInExplorer(item.FilePath)", wpf);
    }

    [Fact]
    public void WinUiTreemap_RevealsOnDoubleClickThroughTheRevealCommand()
    {
        string code = ReadWinUi("Views", "LargeFilesWorkspaceView.xaml.cs");
        Assert.Contains("border.DoubleTapped", code);
        Assert.Contains("ViewModel.RevealCommand.Execute(item)", code);
        Assert.Contains("Double-click to reveal in Explorer", code);
    }

    // ------------------------------------------------------------------
    // 11. Process Manager "Trim All".
    // ------------------------------------------------------------------

    [Fact]
    public void WpfProcessManager_OfferedTheFooterTrimAll()
    {
        string wpf = ReadWpf("MainWindow.xaml");
        Assert.Contains("TrimAllProcesses_Click", wpf);
        Assert.Contains("Trim All Background Memory", wpf);
    }

    [Fact]
    public void WinUiProcessManager_CarriesTheFooterTrimAll()
    {
        string view = ReadWinUi("Views", "ProcessWorkspaceView.xaml");
        Assert.Contains("TrimAllCommand", view);
        Assert.Contains("Trim All Background Memory", view);

        string vm = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Deltempo.App.WinUI", "ViewModels", "ProcessWorkspaceViewModel.cs"));
        Assert.Contains("MemoryOptimizerService.OptimizeRamAsync()", vm);
        Assert.Contains("await RefreshAsync();", vm);
    }
}
