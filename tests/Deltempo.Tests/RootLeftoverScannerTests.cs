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

    /// <summary>
    /// A bare drive letter is DRIVE-RELATIVE, not the drive root. Windows resolves it
    /// against the current directory on that drive, so Path.GetFullPath("C:") yields the
    /// working directory when the app runs from C: and the drive root when it runs from
    /// D:. The guard used to canonicalize first and judge the result, which meant it
    /// refused "C:" on a D: machine and APPROVED it - the working directory - on a C:
    /// machine. These assertions are written against the CURRENT drive so they hold
    /// wherever the suite runs, which is the whole point: the original test hardcoded
    /// "C:" and therefore only passed on a machine whose current drive was not C:.
    /// </summary>
    [Fact]
    public void IsSafeToDeleteResidual_CurrentDriveForms_AreRefused()
    {
        string currentRoot = Path.GetPathRoot(Environment.CurrentDirectory) ?? "C:\\";
        string driveLetter = currentRoot.Length >= 2 ? currentRoot.Substring(0, 2) : "C:";

        // The absolute root of the current drive.
        Assert.False(InstalledAppService.IsSafeToDeleteResidual(currentRoot));

        // The bare drive letter - the form that failed open.
        Assert.False(InstalledAppService.IsSafeToDeleteResidual(driveLetter));

        // A drive-relative path that also carries a directory component.
        Assert.False(InstalledAppService.IsSafeToDeleteResidual(driveLetter + "Windows"));
    }

    /// <summary>
    /// Drive-relative forms are refused on EVERY drive, not just the current one, and
    /// on every platform layout. "C:Windows" resolves to C:\Windows on a D: machine and
    /// to a directory beside the working directory on a C: machine - the ambiguity is
    /// the hazard, so the form is refused outright rather than resolved.
    /// </summary>
    [Theory]
    [InlineData("C:")]
    [InlineData("C:Windows")]
    [InlineData("C:Program Files")]
    [InlineData("D:")]
    [InlineData("D:Windows")]
    [InlineData("Z:")]
    public void IsSafeToDeleteResidual_DriveRelativeForms_AreRefused(string path)
    {
        Assert.False(InstalledAppService.IsSafeToDeleteResidual(path));
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

    [Fact]
    public void InstalledAppItem_StoreApp_DetectsStoreEngine()
    {
        var app = new InstalledAppItem
        {
            DisplayName = "Spotify Music",
            IsWindowsStoreApp = true,
            PackageFullName = "SpotifyAB.SpotifyMusic_1.2.3.4_x64__zpdnekdrzrea0"
        };

        Assert.Equal("MSIX / Store", app.UninstallEngine);
        Assert.True(app.HasUninstaller);
        Assert.False(app.IsBroken);
    }

    [Theory]
    [InlineData(@"C:\Program Files\Common Files")]
    [InlineData(@"C:\Program Files (x86)\Common Files")]
    [InlineData(@"C:\Users\User\AppData\Local\Microsoft")]
    [InlineData(@"C:\Windows\System32\drivers")]
    public void IsSafeToDeleteResidual_AdditionalProtectedPaths_ReturnsFalse(string path)
    {
        bool isSafe = InstalledAppService.IsSafeToDeleteResidual(path);
        Assert.False(isSafe);
    }

    [Fact]
    public async Task SystemRestorePointService_WithEmptyDescription_DoesNotCrash()
    {
        // Calling CreateRestorePointAsync with empty string should safely handle default description
        // without throwing exceptions even if system restore service is disabled on CI/environment.
        var result = await WinTempCleaner.Core.Safety.SystemRestorePointService.CreateRestorePointAsync("");
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task CreateSnapshotAsync_WithNonExistentPath_ReturnsEmptySnapshot()
    {
        var app = new InstalledAppItem
        {
            DisplayName = "SnapshotTestApp",
            InstallLocation = @"C:\NonExistent_Snapshot_Dir_123"
        };

        var snapshot = await RootLeftoverScannerService.CreateSnapshotAsync(app);
        Assert.NotNull(snapshot);
        Assert.Equal("SnapshotTestApp", snapshot.AppName);
        Assert.Empty(snapshot.ExistingFiles);
    }

    [Fact]
    public async Task ScanAppTraces_WithSnapshot_IdentifiesDifferentialResiduals()
    {
        // Create a temporary directory with a test file
        string tempDir = Path.Combine(Path.GetTempPath(), $"DeltempoSnapTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string testFile = Path.Combine(tempDir, "unremoved_component.dll");
        File.WriteAllText(testFile, "test-content");

        try
        {
            var app = new InstalledAppItem
            {
                DisplayName = "SnapResidualApp",
                InstallLocation = tempDir
            };

            var snapshot = new AppTraceSnapshot
            {
                AppName = app.DisplayName,
                InstallLocation = app.InstallLocation
            };
            snapshot.ExistingFiles.Add(testFile);

            var scanResult = await RootLeftoverScannerService.ScanAppTracesAsync(app, snapshot);

            Assert.NotNull(scanResult);
            Assert.Contains(scanResult.Items, i => string.Equals(i.PathOrKey, testFile, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void LeftoverItem_EnvironmentPath_PropertiesAndBadgesAreCorrect()
    {
        var item = new LeftoverItem
        {
            Type = LeftoverType.EnvironmentPath,
            PathOrKey = @"HKCU\Environment",
            SubKeyOrValueName = "Path",
            TargetPath = @"C:\Program Files\TestApp\bin",
            Description = "PATH Variable Orphan"
        };

        Assert.Equal("Environment PATH", item.TypeBadge);
        Assert.Equal("\uE756", item.IconGlyph);
        Assert.Equal(@"C:\Program Files\TestApp\bin", item.TargetPath);
    }

    [Fact]
    public async Task PurgeLeftovers_EnvironmentPath_HandlesEmptyOrMissingGracefully()
    {
        var items = new List<LeftoverItem>
        {
            new LeftoverItem
            {
                Type = LeftoverType.EnvironmentPath,
                PathOrKey = @"HKCU\NonExistentSubKey_123",
                SubKeyOrValueName = "Path",
                TargetPath = @"C:\Fake\Path",
                IsSelected = true
            }
        };

        var result = await RootLeftoverPurgeService.PurgeLeftoversAsync(items);
        Assert.NotNull(result);
        Assert.Equal(0, result.ItemsPurgedCount);
    }
}
