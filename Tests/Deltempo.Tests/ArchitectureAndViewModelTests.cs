using System;
using System.IO;
using System.Threading.Tasks;
using WinTempCleaner.Core.Abstractions;
using WinTempCleaner.ViewModels;
using Xunit;

namespace Deltempo.Tests;

public class ArchitectureAndViewModelTests
{
    [Fact]
    public void SystemClock_ReturnsAccurateCurrentTime()
    {
        ISystemClock clock = SystemClock.Instance;
        var now = clock.UtcNow;
        var systemNow = DateTime.UtcNow;

        Assert.True(Math.Abs((now - systemNow).TotalSeconds) < 2);
    }

    [Fact]
    public void PhysicalFileSystem_BasicOperationsWorkOnSandbox()
    {
        IFileSystem fs = PhysicalFileSystem.Instance;
        string tempDir = Path.Combine(Path.GetTempPath(), "Deltempo_FsTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string tempFile = Path.Combine(tempDir, "test.txt");
            File.WriteAllText(tempFile, "Hello World");

            Assert.True(fs.DirectoryExists(tempDir));
            Assert.True(fs.FileExists(tempFile));
            Assert.Equal(11, fs.GetFileSize(tempFile));
            Assert.True(Math.Abs((fs.GetLastWriteTimeUtc(tempFile) - DateTime.UtcNow).TotalMinutes) < 1);

            var files = fs.EnumerateFiles(tempDir);
            Assert.Single(files);

            fs.DeleteFile(tempFile);
            Assert.False(fs.FileExists(tempFile));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void WindowsRegistryProvider_ReadSafelyHandlesMissingKeys()
    {
        IRegistryProvider reg = WindowsRegistryProvider.Instance;
        var val = reg.GetCurrentUserValue(@"Software\NonExistentVendor_Deltempo_12345", "NonExistentKey");
        Assert.Null(val);
    }

    [Fact]
    public void MemoryOptimizationViewModel_InitializesAndRefreshes()
    {
        var vm = new MemoryOptimizationViewModel();

        Assert.NotNull(vm.BoostCommand);
        Assert.True(vm.BoostCommand.CanExecute(null));
        Assert.False(vm.IsBoosting);
        Assert.NotEmpty(vm.BoostButtonText);

        vm.RefreshTelemetry();
        Assert.True(vm.UsedPercent >= 0 && vm.UsedPercent <= 100);
        Assert.NotEmpty(vm.FormattedDetail);
    }

    [Fact]
    public async Task MemoryOptimizationViewModel_ExecuteBoost_CompletesAndFiresLogs()
    {
        var vm = new MemoryOptimizationViewModel();
        bool logReceived = false;
        vm.LogRequested += (msg, level) =>
        {
            if (!string.IsNullOrEmpty(msg)) logReceived = true;
        };

        var res = await vm.ExecuteBoostAsync();

        Assert.NotNull(res);
        Assert.True(logReceived);
        Assert.False(vm.IsBoosting);
        Assert.Equal("Quick Boost", vm.BoostButtonText);
    }
}
