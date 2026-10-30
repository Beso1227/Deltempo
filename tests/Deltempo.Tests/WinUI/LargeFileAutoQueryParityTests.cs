using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Deltempo.App.WinUI.ViewModels;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// The post-scan auto-query batch: AppSettings.AutoQueryAiForLargeFiles was a dead
/// switch - WinUI's settings card promised "ask the provider about unknown large
/// files as soon as a scan finishes" and persisted the toggle, but no code in
/// either app acted on it, and WPF never even saved it from its own settings
/// modal. These tests pin the fix: the shared target rule in Core, the WinUI
/// auto-query seam (gated, latched, firing after a clean scan), and the XAML/WPF
/// shapes the rule rides on.
///
/// No test touches SettingsService: the gate is an injected seam so the
/// process-global settings store (and the real user's settings.json) is never
/// mutated by a run, and no test reaches the provider surface.
/// </summary>
public class LargeFileAutoQueryParityTests
{
    private static LargeFileInfo NewFile(
        string name,
        bool verified = false,
        bool ticked = false,
        string? basePath = null)
    {
        return new LargeFileInfo
        {
            FilePath = Path.Combine(basePath ?? @"C:\sample", name),
            FileName = name,
            SizeBytes = 60L * 1024 * 1024,
            IsSelected = ticked,
            IsAiOnlineVerified = verified,
        };
    }

    // ---------------------------------------------------------------------
    // Core target rule (LargeFileHunterService.SelectAnalyzeTargets).
    // ---------------------------------------------------------------------

    [Fact]
    public void SelectTargets_PrefersTheTickedFiles()
    {
        var a = NewFile("a.bin");
        var b = NewFile("b.iso", verified: true, ticked: true);
        var c = NewFile("c.exe");

        var targets = LargeFileHunterService.SelectAnalyzeTargets([a, b, c]);

        Assert.Equal([b], targets);
    }

    [Fact]
    public void SelectTargets_FallsBackToNeverVerifiedFiles()
    {
        var done = NewFile("done.bin", verified: true);
        var fresh = NewFile("fresh.dat");
        var untouched = NewFile("unknown.pak");

        var targets = LargeFileHunterService.SelectAnalyzeTargets([done, fresh, untouched]);

        Assert.Equal([fresh, untouched], targets);
    }

    [Fact]
    public void SelectTargets_ReAnalyzesEverythingWhenAllVerified()
    {
        var all = new List<LargeFileInfo>
        {
            NewFile("one.bin", verified: true),
            NewFile("two.bin", verified: true),
        };

        var targets = LargeFileHunterService.SelectAnalyzeTargets(all);

        Assert.Equal(all, targets);
    }

    [Fact]
    public void SelectTargets_EmptyAndNullInputsFailClean()
    {
        Assert.Empty(LargeFileHunterService.SelectAnalyzeTargets(
            Array.Empty<LargeFileInfo>()));
        Assert.Throws<ArgumentNullException>(
            () => LargeFileHunterService.SelectAnalyzeTargets(null!));
    }

    // ---------------------------------------------------------------------
    // WinUI auto-query seam: gate, latch and target rule.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task AutoQueryStandsDownWhenTheGateIsOff()
    {
        var vm = new LargeFilesWorkspaceViewModel();
        vm.LoadFrom([NewFile("big.dat")]);
        bool ran = false;
        vm.AutoQueryRunner = _ => { ran = true; return Task.CompletedTask; };
        vm.AutoQueryGate = () => false;

        await vm.AutoQueryAfterScanAsync();

        Assert.False(ran, "the auto-query must not run while the setting (or online AI) is off");
        Assert.False(vm.IsBatchAnalyzing);
    }

    [Fact]
    public async Task AutoQuery_UsesTheSharedTargetRuleAndHoldsTheLatchWhileRunning()
    {
        var vm = new LargeFilesWorkspaceViewModel();
        var verified = NewFile("already-checked.bin", verified: true);
        var fresh = NewFile("fresh-download.bin");
        var ticked = NewFile("ticked.iso", verified: true);
        ticked.IsSelected = true;
        vm.LoadFrom([verified, fresh, ticked]);

        IReadOnlyList<LargeFileInfo>? captured = null;
        bool latchObserved = false;
        vm.AutoQueryRunner = targets =>
        {
            captured = targets;
            // The manual "Analyze with AI" button must not double-drive while the
            // automatic pass is live: same latch, same CanExecute gate.
            latchObserved = vm.IsBatchAnalyzing && !vm.BatchAnalyzeCommand.CanExecute(null);
            return Task.CompletedTask;
        };
        vm.AutoQueryGate = () => true;

        await vm.AutoQueryAfterScanAsync();

        // Ticked wins over the never-verified rule, same as WPF's Ask-AI button.
        Assert.Equal(new[] { ticked }, captured);
        Assert.True(latchObserved, "the batch latch must hold for the length of the run");
        Assert.False(vm.IsBatchAnalyzing, "the latch must be released afterwards");
        Assert.True(vm.BatchAnalyzeCommand.CanExecute(null));
    }

    [Fact]
    public async Task AutoQueryStandsDownQuietlyWhenNothingNeedsAnalysis()
    {
        var vm = new LargeFilesWorkspaceViewModel();
        vm.LoadFrom([]);
        bool ran = false;
        vm.AutoQueryRunner = _ => { ran = true; return Task.CompletedTask; };
        vm.AutoQueryGate = () => true;

        await vm.AutoQueryAfterScanAsync();

        Assert.False(ran);
        Assert.False(vm.IsBatchAnalyzing);
    }

    // ---------------------------------------------------------------------
    // Wiring: the auto-query fires after a clean scan, driven by the same
    // command the user presses. Scoped to a throwaway one-file directory so
    // the scan is milliseconds, not minutes.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Scan_FiresTheAutoQueryAfterACleanScan_GatedOffItMustNot()
    {
        string scanDir = CreateTinyScanDirectory("autoquery-off");
        try
        {
            var vm = new LargeFilesWorkspaceViewModel
            {
                ScanScope = scanDir,
                MinSizeBytes = 1,
            };
            bool ran = false;
            vm.AutoQueryRunner = _ => { ran = true; return Task.CompletedTask; };
            vm.AutoQueryGate = () => false;
            vm.LogRequested += (_, _) => { };

            await vm.ScanCommand.ExecuteAsync(null);

            Assert.True(vm.HasItems, "the tiny directory must produce its one file");
            Assert.False(ran,
                "a scan must not auto-analyze while the auto-query gate is off");
        }
        finally
        {
            Directory.Delete(scanDir, recursive: true);
        }
    }

    [Fact]
    public async Task Scan_FiresTheAutoQueryAfterACleanScan_GatedOnItMust()
    {
        string scannedFile = CreateTinyScanDirectory("autoquery-on");
        try
        {
            var vm = new LargeFilesWorkspaceViewModel
            {
                ScanScope = scannedFile,
                MinSizeBytes = 1,
            };
            string? captured = null;
            vm.AutoQueryRunner = targets =>
            {
                captured = string.Join("|", targets.Select(t => t.FileName));
                return Task.CompletedTask;
            };
            vm.AutoQueryGate = () => true;
            vm.LogRequested += (_, _) => { };

            await vm.ScanCommand.ExecuteAsync(null);

            Assert.Single(vm.Items);
            string fileName = vm.Items[0].FileName;
            Assert.NotNull(captured);
            Assert.Contains(fileName, captured, StringComparison.Ordinal);
            // The runner override replaced the real batch core, so the status line
            // belongs to RunBatchAnalyzeCoreAsync, not to this wiring; the wiring
            // proof is that the scan's file came back through the auto-query. The
            // latch must be released when it returns.
            Assert.False(vm.IsBatchAnalyzing);
        }
        finally
        {
            Directory.Delete(scannedFile, recursive: true);
        }
    }

    [Fact]
    public async Task BatchAnalyzeOnAnEmptySetReportsInsteadOfRunning()
    {
        var vm = new LargeFilesWorkspaceViewModel();
        vm.LoadFrom([]);

        await vm.BatchAnalyzeAsync();

        Assert.Contains("No files need AI analysis", vm.StatusText, StringComparison.Ordinal);
    }

    /// <summary>
    /// A throwaway directory holding one small file: the scan then walks exactly
    /// one directory and returns one row, in milliseconds.
    /// </summary>
    private static string CreateTinyScanDirectory(string tag)
    {
        string dir = Path.Combine(
            Path.GetTempPath(), "Deltempo.Tests", tag, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "large-enough.dat"), "scan me");
        return dir;
    }

    // ---------------------------------------------------------------------
    // XAML shape guards, so the row capsule, the toggles and the WPF wiring
    // cannot quietly lose their markup.
    // ---------------------------------------------------------------------

    [Fact]
    public void XamlLargeFiles_CarriesTheAiDetailCapsule()
    {
        string xaml = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.App.WinUI", "Views", "LargeFilesWorkspaceView.xaml"));

        // WPF's row 3 capsule (AI Origin / Explanation / Recommendation), ported.
        Assert.Contains("IsAiOnlineVerified", xaml);
        Assert.Contains("IsAiAnalyzing", xaml);
        Assert.Contains("AiProviderLabel", xaml);
        Assert.Contains("Binding Origin", xaml);
        Assert.Contains("Binding Explanation", xaml);
        Assert.Contains("AiRecommendation", xaml);
        Assert.Contains("Analyzing", xaml);

        // The drive card's hand-off into this workspace (WPF's drive-card pill,
        // MainWindow.xaml:770): the pill lives in the cleaner view, the shell
        // navigates on its event.
        string cleaner = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.App.WinUI", "Views", "CleanerWorkspaceView.xaml"));
        string cleanerCode = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.App.WinUI", "Views", "CleanerWorkspaceView.xaml.cs"));
        string winuiShell = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.App.WinUI", "MainWindow.xaml.cs"));
        Assert.Contains("LargeFilesRequested", cleanerCode);
        Assert.Contains("OnOpenLargeFilesClick", cleaner);
        Assert.Contains("Scan Drive for AI-categorized Large Files", cleaner);
        Assert.Contains("CleanerWorkspacePanel.LargeFilesRequested", winuiShell);
        Assert.Contains("NavigateTo(LargeFilesSectionLabel)", winuiShell);
    }

    [Fact]
    public void ShellHeader_CarriesTheClickablePrivilegeCapsule()
    {
        string xaml = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.App.WinUI", "MainWindow.xaml"));
        string shell = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.App.WinUI", "MainWindow.xaml.cs"));

        // WPF's AdminElevationButton (MainWindow.xaml:355) ported: the capsule
        // hosts the same PrivilegeLabel and asks for elevation on click.
        Assert.Contains("PrivilegeCapsule", xaml);
        Assert.Contains("<TextBlock x:Name=\"PrivilegeLabel\"", xaml);
        Assert.Contains("Click=\"OnPrivilegeCapsuleClick\"", xaml);
        Assert.Contains("Status of Administrator Privileges (Click to Elevate)", xaml);
        Assert.Contains("OnPrivilegeCapsuleClick", shell);
        Assert.Contains("Elevate to Administrator", shell);
        Assert.Contains("ElevationService.RestartAsAdmin()", shell);
    }

    [Fact]
    public void WpfCounterparts_StillOwnTheSettingsAndAutoQuerySurfaces()
    {
        string wpf = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.App.Wpf", "MainWindow.xaml"));
        string settings = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.App.Wpf", "MainWindow.Settings.cs"));
        string largeFiles = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.App.Wpf", "MainWindow.LargeFiles.cs"));
        string shell = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.App.Wpf", "MainWindow.xaml.cs"));

        // The settings modal gets the toggle the WinUI card already had.
        Assert.Contains("SettingsAutoQueryAiCheckBox", wpf);
        Assert.Contains("Ask AI about unverified files after every scan", wpf);
        Assert.Contains("SettingsAutoQueryAiCheckBox.IsChecked", shell);

        // Persistence, the factory reset and the post-scan hook stay wired.
        Assert.Contains("s.AutoQueryAiForLargeFiles = SettingsAutoQueryAiCheckBox.IsChecked == true", settings);
        Assert.Contains("s.AutoQueryAiForLargeFiles = defaults.AutoQueryAiForLargeFiles", settings);
        Assert.Contains("SelectAnalyzeTargets", settings);
        Assert.Contains("AutoQueryLargeFilesAfterScanAsync", largeFiles);
        Assert.Contains("RunLargeFileAiBatchAsync", settings);
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
