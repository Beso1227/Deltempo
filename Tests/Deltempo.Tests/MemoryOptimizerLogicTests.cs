using System;
using System.Collections.Generic;
using System.Linq;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Pure-logic tests for MemoryOptimizerService result types and calculations.
/// These require no native calls and run on any platform.
/// </summary>
public class MemoryOptimizerLogicTests
{
    #region MemoryOptimizationResult.Success mapping

    [Theory]
    [InlineData(MemoryOperationStatus.Success, true)]
    [InlineData(MemoryOperationStatus.PartialSuccess, true)]
    [InlineData(MemoryOperationStatus.NoApplicableTargets, false)]
    [InlineData(MemoryOperationStatus.AccessDenied, false)]
    [InlineData(MemoryOperationStatus.Unsupported, false)]
    [InlineData(MemoryOperationStatus.Failed, false)]
    public void MemoryOptimizationResult_Success_MapsCorrectly(MemoryOperationStatus status, bool expected)
    {
        var result = new MemoryOptimizationResult { Status = status };
        Assert.Equal(expected, result.Success);
    }

    [Fact]
    public void MemoryOptimizationResult_MeasuredAvailableDelta_NeverNegative()
    {
        var result = new MemoryOptimizationResult
        {
            MeasuredAvailableBefore = 100L,
            MeasuredAvailableAfter = 50L, // "delta" would be negative
        };

        Assert.Equal(0L, result.MeasuredAvailableDelta);
    }

    [Fact]
    public void MemoryOptimizationResult_MeasuredAvailableDelta_ReportsActualDelta()
    {
        var result = new MemoryOptimizationResult
        {
            MeasuredAvailableBefore = 1000L,
            MeasuredAvailableAfter = 1200L,
        };

        Assert.Equal(200L, result.MeasuredAvailableDelta);
    }

    [Fact]
    public void MemoryOptimizationResult_MeasuredAvailableDelta_ZeroWhenEqual()
    {
        var result = new MemoryOptimizationResult
        {
            MeasuredAvailableBefore = 500L,
            MeasuredAvailableAfter = 500L,
        };

        Assert.Equal(0L, result.MeasuredAvailableDelta);
    }

    #endregion

    #region MemoryAreaResult.Success mapping

    [Theory]
    [InlineData(MemoryOperationStatus.Success, true)]
    [InlineData(MemoryOperationStatus.PartialSuccess, true)]
    [InlineData(MemoryOperationStatus.NoApplicableTargets, false)]
    [InlineData(MemoryOperationStatus.AccessDenied, false)]
    [InlineData(MemoryOperationStatus.Unsupported, false)]
    [InlineData(MemoryOperationStatus.Failed, false)]
    public void MemoryAreaResult_Success_MapsCorrectly(MemoryOperationStatus status, bool expected)
    {
        var result = new MemoryAreaResult { Status = status };
        Assert.Equal(expected, result.Success);
    }

    #endregion

    #region MemoryInfo calculations

    [Theory]
    [InlineData(100L, 0L, 100.0)]         // fully used
    [InlineData(100L, 100L, 0.0)]         // nothing used
    [InlineData(0L, 0L, 0.0)]             // total 0 → guard against divide by zero
    [InlineData(100L, 50L, 50.0)]         // half used
    [InlineData(16788910080L, 7747665920L, 53.852478314065756)] // real-world values
    public void MemoryInfo_UsedPercent_CalculatesCorrectly(long total, long available, double expected)
    {
        var info = new MemoryInfo
        {
            TotalPhysicalBytes = total,
            AvailablePhysicalBytes = available,
        };

        Assert.Equal(expected, info.UsedPercent, precision: 10);
    }

    [Fact]
    public void MemoryInfo_UsedPhysicalBytes_TotalMinusAvailable()
    {
        var info = new MemoryInfo
        {
            TotalPhysicalBytes = 1000L,
            AvailablePhysicalBytes = 300L,
        };

        Assert.Equal(700L, info.UsedPhysicalBytes);
    }

    [Fact]
    public void MemoryInfo_UsedPhysicalBytes_NeverNegative()
    {
        // Available can't exceed Total in practice, but guard the clamp:
        var info = new MemoryInfo
        {
            TotalPhysicalBytes = 100L,
            AvailablePhysicalBytes = 200L, // impossible but tests the clamp
        };

        Assert.Equal(0L, info.UsedPhysicalBytes);
    }

    #endregion

    #region MemoryTargetType coverage

    [Fact]
    public void MemoryTargetType_AllDefinedValuesAreCoveredBySnapshots()
    {
        var areas = MemoryOptimizerService.GetMemoryAreaSnapshots();

        foreach (MemoryTargetType t in Enum.GetValues<MemoryTargetType>())
        {
            Assert.True(areas.Any(a => a.Target == t),
                $"Target {t} missing from memory area snapshots");
            var area = areas.Single(a => a.Target == t);
            Assert.False(string.IsNullOrWhiteSpace(area.DisplayName), $"Missing DisplayName for {t}");
            Assert.False(string.IsNullOrWhiteSpace(area.SafetyBadge), $"Missing SafetyBadge for {t}");
        }
    }

    [Fact]
    public void MemoryAreaSnapshot_SafetyBadges_DoNotClaimUnconditionalSafety()
    {
        // A target must never be described as safe merely because it can technically be purged.
        string[] forbidden = { "100% SAFE", "SAFE TO PURGE", "SAFE", "GENTLE TRIM" };
        var areas = MemoryOptimizerService.GetMemoryAreaSnapshots();

        foreach (var area in areas)
        {
            foreach (var f in forbidden)
            {
                Assert.False(area.SafetyBadge.Contains(f, StringComparison.OrdinalIgnoreCase),
                    $"Target {area.Target} uses the unconditional claim '{f}' in badge '{area.SafetyBadge}'.");
            }
            Assert.False(string.IsNullOrWhiteSpace(area.SafetyBadge));
        }
    }

    [Fact]
    public void MemoryOptimizerService_DefaultActiveTargetTypes_NonEmpty()
    {
        var defaults = MemoryOptimizerService.DefaultActiveTargetTypes();
        Assert.NotEmpty(defaults);
        // StandbyListLowPriority is intentionally NOT in defaults (gentler option, user-selectable)
        Assert.DoesNotContain(MemoryTargetType.StandbyListLowPriority, defaults);
    }

    [Fact]
    public void MemoryOptimizationResult_CountsPartitionAreasWithoutLossOrOverlap()
    {
        var result = new MemoryOptimizationResult
        {
            RequestedTargetCount = 2,
            AreaResults = new List<MemoryAreaResult>
            {
                new() { Target = MemoryTargetType.WorkingSet, Status = MemoryOperationStatus.Success },
                new() { Target = MemoryTargetType.StandbyList, Status = MemoryOperationStatus.AccessDenied },
            }
        };

        Assert.Equal(1, result.SucceededTargetCount);
        Assert.Equal(1, result.FailedTargetCount);
        Assert.Equal(result.AreaResults.Count, result.SucceededTargetCount + result.FailedTargetCount);
    }

    [Fact]
    public void MemoryOptimizationResult_MeasuredAvailableDelta_ClampedAtZeroWhenAvailableDrops()
    {
        // Available RAM going down does not mean negative bytes were freed.
        var result = new MemoryOptimizationResult
        {
            MeasuredAvailableBefore = 8_000_000_000,
            MeasuredAvailableAfter = 7_900_000_000,
        };

        Assert.Equal(0L, result.MeasuredAvailableDelta);
    }

    [Fact]
    public void MemoryOptimizationResult_ExposesNoPerAreaByteTotal()
    {
        // Guards against reintroducing a "bytes freed per area" figure. Per-area byte counts
        // cannot be attributed honestly: the delta is only observable across the whole run.
        var area = new MemoryAreaResult { Target = MemoryTargetType.StandbyList };
        var t = typeof(MemoryAreaResult);

        Assert.Null(t.GetProperty("MeasuredBytesFreed"));
        Assert.Null(t.GetProperty("FormattedFreed"));
        Assert.NotNull(t.GetProperty("ErrorMessage"));
        Assert.Equal(MemoryTargetType.StandbyList, area.Target);
    }

    #endregion
}
