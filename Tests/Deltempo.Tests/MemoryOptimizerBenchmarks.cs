using System.Diagnostics;
using WinTempCleaner.Services;
using Xunit;
using Xunit.Abstractions;

namespace Deltempo.Tests;

/// <summary>
/// Memory engine benchmarks and native execution measurements.
/// Excluded from normal CI via [Trait("Category", "Benchmark")].
/// Assertions test functional correctness, not timing thresholds.
/// </summary>
public class MemoryOptimizerBenchmarks
{
    private readonly ITestOutputHelper _output;

    public MemoryOptimizerBenchmarks(ITestOutputHelper output) => _output = output;

    [Fact]
    [Trait("Category", "Benchmark")]
    public async Task MemoryOptimizer_TelemetrySnapshot_ExecutesAndMeasuresUsage()
    {
        var memInfo = MemoryOptimizerService.GetMemoryInfo();
        var areas = MemoryOptimizerService.GetMemoryAreaSnapshots();

        Assert.NotNull(memInfo);
        Assert.NotEmpty(areas);
        Assert.True(memInfo.TotalPhysicalBytes > 0, "Expected TotalPhysicalBytes > 0");
        Assert.True(memInfo.AvailablePhysicalBytes > 0, "Expected AvailablePhysicalBytes > 0");
        Assert.True(memInfo.AvailablePhysicalBytes <= memInfo.TotalPhysicalBytes, "Available RAM must not exceed total RAM");

        _output.WriteLine($"Total Physical: {memInfo.FormattedTotal}");
        _output.WriteLine($"Available Physical: {memInfo.FormattedAvailable}");
        _output.WriteLine($"System Usage: {memInfo.UsedPercent:F1}%");

        foreach (var area in areas)
        {
            _output.WriteLine($"Area: {area.DisplayName} | Current: {area.FormattedCurrent} | Safety: {area.SafetyBadge}");
            Assert.False(string.IsNullOrWhiteSpace(area.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(area.SafetyBadge));
        }

        var sw = Stopwatch.StartNew();
        var result = await MemoryOptimizerService.OptimizeRamAsync(new[] { MemoryTargetType.WorkingSet });
        sw.Stop();

        _output.WriteLine($"WorkingSet Optimize Elapsed: {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Reclaimed: {result.FormattedReclaimed} | Delta: {result.FormattedMeasuredDelta}");
        _output.WriteLine($"Processes Trimmed: {result.ProcessesOptimized}");

        Assert.NotNull(result);
        Assert.True(result.ExecutionTimeMs >= 0);
    }
}
