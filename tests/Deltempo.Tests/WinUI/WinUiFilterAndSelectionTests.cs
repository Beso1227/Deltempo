using System.Collections.Generic;
using System.Linq;
using Deltempo.App.WinUI.ViewModels;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// The WinUI shell's search, filtering and selection logic.
///
/// Every one of these screens was ported from the WPF app, and the filtering is
/// the part most likely to drift: it is presentation logic over a scanned set,
/// and the WPF app and the WinUI shell can disagree about which entries a
/// filter includes without either looking wrong.
///
/// The ViewModels expose a LoadFrom seam so the scanned set can be supplied
/// without a registry scan, a Task Scheduler scan or a disk scan of the whole
/// machine. The logic under test is the real logic the app runs.
/// </summary>
public class WinUiFilterAndSelectionTests
{
    [Fact]
    public void StartupFilter_HighImpact_KeepsOnlyEnabledHighImpactEntries()
    {
        StartupWorkspaceViewModel vm = new();

        vm.LoadFrom([
            new StartupItem { Name = "High", FriendlyName = "High", IsEnabled = true, Impact = BootImpact.High },
            new StartupItem { Name = "HighOff", FriendlyName = "HighOff", IsEnabled = false, Impact = BootImpact.High },
            new StartupItem { Name = "Low", FriendlyName = "Low", IsEnabled = true, Impact = BootImpact.Low },
            new StartupItem { Name = "Mid", FriendlyName = "Mid", IsEnabled = true, Impact = BootImpact.Medium },
        ]);

        Assert.Equal(4, vm.Items.Count);

        vm.SetFilterCommand.Execute(StartupFilter.HighImpact);

        Assert.Single(vm.Items);
        Assert.Equal("High", vm.Items[0].Name);
    }

    [Fact]
    public void StartupFilter_Disabled_KeepsOnlyDisabledEntries()
    {
        StartupWorkspaceViewModel vm = new();

        vm.LoadFrom([
            new StartupItem { Name = "On", IsEnabled = true, Impact = BootImpact.Low },
            new StartupItem { Name = "Off", IsEnabled = false, Impact = BootImpact.Low },
        ]);

        vm.SetFilterCommand.Execute(StartupFilter.Disabled);

        Assert.Single(vm.Items);
        Assert.Equal("Off", vm.Items[0].Name);
    }

    [Fact]
    public void StartupFilter_Orphaned_KeepsOnlyEntriesWhoseFileIsMissing()
    {
        StartupWorkspaceViewModel vm = new();

        vm.LoadFrom([
            new StartupItem { Name = "Present", IsFileMissing = false },
            new StartupItem { Name = "Missing", IsFileMissing = true },
        ]);

        vm.SetFilterCommand.Execute(StartupFilter.Orphaned);

        Assert.Single(vm.Items);
        Assert.Equal("Missing", vm.Items[0].Name);
    }

    [Fact]
    public void StartupFilter_All_ShowsEveryScannedEntry()
    {
        StartupWorkspaceViewModel vm = new();

        vm.LoadFrom([
            new StartupItem { Name = "A" },
            new StartupItem { Name = "B", IsFileMissing = true },
            new StartupItem { Name = "C", IsEnabled = false },
        ]);

        vm.SetFilterCommand.Execute(StartupFilter.All);

        Assert.Equal(3, vm.Items.Count);
    }

    [Fact]
    public void StartupFilter_ChangingTheFilterDoesNotLoseEntries()
    {
        // The filter narrows what is SHOWN; the scanned set is what a different
        // filter draws from. Switching back must restore everything.
        StartupWorkspaceViewModel vm = new();

        vm.LoadFrom([
            new StartupItem { Name = "High", IsEnabled = true, Impact = BootImpact.High },
            new StartupItem { Name = "Low", IsEnabled = true, Impact = BootImpact.Low },
        ]);

        vm.SetFilterCommand.Execute(StartupFilter.HighImpact);
        Assert.Single(vm.Items);

        vm.SetFilterCommand.Execute(StartupFilter.All);
        Assert.Equal(2, vm.Items.Count);
    }

    [Fact]
    public void AppSearch_MatchesOnNameOrPublisher()
    {
        AppUninstallViewModel vm = new();

        vm.LoadFrom([
            new InstalledAppItem { DisplayName = "Firefox", Publisher = "Mozilla" },
            new InstalledAppItem { DisplayName = "Chrome", Publisher = "Google" },
            new InstalledAppItem { DisplayName = "Acme Editor", Publisher = "Acme Corp" },
        ]);

        vm.SearchText = "moz";
        Assert.Single(vm.Apps);
        Assert.Equal("Firefox", vm.Apps[0].DisplayName);

        vm.SearchText = "acme corp";
        Assert.Single(vm.Apps);
        Assert.Equal("Acme Editor", vm.Apps[0].DisplayName);

        vm.SearchText = string.Empty;
        Assert.Equal(3, vm.Apps.Count);
    }

    [Fact]
    public void AppSearch_MatchesTheNameCaseInsensitively()
    {
        AppUninstallViewModel vm = new();

        vm.LoadFrom([new InstalledAppItem { DisplayName = "Firefox" }]);

        vm.SearchText = "FIREFOX";
        Assert.Single(vm.Apps);

        vm.SearchText = "fire";
        Assert.Single(vm.Apps);
    }

    [Fact]
    public void AppCategoryFilter_KeepsOnlyThatCategory()
    {
        AppUninstallViewModel vm = new();

        vm.LoadFrom([
            new InstalledAppItem { DisplayName = "Firefox", Category = "Browsers" },
            new InstalledAppItem { DisplayName = "Chrome", Category = "Browser" },
            new InstalledAppItem { DisplayName = "Steam", Category = "Gaming" },
        ]);

        vm.SetFilterCommand.Execute(AppListFilter.Browsers);

        // "Browsers" and "Browser" are both browser categories - the filter must
        // not treat them as different things, or entries vanish depending on how
        // the installed app happened to label itself.
        Assert.Equal(2, vm.Apps.Count);
        Assert.DoesNotContain(vm.Apps, a => a.DisplayName == "Steam");

        vm.SetFilterCommand.Execute(AppListFilter.Gaming);
        Assert.Single(vm.Apps);
        Assert.Equal("Steam", vm.Apps[0].DisplayName);
    }

    [Fact]
    public void LargeFiles_SelectSafe_SelectsOnlyVerifiedSafeFiles()
    {
        LargeFilesWorkspaceViewModel vm = new();

        vm.LoadFrom([
            new LargeFileInfo { FileName = "safe.bin", SizeBytes = 100, IsSafe = true },
            new LargeFileInfo { FileName = "protected.bin", SizeBytes = 200, IsSafe = false },
            new LargeFileInfo { FileName = "also-safe.bin", SizeBytes = 300, IsSafe = true },
        ]);

        vm.SelectSafeCommand.Execute(null);

        Assert.True(vm.Items.Single(i => i.FileName == "safe.bin").IsSelected);
        Assert.False(vm.Items.Single(i => i.FileName == "protected.bin").IsSelected);
        Assert.True(vm.Items.Single(i => i.FileName == "also-safe.bin").IsSelected);
    }

    [Fact]
    public void LargeFiles_SelectAll_ThenClearSelection()
    {
        LargeFilesWorkspaceViewModel vm = new();

        vm.LoadFrom([
            new LargeFileInfo { FileName = "a.bin", SizeBytes = 1, IsSafe = true },
            new LargeFileInfo { FileName = "b.bin", SizeBytes = 2, IsSafe = false },
        ]);

        vm.SelectAllCommand.Execute(null);
        Assert.All(vm.Items, i => Assert.True(i.IsSelected));

        vm.ClearSelectionCommand.Execute(null);
        Assert.All(vm.Items, i => Assert.False(i.IsSelected));
    }

    [Fact]
    public void LargeFiles_StorageSplitAddsUpToTheTotal()
    {
        LargeFilesWorkspaceViewModel vm = new();

        vm.LoadFrom([
            new LargeFileInfo { FileName = "a.bin", SizeBytes = 1024, IsSafe = true },
            new LargeFileInfo { FileName = "b.bin", SizeBytes = 2048, IsSafe = false },
        ]);

        // The headline says how much is safe and how much is protected. If the two
        // did not add up to the total, the user would be looking at a split that
        // does not describe the disk.
        Assert.Contains("2 files", vm.TotalStorageText, StringComparison.Ordinal);
        Assert.Contains("1 safe", vm.SafeStorageText, StringComparison.Ordinal);
        Assert.Contains("1 protected", vm.ProtectedStorageText, StringComparison.Ordinal);
    }

    [Fact]
    public void Cleaner_SelectSafe_LeavesOrphanedAppFoldersForReview()
    {
        CleanerWorkspaceViewModel vm = new();

        vm.LoadFrom([
            new TargetFolderInfo { Id = "temp", Name = "Temp", IsOrphanedAppFolder = false, SizeBytes = 10 },
            new TargetFolderInfo { Id = "orphan", Name = "Orphan", IsOrphanedAppFolder = true, SizeBytes = 20 },
        ]);

        vm.SelectSafeCommand.Execute(null);

        // "Select safe" selects the 100% safe caches and deliberately LEAVES the
        // orphaned application leftovers unselected, for the user to review -
        // an orphaned app folder may still hold the user's data. This matches
        // the WPF app's SelectSafeOnlyButton_Click exactly.
        Assert.True(vm.Targets.Single(t => t.Id == "temp").IsSelected);
        Assert.False(vm.Targets.Single(t => t.Id == "orphan").IsSelected);
    }

    [Fact]
    public void Cleaner_DeselectAll_ClearsEverySelection()
    {
        CleanerWorkspaceViewModel vm = new();

        vm.LoadFrom([
            new TargetFolderInfo { Id = "temp", Name = "Temp", IsSafeModeEligible = true, SizeBytes = 10 },
            new TargetFolderInfo { Id = "other", Name = "Other", IsSafeModeEligible = true, SizeBytes = 20 },
        ]);

        vm.SelectAllCommand.Execute(null);
        Assert.All(vm.Targets, t => Assert.True(t.IsSelected));

        vm.DeselectAllCommand.Execute(null);
        Assert.All(vm.Targets, t => Assert.False(t.IsSelected));
        Assert.False(vm.HasSelection);
    }
}
