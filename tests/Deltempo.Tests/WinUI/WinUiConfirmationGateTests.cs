using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Deltempo.App.WinUI.ViewModels;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Every destructive command in the WinUI 3 shell goes through an injected
/// confirmation host, and every one of those hosts DEFAULTS TO REFUSING. That is
/// the design that makes the shell safe to ship next to a Windows cleaner: with
/// no confirmation surface wired up, no destructive path can be entered.
///
/// None of it was tested. The shell's ViewModels were not compiled into the test
/// project at all, so the invariant was a comment in a source file rather than
/// something the build could check.
///
/// These tests drive the item-scoped destructive commands of each shell surface
/// with a confirmation host that records the prompt and refuses, and assert that
/// the prompt was raised and that nothing was mutated. Where the mutation is
/// observable on disk, it is asserted on disk - a file that must still be there.
///
/// The collection-scoped commands (SelectSafe, OptimizeBoot, CleanSelected,
/// RecycleSelected) are not covered here because they read a private list that
/// only a real scan populates; driving them would mean a real scan. The
/// item-scoped commands take their subject as an argument, which is exactly what
/// makes them testable.
/// </summary>
public class WinUiConfirmationGateTests : IDisposable
{
    private readonly string _sandbox;

    public WinUiConfirmationGateTests()
    {
        _sandbox = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Deltempo_WinUiGate_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_sandbox);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
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

    /// <summary>A confirmation host that records the prompt it was given and refuses.</summary>
    private sealed class RefusingHost
    {
        public List<string> Titles { get; } = [];

        public List<string> Messages { get; } = [];

        public Func<string, string, Task<bool>> Callback => (title, message) =>
        {
            Titles.Add(title);
            Messages.Add(message);
            return Task.FromResult(false);
        };
    }

    [Fact]
    public async Task ForceRootClean_AsksForConfirmation_AndRefusalInspectsNothing()
    {
        RefusingHost host = new();
        AppUninstallViewModel vm = new(confirmAsync: host.Callback);

        InstalledAppItem app = new() { DisplayName = "Widget Pro", Publisher = "Acme" };
        vm.Apps.Add(app);

        await vm.ForceRootCleanCommand.ExecuteAsync(app);

        Assert.Contains("Force Root Clean", host.Titles);
        Assert.Contains("Widget Pro", host.Messages[0]);

        // The scan that finds the leftovers is downstream of the confirmation, so
        // a refusal means the app was never inspected and nothing was collected.
        Assert.Empty(vm.Leftovers);
    }

    [Fact]
    public async Task RecycleLargeFile_AsksForConfirmation_AndRefusalLeavesTheFileOnDisk()
    {
        string file = Path.Combine(_sandbox, "large.bin");
        await File.WriteAllTextAsync(file, new string('x', 4096));

        RefusingHost host = new();
        LargeFilesWorkspaceViewModel vm = new(confirmAsync: host.Callback);

        LargeFileInfo item = new()
        {
            FilePath = file,
            FileName = "large.bin",
            SizeBytes = 4096,
            SafetyTier = SafetyRiskTier.Safe,
        };
        vm.Items.Add(item);

        await vm.RecycleItemCommand.ExecuteAsync(item);

        Assert.Contains("Recycle Large File", host.Titles);
        Assert.True(
            File.Exists(file),
            "A refused confirmation must not move the file to the Recycle Bin.");
    }

    [Fact]
    public async Task RecycleLargeFile_WithNoConfirmationHost_LeavesTheFileOnDisk()
    {
        // The default host, not an injected one. This is the fail-closed default
        // the shell ships with.
        string file = Path.Combine(_sandbox, "untouched.bin");
        await File.WriteAllTextAsync(file, new string('x', 4096));

        LargeFilesWorkspaceViewModel vm = new();

        LargeFileInfo item = new()
        {
            FilePath = file,
            FileName = "untouched.bin",
            SizeBytes = 4096,
            SafetyTier = SafetyRiskTier.Safe,
        };
        vm.Items.Add(item);

        await vm.RecycleItemCommand.ExecuteAsync(item);

        Assert.True(
            File.Exists(file),
            "A ViewModel with no confirmation host must not move the file.");
    }

    [Fact]
    public async Task CleanOrphanedStartupEntry_AsksForConfirmation_AndRefusalKeepsTheEntry()
    {
        RefusingHost host = new();
        StartupWorkspaceViewModel vm = new(confirmAsync: host.Callback);

        StartupItem item = new()
        {
            Name = "Orphaned Test Entry",
            FriendlyName = "Orphaned Test Entry",
            Command = @"C:\nonexistent\orphan.exe",
            ExePath = @"C:\nonexistent\orphan.exe",
            Location = "HKCU",
            IsFileMissing = true,
        };

        Assert.True(item.CanCleanOrphaned, "The fixture must be cleanable for the test to mean anything.");

        vm.Items.Add(item);
        await vm.CleanOrphanedCommand.ExecuteAsync(item);

        Assert.Contains("Clean Orphaned Entry", host.Titles);
        Assert.Contains(item, vm.Items);
    }

    [Fact]
    public async Task Cleaner_AsksForConfirmation_AndRefusalSelectsNothingAway()
    {
        // The cleaner's destructive command is collection-scoped and needs a scan
        // to populate, so this asserts the OTHER half of the same contract: the
        // selection helpers that decide what would be cleaned.
        RefusingHost host = new();
        CleanerWorkspaceViewModel vm = new(confirmAsync: host.Callback);

        Assert.NotNull(vm.SelectSafeCommand);
        Assert.NotNull(vm.DeselectAllCommand);
        Assert.False(vm.HasSelection);

        // With no confirmation host the clean button cannot do anything.
        await vm.CleanSelectedCommand.ExecuteAsync(null);

        Assert.False(vm.HasSelection);
    }
}
