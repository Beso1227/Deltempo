using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Deltempo.App.WinUI.ViewModels;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// The WinUI 3 shell's force-delete surface is the most safety-sensitive screen in
/// the app, and it had NO tests at all: the shell's ViewModels were never compiled
/// into the test project, so nothing exercised the invariants the port was
/// explicitly designed around.
///
/// The seam already existed. Every ViewModel takes its confirmation host through
/// the constructor and DEFAULTS TO REFUSING, which is what makes the surface
/// testable without a UI. These tests hold that line down - in particular the
/// three properties the port's own documentation claims, none of which were
/// checked by anything before now:
///
///   1. Shielded targets are staged PRE-DESELECTED.
///   2. EligibleTargets() re-filters on tier, so a shielded path cannot reach the
///      engine even if selection state is corrupted or written from a binding.
///   3. The destructive confirmation is injected and defaults to refusing, so a
///      ViewModel with no confirmation host cannot delete anything.
/// </summary>
public class ForceDeleteViewModelTests : IDisposable
{
    private readonly string _sandbox;

    public ForceDeleteViewModelTests()
    {
        _sandbox = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Deltempo_WinUiForceDelete_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_sandbox);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_sandbox))
            {
                Directory.Delete(_sandbox, recursive: true);
            }
        }
        catch (IOException)
        {
            // A locked sandbox is not a test failure.
        }
    }

    /// <summary>
    /// A path the gate classifies as Tier A (AbsoluteBlock): anything inside the
    /// Windows directory's System32. Derived from the environment rather than
    /// hardcoded, so it holds on a machine where Windows is not on C:.
    /// </summary>
    private static string ShieldedSystemPath()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "notepad.exe");

    [Fact]
    public async Task ShieldedTarget_IsStagedPreDeselected_AndIsNeverEligible()
    {
        ForceDeleteViewModel vm = new();

        await vm.StagePathsAsync([ShieldedSystemPath()]);

        ForceDeleteTargetRowViewModel row = Assert.Single(vm.Targets);
        Assert.Equal(ForceDeleteTier.AbsoluteBlock, row.Tier);
        Assert.True(row.IsShielded);
        Assert.False(row.IsSelectable);

        // Pre-deselected: a shielded target does not arrive selected.
        Assert.False(row.IsSelected);

        // And structurally excluded, so the tier - not the selection flag - is what
        // keeps it away from the engine.
        Assert.Empty(vm.EligibleTargets());
        Assert.False(vm.HasEligibleTargets);
        Assert.False(vm.CanRunPurge);
    }

    [Fact]
    public async Task ShieldedTarget_CannotReachTheEngineEvenIfSelectionStateIsCorrupted()
    {
        // The selection flag is bound to a checkbox and could be written by
        // anything. The tier re-filter is the structural guard, so corrupting the
        // flag must not be enough to get a shielded path deleted.
        ForceDeleteViewModel vm = new();

        await vm.StagePathsAsync([ShieldedSystemPath()]);
        ForceDeleteTargetRowViewModel row = Assert.Single(vm.Targets);

        row.IsSelected = true;

        Assert.Empty(vm.EligibleTargets());
        Assert.False(vm.HasEligibleTargets);
    }

    [Fact]
    public async Task TogglingAShieldedRow_CannotSelectIt()
    {
        ForceDeleteViewModel vm = new();

        await vm.StagePathsAsync([ShieldedSystemPath()]);
        ForceDeleteTargetRowViewModel row = Assert.Single(vm.Targets);

        vm.ToggleSelectionCommand.Execute(row);

        Assert.False(row.IsSelected);
        Assert.Empty(vm.EligibleTargets());
    }

    [Fact]
    public async Task WithNoConfirmationHost_ExecuteDeletesNothing()
    {
        // The end-to-end proof that the default really is fail-closed: a real file
        // on disk, a real staging, a real Execute - and the file survives, because
        // the absent confirmation host answers "no" forever.
        string file = Path.Combine(_sandbox, "must-survive.txt");
        await File.WriteAllTextAsync(file, "payload");

        ForceDeleteViewModel vm = new();
        await vm.StagePathsAsync([file]);

        ForceDeleteTargetRowViewModel row = Assert.Single(vm.Targets);
        Assert.True(row.IsSelected, "A normal file must be eligible to stage.");

        await vm.ExecuteCommand.ExecuteAsync(null);

        Assert.True(
            File.Exists(file),
            "A ForceDeleteViewModel with no confirmation host must not delete anything.");
    }

    [Fact]
    public async Task WithNoConfirmationHost_ExecuteReportsThatItWasCancelled()
    {
        string file = Path.Combine(_sandbox, "cancelled.txt");
        await File.WriteAllTextAsync(file, "payload");

        var logs = new List<string>();
        ForceDeleteViewModel vm = new();
        vm.LogRequested += (message, _) => logs.Add(message);

        await vm.StagePathsAsync([file]);
        await vm.ExecuteCommand.ExecuteAsync(null);

        Assert.Contains(logs, m => m.Contains("cancelled by user", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ARefusedConfirmation_StopsThePurgeAndLeavesTheFileAlone()
    {
        string file = Path.Combine(_sandbox, "refused.txt");
        await File.WriteAllTextAsync(file, "payload");

        bool prompted = false;
        ForceDeleteViewModel vm = new(
            confirmAsync: (_, _) =>
            {
                prompted = true;
                return Task.FromResult(false);
            });

        await vm.StagePathsAsync([file]);
        await vm.ExecuteCommand.ExecuteAsync(null);

        Assert.True(prompted, "The destructive path must ask for confirmation.");
        Assert.True(File.Exists(file), "A refused confirmation must not delete anything.");
    }

    [Fact]
    public void Defaults_MatchThePrescribedBehaviour()
    {
        ForceDeleteViewModel vm = new();

        // Terminating the applications holding a file is a destructive capability,
        // so it starts in the state the WPF app prescribed - not the safer-looking
        // one - and permanent deletion is the default rather than the Recycle Bin.
        Assert.True(vm.TerminateLockers);
        Assert.False(vm.SendToRecycleBin);
    }

    [Fact]
    public async Task ConfirmationText_NamesTheCountTheSizeAndTheIrreversibility()
    {
        string file = Path.Combine(_sandbox, "big.bin");
        await File.WriteAllTextAsync(file, new string('x', 2048));

        ForceDeleteViewModel vm = new();
        await vm.StagePathsAsync([file]);

        List<ForceDeleteTargetRowViewModel> eligible = vm.EligibleTargets();
        Assert.Single(eligible);

        string text = vm.BuildConfirmationText(eligible);

        Assert.Contains("Permanently delete", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1 file", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CANNOT be undone", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmationText_WarnsWhenForceTerminatingLockers()
    {
        string file = Path.Combine(_sandbox, "locked.bin");
        await File.WriteAllTextAsync(file, "payload");

        ForceDeleteViewModel vm = new();
        await vm.StagePathsAsync([file]);

        string text = vm.BuildConfirmationText(vm.EligibleTargets());

        Assert.Contains("force-terminated", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Summary_ReportsTheShieldedCount()
    {
        ForceDeleteViewModel vm = new();
        await vm.StagePathsAsync([ShieldedSystemPath()]);

        Assert.True(vm.HasProtectedNotice);
        Assert.Contains("shielded", vm.ProtectedNotice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never be deleted", vm.ProtectedNotice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StagingTheSamePathTwice_KeepsOneRow()
    {
        string file = Path.Combine(_sandbox, "duplicate.txt");
        await File.WriteAllTextAsync(file, "payload");

        ForceDeleteViewModel vm = new();
        await vm.StagePathsAsync([file]);
        await vm.StagePathsAsync([file]);

        Assert.Single(vm.Targets);
    }

    [Fact]
    public async Task ClearAll_EmptiesTheStagingList()
    {
        string file = Path.Combine(_sandbox, "clearme.txt");
        await File.WriteAllTextAsync(file, "payload");

        ForceDeleteViewModel vm = new();
        await vm.StagePathsAsync([file]);
        Assert.NotEmpty(vm.Targets);

        vm.ClearAllCommand.Execute(null);

        Assert.Empty(vm.Targets);
        Assert.False(vm.HasTargets);
        Assert.False(vm.CanRunPurge);
    }

    [Fact]
    public async Task StagingWithoutAPicker_IsImpossible()
    {
        // The picker is injected and defaults to null, so with no picker there is
        // nothing to stage and therefore nothing for Execute to act on.
        ForceDeleteViewModel vm = new();

        await vm.AddFilesCommand.ExecuteAsync(null);
        await vm.AddFolderCommand.ExecuteAsync(null);

        Assert.Empty(vm.Targets);
    }

    [Fact]
    public async Task BadgeWording_DistinguishesTheThreeTiers()
    {
        ForceDeleteViewModel vm = new();
        await vm.StagePathsAsync([ShieldedSystemPath()]);

        ForceDeleteTargetRowViewModel shielded = Assert.Single(vm.Targets);
        Assert.Equal("Shielded", shielded.BadgeText);

        ForceDeleteTargetRowViewModel safe = new(
            new ForceDeleteTargetItem
            {
                FilePath = @"C:\temp\thing.tmp",
                FileName = "thing.tmp",
                SizeBytes = 10,
                GateTier = ForceDeleteTier.Allowed,
            },
            vm.ToggleSelectionCommand);

        Assert.Equal("Safe", safe.BadgeText);
        Assert.True(safe.IsSelectable);
        Assert.False(safe.IsShielded);
    }
}
