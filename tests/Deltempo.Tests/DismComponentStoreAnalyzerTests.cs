using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// The DISM component-store analysis is the only honest source for what Disk Cleanup
/// calls "Windows Update Cleanup". These tests pin the parse contract, including the
/// fail-closed behaviour when DISM prints an error instead of an analysis.
/// </summary>
public class DismComponentStoreAnalyzerTests
{
    private const string EnglishAnalysis = """
        Deployment Image Servicing and Management tool
        Version: 10.0.26100.1

        Image Version: 10.0.26100.1

        [==========================100.0%==========================]

        Component Store (WinSxS) information:

        Windows Explorer Reported Size of Component Store : 13.02 GB
        Actual Size of Component Store : 12.98 GB
            Shared with Windows : 8.11 GB
            Backups and Disabled Features : 4.21 GB
            Cache and Temporary Data : 680.44 MB

        Date of Last Cleanup : 2026-10-14 09:31:22

        Number of Reclaimable Packages : 7
        Component Store Cleanup Recommended : Yes

        The operation completed successfully.
        """;

    [Fact]
    public void Parse_EnglishAnalysis_ReadsEveryField()
    {
        var analysis = DismComponentStoreAnalyzer.Parse(EnglishAnalysis);

        Assert.NotNull(analysis);
        Assert.True(analysis!.HasData);
        Assert.Equal(7, analysis.ReclaimablePackageCount);
        Assert.True(analysis.CleanupRecommended);
        Assert.NotNull(analysis.LastCleanupUtc);
        Assert.Equal(new DateTime(2026, 10, 14), analysis.LastCleanupUtc!.Value.Date);

        void AssertSize(long actual, double expected)
            => Assert.InRange(actual, (long)expected - 4096, (long)expected + 4096);

        AssertSize(analysis.ActualSizeBytes, 12.98 * 1024 * 1024 * 1024);
        AssertSize(analysis.SharedWithWindowsBytes, 8.11 * 1024 * 1024 * 1024);
        AssertSize(analysis.BackupsAndDisabledFeaturesBytes, 4.21 * 1024 * 1024 * 1024);
        AssertSize(analysis.CacheAndTemporaryDataBytes, 680.44 * 1024 * 1024);
    }

    [Fact]
    public void Parse_SizeOnlyOutput_LeavesCountsAtZero()
    {
        var analysis = DismComponentStoreAnalyzer.Parse("""
            Actual Size of Component Store : 9.00 GB
            Backups and Disabled Features : 1.50 GB
            Component Store Cleanup Recommended : No
            """);

        Assert.NotNull(analysis);
        Assert.Equal(0, analysis!.ReclaimablePackageCount);
        Assert.False(analysis.CleanupRecommended);
        Assert.Equal((long)(1.5 * 1024 * 1024 * 1024), analysis.EstimatedReclaimableBytes);
    }

    [Fact]
    public void Parse_ErrorOutput_ReturnsNullInsteadOfZeroes()
    {
        // This is exactly what a non-elevated run prints. Returning a zero analysis would
        // let the UI claim "Clean (0 B)" for a store it never measured.
        var analysis = DismComponentStoreAnalyzer.Parse("""
            Error: 740

            Elevated permissions are required to run DISM.
            Use an elevated command prompt to complete these tasks.
            """);

        Assert.Null(analysis);
    }

    [Theory]
    [InlineData("9.57 GB", 9.57 * 1024 * 1024 * 1024)]
    [InlineData("680.44 MB", 680.44 * 1024 * 1024)]
    [InlineData("1.25 TB", 1.25 * 1024 * 1024 * 1024 * 1024)]
    [InlineData("512 KB", 512 * 1024)]
    [InlineData("42 B", 42)]
    public void ParseSize_HandlesEveryUnitDismEmits(string text, double expectedBytes)
    {
        // 4 KB tolerance: the attribute's compile-time product and the parser's
        // multiplier can differ by one floating-point ULP before truncation.
        long actual = DismComponentStoreAnalyzer.ParseSize(text);
        Assert.InRange(actual, (long)expectedBytes - 4096, (long)expectedBytes + 4096);
    }

    [Fact]
    public void EstimatedReclaimableBytes_SumsBackupsAndCache()
    {
        var analysis = new ComponentStoreAnalysis(
            ActualSizeBytes: 12L * 1024 * 1024 * 1024,
            SharedWithWindowsBytes: 8L * 1024 * 1024 * 1024,
            BackupsAndDisabledFeaturesBytes: 3L * 1024 * 1024 * 1024,
            CacheAndTemporaryDataBytes: 512L * 1024 * 1024,
            ReclaimablePackageCount: 3,
            CleanupRecommended: true,
            LastCleanupUtc: null);

        Assert.Equal((3L * 1024 * 1024 * 1024) + (512L * 1024 * 1024), analysis.EstimatedReclaimableBytes);
    }
}
