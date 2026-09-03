using System.Threading;
using System.Threading.Tasks;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

public class DeepCleanEngineTests
{
    [Fact]
    public void DeepCleanResult_CalculatesFormattedBytesAccurately()
    {
        var res = new DeepCleanResult
        {
            DiskFreedBytes = 1024L * 1024 * 1024 * 5, // 5 GB
            RamFreedBytes = 1024L * 1024 * 512,       // 512 MB
            FilesDeleted = 1500,
            CategoriesProcessed = 25
        };

        Assert.Equal("5.0 GB", res.FormattedDiskFreed);
        Assert.Equal("512.0 MB", res.FormattedRamFreed);
        Assert.Equal(1500, res.FilesDeleted);
        Assert.Equal(25, res.CategoriesProcessed);
    }

    [Fact]
    public async Task DeepCleanEngine_ExecutesPipelineWithProgressReports()
    {
        var reports = new List<DeepCleanProgress>();
        var progress = new Progress<DeepCleanProgress>(p => reports.Add(p));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var result = await DeepCleanEngine.ExecuteDeepCleanAsync(
            logAction: (msg, lvl) => { },
            progress: progress,
            purgeAllRestorePoints: false,
            ct: cts.Token);

        Assert.NotNull(result);
        Assert.True(result.Duration >= TimeSpan.Zero);
        Assert.True(result.CategoriesProcessed >= 10);
        Assert.NotEmpty(result.SummaryHighlights);
    }
}
