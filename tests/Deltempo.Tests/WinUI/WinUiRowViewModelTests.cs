using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Deltempo.App.WinUI.ViewModels;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// The WinUI shell's row and presentation ViewModels.
///
/// These are the types that carry the wording and the numbers the user actually
/// reads - the badge that says "Shielded" rather than "Safe", the formatted size,
/// the share percentage on the AppData footprint bar. They had no tests, and the
/// shell's own design notes record that this layer has already produced wrong
/// output before: a share bar that rendered as an empty track, and a byte total
/// that double-counted every file.
///
/// They are plain objects with plain inputs, so they are directly constructible.
/// </summary>
public class WinUiRowViewModelTests
{
    [Fact]
    public void LockedFileRow_ProjectsTheItemsFields()
    {
        LockedFileItem item = new()
        {
            FilePath = @"C:\apps\thing.exe",
            FileName = "thing.exe",
            SizeBytes = 2048,
            LockingProcessName = "explorer",
            LockingProcessId = 4242,
        };

        LockedFilesViewModel vm = new();
        LockedFileRowViewModel row = new(item, vm.ScheduleOneCommand);

        Assert.Equal("thing.exe", row.FileName);
        Assert.Equal(@"C:\apps\thing.exe", row.FilePath);
        Assert.Equal(item.FormattedSize, row.FormattedSize);
        Assert.Contains("explorer", row.DisplayProcessInfo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("4242", row.DisplayProcessInfo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LockedFileRow_WithoutAKnownProcess_FallsBackToAGenericHolder()
    {
        // The process could not be resolved, but the file IS held by something, so
        // the row says "Active Application" rather than leaving the user to wonder
        // whether the file is locked at all.
        LockedFileItem item = new()
        {
            FilePath = @"C:\apps\thing.exe",
            FileName = "thing.exe",
            SizeBytes = 1,
        };

        LockedFilesViewModel vm = new();
        LockedFileRowViewModel row = new(item, vm.ScheduleOneCommand);

        Assert.Equal("Active Application", row.DisplayProcessInfo);
    }

    [Fact]
    public void LockedFileRow_WithANamedProcessAndNoId_ShowsTheName()
    {
        LockedFileItem item = new()
        {
            FileName = "thing.exe",
            SizeBytes = 1,
            LockingProcessName = "explorer",
        };

        LockedFilesViewModel vm = new();
        LockedFileRowViewModel row = new(item, vm.ScheduleOneCommand);

        Assert.Equal("explorer", row.DisplayProcessInfo);
    }

    [Fact]
    public void LockedFileRow_StatusText_IsUpdatable()
    {
        LockedFileItem item = new() { FileName = "thing.exe" };
        LockedFilesViewModel vm = new();
        LockedFileRowViewModel row = new(item, vm.ScheduleOneCommand);

        string before = row.StatusText;
        row.SetStatus("Scheduled for reboot");

        Assert.NotEqual(before, row.StatusText);
        Assert.Equal("Scheduled for reboot", row.StatusText);
    }

    [Fact]
    public void MemoryZoneRow_ProjectsTheSnapshotFields()
    {
        MemoryAreaSnapshot snapshot = new()
        {
            DisplayName = "Modified page file",
            TotalBytes = 8L * 1024 * 1024 * 1024,
            CurrentBytes = 2L * 1024 * 1024 * 1024,
            UsedPercent = 25.0,
            SafetyBadge = "SAFE",
        };

        MemoryOptimizerViewModel vm = new();
        MemoryZoneRowViewModel row = new(snapshot, vm.FlushZoneCommand);

        Assert.Equal("Modified page file", row.Snapshot.DisplayName);
        Assert.Equal(snapshot.FormattedCurrent, row.Snapshot.FormattedCurrent);
        Assert.Equal(snapshot.FormattedTotal, row.Snapshot.FormattedTotal);
        Assert.Equal(25.0, row.Snapshot.UsedPercent);
        Assert.Equal("SAFE", row.Snapshot.SafetyBadge);
    }

    [Fact]
    public void FootprintRow_CarriesItsDisplayFields()
    {
        FootprintRowViewModel row = new()
        {
            DisplayName = "Caches",
            Path = @"C:\Users\me\AppData\Local\Cache",
            FormattedSize = "1.2 GB",
            SharePercent = 42.5,
            BadgeText = "42%",
            BadgeForeground = "#FFFFFFFF",
            BadgeBackground = "#FF000000",
            BarColor = "#FF00FF00",
        };

        Assert.Equal("Caches", row.DisplayName);
        Assert.Equal("1.2 GB", row.FormattedSize);
        Assert.Equal(42.5, row.SharePercent);
        Assert.Equal("42%", row.BadgeText);
    }

    [Fact]
    public void ForceDeleteRow_BadgeColoursSeparateTheTiers()
    {
        ForceDeleteViewModel vm = new();

        ForceDeleteTargetRowViewModel shielded = new(
            new ForceDeleteTargetItem { FileName = "bootmgr", GateTier = ForceDeleteTier.AbsoluteBlock },
            vm.ToggleSelectionCommand);

        ForceDeleteTargetRowViewModel confirm = new(
            new ForceDeleteTargetItem { FileName = "Documents", GateTier = ForceDeleteTier.OverrideRequired },
            vm.ToggleSelectionCommand);

        ForceDeleteTargetRowViewModel safe = new(
            new ForceDeleteTargetItem { FileName = "temp.tmp", GateTier = ForceDeleteTier.Allowed },
            vm.ToggleSelectionCommand);

        Assert.Equal("Shielded", shielded.BadgeText);
        Assert.Equal("Confirm", confirm.BadgeText);
        Assert.Equal("Safe", safe.BadgeText);

        // The badge colour is the visual difference between "you cannot do this"
        // and "this is fine", so they must not be the same colour.
        Assert.NotEqual(shielded.BadgeBackgroundHex, safe.BadgeBackgroundHex);
        Assert.NotEqual(shielded.BadgeForegroundHex, safe.BadgeForegroundHex);
        Assert.NotEqual(confirm.BadgeBorderHex, safe.BadgeBorderHex);
    }

    [Fact]
    public void ForceDeleteRow_OnlyShieldedRowsAreUnselectable()
    {
        ForceDeleteViewModel vm = new();

        ForceDeleteTargetRowViewModel shielded = new(
            new ForceDeleteTargetItem { GateTier = ForceDeleteTier.AbsoluteBlock },
            vm.ToggleSelectionCommand);

        ForceDeleteTargetRowViewModel confirm = new(
            new ForceDeleteTargetItem { GateTier = ForceDeleteTier.OverrideRequired },
            vm.ToggleSelectionCommand);

        ForceDeleteTargetRowViewModel safe = new(
            new ForceDeleteTargetItem { GateTier = ForceDeleteTier.Allowed },
            vm.ToggleSelectionCommand);

        Assert.False(shielded.IsSelectable);
        Assert.True(confirm.IsSelectable);
        Assert.True(safe.IsSelectable);

        Assert.True(shielded.IsShielded);
        Assert.False(confirm.IsShielded);
        Assert.False(safe.IsShielded);

        Assert.True(confirm.RequiresOverride);
        Assert.False(safe.RequiresOverride);
    }

    [Fact]
    public void ForceDeleteRow_FormatsItsSize()
    {
        ForceDeleteViewModel vm = new();

        ForceDeleteTargetRowViewModel row = new(
            new ForceDeleteTargetItem { FileName = "big.bin", SizeBytes = 1024 },
            vm.ToggleSelectionCommand);

        Assert.Equal(TargetFolderInfo.FormatBytes(1024), row.FormattedSize);
    }
}
