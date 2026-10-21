using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WinTempCleaner.Models;
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

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var sandbox = Path.Combine(Path.GetTempPath(), "Deltempo_DeepClean_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);
        try
        {
            var dummyTarget = new TargetFolderInfo
            {
                Id = "test_dummy",
                Name = "Test Dummy Target",
                Description = "Test Description",
                IconGlyph = "Folder",
                FolderPath = sandbox,
                HasAccess = true,
                IsSelected = true
            };

            var result = await DeepCleanEngine.ExecuteDeepCleanAsync(
                logAction: (msg, lvl) => { },
                progress: progress,
                purgeAllRestorePoints: false,
                skipDism: true,
                targets: new[] { dummyTarget },
                ct: cts.Token);

            Assert.NotNull(result);
            Assert.True(result.Duration >= TimeSpan.Zero);
            Assert.True(result.CategoriesProcessed >= 1);
            Assert.NotEmpty(reports);
        }
        finally
        {
            try { Directory.Delete(sandbox, true); } catch { }
        }
    }
}
