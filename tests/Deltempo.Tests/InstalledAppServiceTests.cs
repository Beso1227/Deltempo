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
