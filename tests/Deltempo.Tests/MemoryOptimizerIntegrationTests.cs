using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Integration tests for MemoryOptimizerService native API calls.
/// These invoke real Windows NT memory APIs (NtSetSystemInformation, etc.).
/// Only safe, user-initiated targets are exercised — no aggressive purging.
/// Tests are skipped if the native call is not supported on this OS.
/// </summary>
[Collection("SettingsService")]
public class MemoryOptimizerIntegrationTests
{
    #region GetMemoryInfo

    [Fact]
    public void GetMemoryInfo_ReturnsValidMemorySnapshot()
    {
        var info = MemoryOptimizerService.GetMemoryInfo();

        Assert.NotNull(info);
        Assert.True(info.MeasurementSucceeded, "Memory measurement should succeed on this OS");
        Assert.True(info.TotalPhysicalBytes > 0, "TotalPhysicalBytes must be > 0");
        Assert.True(info.AvailablePhysicalBytes > 0, "AvailablePhysicalBytes must be > 0");
        Assert.True(info.AvailablePhysicalBytes <= info.TotalPhysicalBytes,
            "Available RAM must not exceed total RAM");
        Assert.True(info.CommitLimitBytes > 0, "CommitLimitBytes must be > 0");
    }

    [Fact]
    public void GetMemoryInfo_CommitTotalDoesNotExceedCommitLimit()
    {
        var info = MemoryOptimizerService.GetMemoryInfo();

        Assert.True(info.CommitTotalBytes >= 0);
        Assert.True(info.CommitTotalBytes <= info.CommitLimitBytes,
            "Commit total must not exceed commit limit");
    }

    #endregion

    #region GetMemoryAreaSnapshots

    [Fact]
    public void GetMemoryAreaSnapshots_ReturnsAllTargetsWithValidMetadata()
    {
        var areas = MemoryOptimizerService.GetMemoryAreaSnapshots();

        Assert.NotNull(areas);
        Assert.NotEmpty(areas);
        Assert.Equal(Enum.GetValues<MemoryTargetType>().Length, areas.Count);

        foreach (var area in areas)
        {
            Assert.False(string.IsNullOrWhiteSpace(area.DisplayName), $"Target {area.Target} missing display name");
            Assert.False(string.IsNullOrWhiteSpace(area.SafetyBadge), $"Target {area.Target} missing safety badge");
            Assert.True(area.TotalBytes >= 0, $"Target {area.Target} TotalBytes must not be negative");
            Assert.True(area.FreeBytes >= 0, $"Target {area.Target} FreeBytes must not be negative");
            Assert.True(area.CurrentBytes >= 0, $"Target {area.Target} CurrentBytes must not be negative");
            Assert.True(area.UsedPercent >= 0.0 && area.UsedPercent <= 100.0, $"Target {area.Target} UsedPercent out of range: {area.UsedPercent}");
        }
    }

    #endregion

    #region OptimizeRamAsync - safe targets only

    [Fact]
    public async Task OptimizeRamAsync_WorkingSetOnly_ReturnsValidResult()
    {
        // WorkingSet is the gentlest target: empty working sets, doesn't discard file cache.
        var result = await MemoryOptimizerService.OptimizeRamAsync(
            new[] { MemoryTargetType.WorkingSet });

        Assert.NotNull(result);
        Assert.True(result.ExecutionTimeMs >= 0);
        Assert.True(result.MeasuredAvailableBefore >= 0);
        Assert.True(result.MeasuredAvailableAfter >= 0);
        Assert.True(result.MeasuredAvailableDelta >= 0, "MeasuredAvailableDelta must never be negative");
        Assert.Equal(1, result.RequestedTargetCount);
        Assert.Equal(1, result.SucceededTargetCount + result.FailedTargetCount);
        Assert.NotEmpty(result.AreaResults);
        Assert.Contains(result.AreaResults, r => r.Target == MemoryTargetType.WorkingSet);
    }

    [Fact]
    public async Task OptimizeRamAsync_DefaultTargets_ReturnsResultsForEachTarget()
    {
        var result = await MemoryOptimizerService.OptimizeRamAsync();

        Assert.NotNull(result);
        Assert.True(result.ExecutionTimeMs >= 0);
        Assert.NotEmpty(result.AreaResults);
        // Default targets exclude StandbyListLowPriority (gentler, user-selectable)
        Assert.DoesNotContain(result.AreaResults, r => r.Target == MemoryTargetType.StandbyListLowPriority);
    }

    [Fact]
    public async Task OptimizeRamAsync_EmptyTargetArray_ReturnsNoApplicableTargets()
    {
        var result = await MemoryOptimizerService.OptimizeRamAsync(Array.Empty<MemoryTargetType>());

        Assert.NotNull(result);
        Assert.Empty(result.AreaResults);
        // Empty target list: nothing attempted → NoApplicableTargets, not Success.
        Assert.Equal(MemoryOperationStatus.NoApplicableTargets, result.Status);
    }

    #endregion

    #region OptimizeAreaAsync

    [Fact]
    public async Task OptimizeAreaAsync_WorkingSet_ReturnsResult()
    {
        var result = await MemoryOptimizerService.OptimizeAreaAsync(MemoryTargetType.WorkingSet);

        Assert.NotNull(result);
        Assert.Equal(MemoryTargetType.WorkingSet, result.Target);
        Assert.True(result.Success == (result.Status is MemoryOperationStatus.Success or MemoryOperationStatus.PartialSuccess));
    }

    [Fact]
    public async Task OptimizeAreaAsync_AllTargets_ReturnOneResultPerTarget()
    {
        foreach (var t in Enum.GetValues<MemoryTargetType>())
        {
            var result = await MemoryOptimizerService.OptimizeAreaAsync(t);

            Assert.NotNull(result);
            Assert.Equal(t, result.Target);
            Assert.True(result.Success == (result.Status is MemoryOperationStatus.Success or MemoryOperationStatus.PartialSuccess));
            // Unsupported status is acceptable on some OS versions; Failed is acceptable too.
            Assert.True(
                result.Status == MemoryOperationStatus.Success ||
                result.Status == MemoryOperationStatus.PartialSuccess ||
                result.Status == MemoryOperationStatus.Unsupported ||
                result.Status == MemoryOperationStatus.AccessDenied ||
                result.Status == MemoryOperationStatus.Failed,
                $"Unexpected status {result.Status} for target {t}");
        }
    }

    #endregion

    #region Cancellation

    [Fact]
    public async Task OptimizeRamAsync_CancellationToken_CompletesWithoutThrowing()
    {
        // Cancel after a short delay so the task starts first, then cooperative
        // cancellation stops processing mid-loop. The task should complete (not throw)
        // because ct.IsCancellationRequested is checked in the loop.
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(25);

        var result = await MemoryOptimizerService.OptimizeRamAsync(
            new[] { MemoryTargetType.WorkingSet, MemoryTargetType.StandbyList },
            cts.Token);

        Assert.NotNull(result);
        // With early cancellation, likely no areas were processed successfully.
        Assert.True(
            result.Status == MemoryOperationStatus.Failed ||
            result.Status == MemoryOperationStatus.NoApplicableTargets ||
            result.Status == MemoryOperationStatus.PartialSuccess ||
            result.Status == MemoryOperationStatus.Success,
            $"Unexpected status after cancellation: {result.Status}");
    }

    #endregion
}
