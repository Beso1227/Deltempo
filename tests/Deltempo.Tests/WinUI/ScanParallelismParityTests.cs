using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// The last behavioural gap between the two heads: the cleaner scan.
///
/// WPF walked the target list through <c>VolumeScanCoordinator</c> (three folders per
/// drive, drives in parallel) and reported throughput plus an ETA from
/// <c>ScanVelocityTracker</c>. The WinUI preview ran a plain <c>foreach</c> with
/// <c>await</c>, so a second drive was scanned strictly after the first and the status
/// line could only say which single category was in flight.
///
/// Shape-only, like the other parity files: <c>ScanAllAsync</c> touches the real
/// filesystem, so it cannot be run inside a test. Each check pairs a source fact the
/// shell build would break if it drifted with the WPF counterpart that must keep
/// existing too. The coordinator and tracker themselves are covered behaviourally by
/// <c>VolumeScanCoordinatorTests</c> and <c>ScanVelocityTrackerTests</c>.
/// </summary>
public class ScanParallelismParityTests
{
    private static string ReadWinUi(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot(), "src", "Deltempo.App.WinUI" }.Concat(parts).ToArray()));

    private static string ReadWpf(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot(), "src", "Deltempo.App.Wpf" }.Concat(parts).ToArray()));

    private static string CleanerViewModel() => ReadWinUi("ViewModels", "CleanerWorkspaceViewModel.cs");

    // ------------------------------------------------------------------
    // The counterpart, and the coordinator call that replaced the loop.
    // ------------------------------------------------------------------

    [Fact]
    public void WpfScan_PartitionsByVolumeAndTracksVelocity()
    {
        string wpf = ReadWpf("MainWindow.Cleaning.cs");

        Assert.Contains("ScanVelocityTracker", wpf);
        Assert.Contains("VolumeScanCoordinator.ExecutePartitionedAsync", wpf);
        Assert.Contains(
            ".ResolveVolumeKeyForTarget(target, WinTempCleaner.Services.CleanerService.ResolveDirectoriesForFolderPublic)",
            wpf);
        Assert.Contains("maxParallelismPerVolume: 3", wpf);
        Assert.Contains("GetFormattedVelocity()", wpf);
        Assert.Contains("EstimateRemaining(done, totalTargets)", wpf);
    }

    [Fact]
    public void WinUiScan_UsesTheSameVolumePartitionedCoordinator()
    {
        string vm = CleanerViewModel();

        Assert.Contains("VolumeScanCoordinator.ExecutePartitionedAsync", vm);

        // Keyed on the volume the target's REAL directories live on: seventeen of the
        // twenty-six default scopes carry a display string in FolderPath, which would
        // collapse them into one partition and defeat the per-volume cap.
        Assert.Contains("VolumeScanCoordinator.ResolveVolumeKeyForTarget", vm);
        Assert.Contains("CleanerService.ResolveDirectoriesForFolderPublic", vm);
        Assert.Contains("VolumeScanCoordinator.DefaultMaxParallelismPerVolume", vm);
    }

    [Fact]
    public void WinUiScan_NoLongerWalksTheTargetsOneAtATime()
    {
        string vm = CleanerViewModel();

        // The sequential call took the scan's own token; the partitioned one takes the
        // per-worker token the coordinator hands it.
        Assert.DoesNotContain("ScanFolderAsync(target, Log, token, safeMode)", vm);
        Assert.Contains("ScanFolderAsync(target, Log, workerToken, safeMode)", vm);

        // And the line that named one in-flight category at a time is gone with the loop:
        // three folders can be scanning at once, so no single name is truthful.
        Assert.DoesNotContain("Scanning ({done + 1}/{targets.Count})", vm);
    }

    // ------------------------------------------------------------------
    // Throughput, ETA and the throttled repaint.
    // ------------------------------------------------------------------

    [Fact]
    public void WinUiScan_ReportsThroughputAndEta()
    {
        string vm = CleanerViewModel();

        Assert.Contains("new ScanVelocityTracker()", vm);
        Assert.Contains("velocity.RecordProgress(target.SizeBytes, target.FileCount)", vm);
        Assert.Contains("velocity.GetFormattedVelocity()", vm);
        Assert.Contains("velocity.EstimateRemaining(completed, totalTargets)", vm);

        // Same rule as WPF: an ETA under a second is noise, not information.
        Assert.Contains("eta.Value.TotalSeconds >= 1", vm);
        Assert.Contains("ETA: {eta.Value.TotalSeconds:N0}s", vm);
    }

    [Fact]
    public void WinUiScan_ThrottlesRepaintsButAlwaysPaintsTheLastTarget()
    {
        string vm = CleanerViewModel();

        Assert.Contains("Interlocked.Increment(ref done)", vm);
        Assert.Contains("Interlocked.Read(ref lastUiUpdateTicks)", vm);
        Assert.Contains("Interlocked.Exchange(ref lastUiUpdateTicks, now)", vm);

        // Four repaints a second, and the final target repaints unconditionally so the
        // bar cannot stop short of the visible scan's share.
        Assert.Contains(".TotalMilliseconds < 250", vm);
        Assert.Contains("completed != totalTargets", vm);
    }

    [Fact]
    public void WinUiScan_MarshalsWorkerProgressThroughTheInjectedPoster()
    {
        string vm = CleanerViewModel();

        // The worker lambda runs on the thread pool; PropertyChanged must not be raised
        // there. The shell injects DispatcherQueue.TryEnqueue as _post (see
        // CleanerWorkspaceView.xaml.cs), and a test host passes nothing, which runs inline.
        int post = vm.IndexOf("_post(() =>", StringComparison.Ordinal);
        Assert.True(post >= 0, "the throttled scan update must go through the injected _post marshaler");

        int status = vm.IndexOf("StatusText = $\"Scanning ({completed}/{totalTargets})", StringComparison.Ordinal);
        Assert.True(status > post, "the scan status line must be written inside the _post callback");
        Assert.True(status - post < 800, "the status write moved away from the _post callback");
    }

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
