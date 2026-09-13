using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WinTempCleaner.Models;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

public class RootLeftoverScannerTests
{
    [Fact]
    public async Task ScanAppTraces_WithEmptyApp_ReturnsEmptyResult()
    {
        var app = new InstalledAppItem
        {
            DisplayName = "",
            Publisher = ""
        };

        var result = await RootLeftoverScannerService.ScanAppTracesAsync(app);
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalSizeBytes);
    }

    [Theory]
    [InlineData("msiexec.exe /x {12345-6789}", "", "MSI")]
    [InlineData("C:\\Program Files\\App\\unins000.exe", "", "Inno Setup")]
    [InlineData("C:\\Program Files\\App\\uninstall.exe", "", "NSIS")]
    [InlineData("C:\\Program Files\\Common Files\\InstallShield\\Driver\\setup.exe", "", "InstallShield")]
    [InlineData("C:\\Users\\User\\AppData\\Local\\App\\Update.exe --uninstall", "", "Squirrel")]
    [InlineData("C:\\CustomApp\\remove.exe", "", "Standard")]
    public void InstalledAppItem_DetectsUninstallEngine(string uninstallStr, string quietStr, string expectedEngine)
    {
        var app = new InstalledAppItem
        {
            DisplayName = "Test App",
            UninstallString = uninstallStr,
            QuietUninstallString = quietStr
        };

        Assert.Equal(expectedEngine, app.UninstallEngine);
    }

    [Theory]
    [InlineData("C:\\Windows")]
    [InlineData("C:\\Windows\\System32")]
    [InlineData("C:\\Program Files")]
    [InlineData("C:\\Program Files (x86)")]
    [InlineData("C:\\")]
    [InlineData("C:")]
    [InlineData("")]
    public void IsSafeToDeleteResidual_ProtectedPaths_ReturnsFalse(string path)
    {
        bool isSafe = InstalledAppService.IsSafeToDeleteResidual(path);
        Assert.False(isSafe);
    }

    [Fact]
    public void IsSafeToDeleteResidual_SubApplicationFolder_ReturnsTrue()
    {
        string safePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TempDeltempoTestApp");
        bool isSafe = InstalledAppService.IsSafeToDeleteResidual(safePath);
        Assert.True(isSafe);
    }

    [Fact]
    public void CalculateDirectorySizeSafe_NonExistent_ReturnsZero()
    {
        long size = RootLeftoverScannerService.CalculateDirectorySizeSafe(@"C:\NonExistentDirectory_XYZ_12345");
        Assert.Equal(0, size);
    }

    [Fact]
    public async Task PurgeLeftovers_WithNoSelectedItems_ReturnsZeroReclaimed()
    {
        var items = new List<LeftoverItem>
        {
            new LeftoverItem
            {
                Type = LeftoverType.File,
                PathOrKey = @"C:\Fake\File.txt",
                SizeBytes = 1024,
                IsSelected = false
            }
        };

        var res = await RootLeftoverPurgeService.PurgeLeftoversAsync(items);
        Assert.Equal(0, res.ItemsPurgedCount);
        Assert.Equal(0, res.TotalReclaimedBytes);
    }
}
