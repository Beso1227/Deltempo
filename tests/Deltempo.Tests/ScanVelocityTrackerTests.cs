using System;
using System.Threading;
using WinTempCleaner.Core.Scanning;
using Xunit;

namespace Deltempo.Tests;

public class ScanVelocityTrackerTests
{
    [Fact]
    public void Fresh_Tracker_StartsEmpty()
    {
        var tracker = new ScanVelocityTracker();

        Assert.Equal(0, tracker.TotalFiles);
        Assert.Equal(0, tracker.TotalBytes);
        Assert.True(tracker.Elapsed >= TimeSpan.Zero);
    }

    [Fact]
    public void RecordProgress_AccumulatesFilesAndBytes()
    {
        var tracker = new ScanVelocityTracker();

        tracker.RecordProgress(500);
        tracker.RecordProgress(250, filesAdded: 3);

        Assert.Equal(4, tracker.TotalFiles);
        Assert.Equal(750, tracker.TotalBytes);
    }

    [Fact]
    public void SampleThroughput_ImmediatelyAfterConstruction_ReturnsZero()
    {
        var tracker = new ScanVelocityTracker();

        // Under the 0.25s sampling window: no instantaneous spike is reported,
        // which is exactly what keeps a just-started scan from showing 1e6 files/s.
        var (filesPerSec, bytesPerSec) = tracker.SampleThroughput();

        Assert.Equal(0, filesPerSec);
        Assert.Equal(0, bytesPerSec);
    }

    [Fact]
    public void SampleThroughput_ReflectsRecordedProgressAfterSamplingWindow()
    {
        var tracker = new ScanVelocityTracker();

        tracker.RecordProgress(1_000_000);
        Thread.Sleep(320); // exceed the 0.25s smoothing window

        var (filesPerSec, bytesPerSec) = tracker.SampleThroughput();

        Assert.True(bytesPerSec > 0, "bytesPerSec should be positive after 1 MB was recorded.");
        Assert.True(filesPerSec > 0, "filesPerSec should be positive after one file was recorded.");
    }

    [Fact]
    public void GetFormattedVelocity_ReturnsAReadableUiString()
    {
        var tracker = new ScanVelocityTracker();

        string text = tracker.GetFormattedVelocity();

        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.Contains("files/s", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(5, 5)]
    [InlineData(7, 5)]
    public void EstimateRemaining_ReturnsNull_WhenNoWorkLeftToEstimate(int completed, int total)
    {
        var tracker = new ScanVelocityTracker();

        Assert.Null(tracker.EstimateRemaining(completed, total));
    }

    [Fact]
    public void EstimateRemaining_ReturnsPositiveSpan_WhenProgressIsAvailable()
    {
        var tracker = new ScanVelocityTracker();

        tracker.RecordProgress(0);
        Thread.Sleep(60); // give the rate a nonzero elapsed base

        TimeSpan? estimate = tracker.EstimateRemaining(completedUnits: 1, totalUnits: 10);

        Assert.NotNull(estimate);
        Assert.True(estimate!.Value > TimeSpan.Zero);
    }
}
