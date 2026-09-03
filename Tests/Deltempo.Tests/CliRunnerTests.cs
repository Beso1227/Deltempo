using System.IO;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

public class CliRunnerTests : IDisposable
{
    private readonly string _testSandboxDir;

    public CliRunnerTests()
    {
        _testSandboxDir = Path.Combine(Path.GetTempPath(), "Deltempo_Cli_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testSandboxDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testSandboxDir))
            {
                Directory.Delete(_testSandboxDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task CliRunner_TestCommand_ReturnsZero()
    {
        int exitCode = await CliRunner.RunAsync(new[] { "test" });
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task CliRunner_HelpCommand_ReturnsZero()
    {
        int exitCode = await CliRunner.RunAsync(new[] { "help" });
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task CliRunner_StatusCommand_ReturnsZero()
    {
        int exitCode = await CliRunner.RunAsync(new[] { "status", "--json" });
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task CliRunner_StartupList_ReturnsZero()
    {
        int exitCode = await CliRunner.RunAsync(new[] { "startup", "--json" });
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task CliRunner_ScanDryRun_ReturnsZero()
    {
        int exitCode = await CliRunner.RunAsync(new[] { "scan", "--json" });
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task CliRunner_CleanDryRun_ReturnsZero()
    {
        int exitCode = await CliRunner.RunAsync(new[] { "clean", "--dry-run", "--json" });
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task CliRunner_LargeFilesInspect_NonExistentFile_ReturnsOne()
    {
        int exitCode = await CliRunner.RunAsync(new[] { "large", "inspect", @"C:\NonExistent_Fake_Deltempo_File.tmp" });
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task CliRunner_LargeFilesInspect_ValidFile_ReturnsZero()
    {
        string sampleFile = Path.Combine(_testSandboxDir, "stale_test_setup.exe");
        File.WriteAllBytes(sampleFile, new byte[1024]);

        int exitCode = await CliRunner.RunAsync(new[] { "large", "inspect", sampleFile, "--json" });
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task LargeFileHunter_CustomPathScan_DiscoversLargeFiles()
    {
        // Create 2 files: one small (100KB), one large (60MB)
        string smallFile = Path.Combine(_testSandboxDir, "small.txt");
        File.WriteAllBytes(smallFile, new byte[100 * 1024]);

        string bigFile = Path.Combine(_testSandboxDir, "big_archive.zip");
        // Create sparse or 55MB file
        using (var fs = new FileStream(bigFile, FileMode.Create, FileAccess.Write))
        {
            fs.SetLength(55L * 1024 * 1024);
        }

        var results = await LargeFileHunterService.ScanLargeFilesAsync(
            minSizeBytes: 50L * 1024 * 1024,
            targetScope: _testSandboxDir);

        Assert.NotEmpty(results);
        Assert.Contains(results, f => f.FileName == "big_archive.zip");
        Assert.DoesNotContain(results, f => f.FileName == "small.txt");
    }
}
