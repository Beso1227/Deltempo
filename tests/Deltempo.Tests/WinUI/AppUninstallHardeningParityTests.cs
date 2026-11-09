using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Guards for the uninstaller-hardening round.
///
/// The App Uninstaller surface exists twice (WPF modal + WinUI workspace) and the
/// dangerous parts live in code-behind and in a ViewModel, so the safety contract
/// is pinned at the source level the same way the large-files repair round pins
/// its bindings. Every assertion here corresponds to a behaviour that was
/// previously missing and would silently regress without a test:
///
///  * nothing is deleted from the drawer without a confirmation
///  * the Force Root Clean prompt does not promise that clicking it purges
///  * a partial purge is reported with a warning cue, not a success cue
///  * a failed or cancelled uninstall does not fall through to "scan and purge"
///  * a restore point that could not be created is surfaced before the uninstall
///  * the purge button cannot be pressed again while the app is busy
/// </summary>
public class AppUninstallHardeningParityTests
{
    [Fact]
    public void WpfPurge_RequiresConfirmation_BeforeDeletingAnything()
    {
        string code = ReadRepoFile("src", "Deltempo.App.Wpf", "Views", "Modals", "AppUninstallModal.xaml.cs");

        Assert.Contains("Eradicate Root Traces", code, StringComparison.Ordinal);
        Assert.Contains("This action CANNOT be undone. Proceed?", code, StringComparison.Ordinal);

        // The confirmation must be evaluated before the purge call, not after it.
        int confirm = code.IndexOf("Eradicate Root Traces", StringComparison.Ordinal);
        int purge = code.IndexOf("RootLeftoverPurgeService.PurgeLeftoversAsync", StringComparison.Ordinal);
        Assert.True(confirm >= 0 && purge > confirm,
            "the purge confirmation must be raised before PurgeLeftoversAsync is called");
    }

    [Fact]
    public void WpfPurge_WarnsOnPartialFailure_AndRescansAfterwards()
    {
        string code = ReadRepoFile("src", "Deltempo.App.Wpf", "Views", "Modals", "AppUninstallModal.xaml.cs");

        // A success chime over items that survived would misreport the outcome.
        Assert.Contains("if (hadErrors) SoundService.PlayWarningSound();", code, StringComparison.Ordinal);

        // The drawer must show what actually survived the purge.
        Assert.Contains("await InspectAppTracesAsync(_currentInspectedApp);", code, StringComparison.Ordinal);
        Assert.Contains("Root Purge Completed With Issues", code, StringComparison.Ordinal);
        Assert.Contains("Some Items Scheduled for Reboot", code, StringComparison.Ordinal);
    }

    [Fact]
    public void WpfForceRootClean_DoesNotClaimItDeletes()
    {
        string code = ReadRepoFile("src", "Deltempo.App.Wpf", "Views", "Modals", "AppUninstallModal.xaml.cs");
        string xaml = ReadRepoFile("src", "Deltempo.App.Wpf", "Views", "Modals", "AppUninstallModal.xaml");

        Assert.DoesNotContain("directly purge", code, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("directly purge", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Nothing is deleted until you review", code, StringComparison.Ordinal);
        Assert.Contains("Nothing is deleted until you review", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void WpfUninstall_FailureDoesNotFallThroughToScanAndPurge()
    {
        string code = ReadRepoFile("src", "Deltempo.App.Wpf", "Views", "Modals", "AppUninstallModal.xaml.cs");

        Assert.Contains("Uninstall Did Not Complete", code, StringComparison.Ordinal);
        Assert.Contains("Restore Point Not Created", code, StringComparison.Ordinal);
        Assert.Contains("Continue with the uninstall without a restore point?", code, StringComparison.Ordinal);
    }

    [Fact]
    public void WpfPurgeButton_IsDisabledWhileBusy_AndStartsDisabled()
    {
        string xaml = ReadRepoFile("src", "Deltempo.App.Wpf", "Views", "Modals", "AppUninstallModal.xaml");
        string code = ReadRepoFile("src", "Deltempo.App.Wpf", "Views", "Modals", "AppUninstallModal.xaml.cs");

        var button = Regex.Match(xaml, "<Button x:Name=\"PurgeRootTracesBtn\"[\\s\\S]{0,600}?>");
        Assert.True(button.Success, "PurgeRootTracesBtn must exist in the WPF drawer footer");
        Assert.Contains("IsEnabled=\"False\"", button.Value, StringComparison.Ordinal);

        Assert.Contains("PurgeRootTracesBtn.IsEnabled = !busy && selectedCount > 0;", code, StringComparison.Ordinal);
        Assert.Contains("PurgeRootTracesBtn.IsEnabled = !_isBusy && selectedCount > 0;", code, StringComparison.Ordinal);
    }

    [Fact]
    public void WinUiPurge_ReportsPartialFailure_WithANoticeAndAWarningCue()
    {
        string vm = ReadRepoFile("src", "Deltempo.App.WinUI", "ViewModels", "AppUninstallViewModel.cs");

        Assert.Contains("public event Action<string, string>? NoticeRequested;", vm, StringComparison.Ordinal);
        Assert.Contains("if (hadErrors) SoundService.PlayWarningSound();", vm, StringComparison.Ordinal);
        Assert.Contains("\"Root Purge Completed With Issues\"", vm, StringComparison.Ordinal);
        Assert.Contains("These items could not be purged and are still on the system:", vm, StringComparison.Ordinal);

        // The View has to host the notice, otherwise the event is dead code.
        string view = ReadRepoFile("src", "Deltempo.App.WinUI", "Views", "AppUninstallView.xaml.cs");
        Assert.Contains("ViewModel.NoticeRequested += OnNotice;", view, StringComparison.Ordinal);
        Assert.Contains("private async void OnNotice(string title, string message)", view, StringComparison.Ordinal);
    }

    [Fact]
    public void WinUiUninstall_KeepsTheSameGates_AsWpf()
    {
        string vm = ReadRepoFile("src", "Deltempo.App.WinUI", "ViewModels", "AppUninstallViewModel.cs");

        Assert.Contains("Restore Point Not Created", vm, StringComparison.Ordinal);
        Assert.Contains("Continue with the uninstall without a restore point?", vm, StringComparison.Ordinal);
        Assert.Contains("Nothing is deleted until you review", vm, StringComparison.Ordinal);
        Assert.DoesNotContain("directly purge", vm, StringComparison.OrdinalIgnoreCase);

        // CanPurgeTraces is computed; it must be republished when IsBusy flips or the
        // purge button stays enabled during a delete.
        Assert.Contains("partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanPurgeTraces));",
            vm, StringComparison.Ordinal);
    }

    [Fact]
    public void WinUiAndWpfTooltips_DescribeAReviewStep_NotAnAutomaticPurge()
    {
        string winui = ReadRepoFile("src", "Deltempo.App.WinUI", "Views", "AppUninstallView.xaml");
        string wpf = ReadRepoFile("src", "Deltempo.App.Wpf", "Views", "Modals", "AppUninstallModal.xaml");

        Assert.Contains("Nothing is deleted until you confirm the purge", winui, StringComparison.Ordinal);
        Assert.Contains("then scan leftover traces for your review", winui, StringComparison.Ordinal);
        Assert.Contains("then scan leftover traces for your review", wpf, StringComparison.Ordinal);
    }

    [Fact]
    public void CliPurge_ReportsPartialFailure_InJsonAndInTheExitCode()
    {
        string cli = ReadRepoFile("src", "Deltempo.Core", "Services", "CliRunner.Uninstall.cs");

        Assert.Contains("success = purgeResult.ErrorsCount == 0", cli, StringComparison.Ordinal);
        Assert.Contains("errors = purgeResult.ErrorMessages", cli, StringComparison.Ordinal);
        Assert.Contains("rebootScheduledPaths = purgeResult.RebootScheduledItems", cli, StringComparison.Ordinal);
        Assert.Contains("return purgeResult.ErrorsCount == 0 ? 0 : 2;", cli, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(params string[] segments)
    {
        string dir = AppContext.BaseDirectory;

        for (var i = 0; i < 12; i++)
        {
            string candidate = Path.Combine(dir, Path.Combine(segments));
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }

        throw new InvalidOperationException(
            $"could not locate {Path.Combine(segments)} under the repository root");
    }
}
