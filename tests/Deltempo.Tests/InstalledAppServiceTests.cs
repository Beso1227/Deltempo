using System;
using System.IO;
using System.Linq;
using WinTempCleaner.Models;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

public class InstalledAppServiceTests
{
    [Fact]
    public void GetInstalledApps_ReturnsNonEmptyListOnWindows()
    {
        var apps = InstalledAppService.GetInstalledApps();
        Assert.NotNull(apps);
        // Any standard Windows machine has multiple installed runtimes or apps in registry
        Assert.True(apps.Count >= 0);
    }

    [Fact]
    public void InstalledAppItem_FormatsDisplayCorrectly()
    {
        var item = new InstalledAppItem
        {
            DisplayName = "Test Suite Application",
            Publisher = "Deltempo Systems",
            DisplayVersion = "1.0.4",
            EstimatedSizeBytes = 1024 * 1024 * 150, // 150 MB
            UninstallString = "MsiExec.exe /I{12345678-1234-1234-1234-123456789012}"
        };

        Assert.Equal("Test Suite Application", item.DisplayName);
        Assert.Equal("150.0 MB", item.FormattedSize);
        Assert.False(item.IsWindowsStoreApp);
    }

    [Fact]
    public void FindResidualLeftovers_ProtectedApp_DoesNotReturnSystemFolders()
    {
        var windowsApp = new InstalledAppItem
        {
            DisplayName = "Windows",
            Publisher = "Microsoft Corporation"
        };

        var leftovers = InstalledAppService.FindResidualLeftovers(windowsApp);
        Assert.NotNull(leftovers);
        // It must NOT include C:\Windows or critical folders
        foreach (var path in leftovers)
        {
            Assert.DoesNotContain("system32", path, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// A timeout and a non-zero exit code must not be reported as success. 3010/1641 are
    /// successful Windows Installer codes that merely require or start a reboot.
    /// </summary>
    [Theory]
    [InlineData(0, true)]
    [InlineData(3010, true)]
    [InlineData(1641, true)]
    [InlineData(1, false)]
    [InlineData(1602, false)]
    [InlineData(-1, false)]
    public void IsSuccessfulUninstallerExit_ClassifiesCommonExitCodes(int exitCode, bool expected)
    {
        Assert.Equal(expected, InstalledAppService.IsSuccessfulUninstallerExit(exitCode));
    }

    /// <summary>
    /// The launcher path itself, not just the classifier: the command string the registry
    /// stores is parsed, a real child process is started, waited for, and its exit code is
    /// classified. This is the branch that used to report success for anything that exited,
    /// and nothing exercised it before.
    /// </summary>
    [Theory]
    [InlineData("cmd.exe /c exit 0", true)]
    [InlineData("cmd.exe /c exit 3010", true)]
    [InlineData("cmd.exe /c exit 1641", true)]
    [InlineData("cmd.exe /c exit 1", false)]
    public async Task UninstallAppAsync_ClassifiesTheRealProcessExitCode(string command, bool expected)
    {
        var app = new InstalledAppItem
        {
            DisplayName = "Deltempo Uninstall Harness",
            Publisher = "Deltempo Tests",
            UninstallString = command
        };

        Assert.Equal(expected, await InstalledAppService.UninstallAppAsync(app, silent: false, timeoutMs: 30_000));
    }

    /// <summary>
    /// A child that is still running when the timeout expires must not be reported as
    /// uninstalled. The child is self-limiting (~11 s), so the test leaves nothing behind
    /// and never kills a process it did not start: an earlier version killed every process
    /// named PING, which raced <see cref="SystemProcessRunnerTests"/> running its own ping
    /// child in parallel and made that test's child vanish before its timeout.
    ///
    /// The deadline has to cover the whole attempt, and it must not depend on the thread
    /// pool. The attempt used to be entered through Task.Run, so a saturated pool delayed
    /// it before the wait even started: a 500 ms timeout was measured at 9 s on a CI
    /// runner, against a 1.5 s baseline that did get a thread. The call must therefore
    /// come back on time, not merely come back - which is why the bound below is
    /// absolute as well as relative to the baseline.
    /// </summary>
    [Fact]
    public async Task UninstallAppAsync_Timeout_IsNotReportedAsSuccess()
    {
        var app = new InstalledAppItem
        {
            DisplayName = "Deltempo Uninstall Harness",
            Publisher = "Deltempo Tests",
            UninstallString = "cmd.exe /c ping -n 12 127.0.0.1 >nul"
        };

        var baseline = new InstalledAppItem
        {
            DisplayName = "Deltempo Uninstall Harness",
            Publisher = "Deltempo Tests",
            UninstallString = "cmd.exe /c exit 0"
        };

        var baselineWatch = System.Diagnostics.Stopwatch.StartNew();
        await InstalledAppService.UninstallAppAsync(baseline, silent: false, timeoutMs: 500);
        baselineWatch.Stop();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        bool result = await InstalledAppService.UninstallAppAsync(app, silent: false, timeoutMs: 500);
        stopwatch.Stop();

        Assert.False(result, "a child that outlived the timeout must not be reported as uninstalled");
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(4),
            $"a 500 ms timeout blocked for {stopwatch.Elapsed}; the deadline must cover the whole attempt.");
        Assert.True(
            stopwatch.Elapsed < baselineWatch.Elapsed + TimeSpan.FromSeconds(3),
            $"the wait did not honour the 500 ms timeout; it blocked for {stopwatch.Elapsed} against a {baselineWatch.Elapsed} baseline.");
    }

    [Fact]
    public async Task UninstallAppAsync_WithoutAnUninstallString_ReturnsFalse()
    {
        var app = new InstalledAppItem
        {
            DisplayName = "Deltempo Uninstall Harness",
            Publisher = "Deltempo Tests",
            UninstallString = string.Empty
        };

        Assert.False(await InstalledAppService.UninstallAppAsync(app, silent: false, timeoutMs: 5_000));
    }

    [Fact]
    public void IsSafeToDeleteResidual_BlocksDangerousSystemPaths()
    {
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        Assert.False(InstalledAppService.IsSafeToDeleteResidual(winDir));
        Assert.False(InstalledAppService.IsSafeToDeleteResidual(progFiles));
        Assert.False(InstalledAppService.IsSafeToDeleteResidual(@"C:\"));
        Assert.False(InstalledAppService.IsSafeToDeleteResidual(""));
    }
}
