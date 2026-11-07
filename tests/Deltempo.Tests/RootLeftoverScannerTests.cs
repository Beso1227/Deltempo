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
        // Create a temporary app-owned directory with a test file. The folder is named after
        // the app because InstallLocation alone never proves ownership.
        string tempRoot = Path.Combine(Path.GetTempPath(), $"DeltempoSnapTest_{Guid.NewGuid():N}");
        string tempDir = Path.Combine(tempRoot, "SnapResidualApp");
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
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
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

    /// <summary>
    /// Regression: a user installs several unrelated apps into one folder (D:\Setups). The
    /// registry InstallLocation points at that shared folder, so the scanner used to propose
    /// the folder itself as "Main Installation Directory" and every file the uninstaller left
    /// behind as a High-confidence residual. Select all + purge then deleted the whole folder.
    /// The scan must propose only entries whose names prove they belong to the app.
    /// </summary>
    [Fact]
    public async Task ScanAppTraces_SharedInstallContainer_ProposesOnlyAppOwnedEntries()
    {
        string container = Path.Combine(Path.GetTempPath(), $"DeltempoSharedContainer_{Guid.NewGuid():N}");
        string appDir = Path.Combine(container, "Contoso Widget");
        string appPayload = Path.Combine(appDir, "widget.dll");
        string appInstaller = Path.Combine(container, "Contoso-Widget-Setup.exe");
        string unrelatedInstaller = Path.Combine(container, "OtherApp-Setup.exe");
        string unrelatedNotes = Path.Combine(container, "notes.txt");

        Directory.CreateDirectory(appDir);
        File.WriteAllText(appPayload, "payload");
        File.WriteAllText(appInstaller, "installer");
        File.WriteAllText(unrelatedInstaller, "unrelated");
        File.WriteAllText(unrelatedNotes, "notes");

        try
        {
            var app = new InstalledAppItem
            {
                DisplayName = "Contoso Widget",
                InstallLocation = container
            };

            var snapshot = new AppTraceSnapshot
            {
                AppName = app.DisplayName,
                InstallLocation = container
            };
            snapshot.ExistingFiles.Add(appPayload);
            snapshot.ExistingFiles.Add(appInstaller);
            snapshot.ExistingFiles.Add(unrelatedInstaller);
            snapshot.ExistingFiles.Add(unrelatedNotes);

            var result = await RootLeftoverScannerService.ScanAppTracesAsync(app, snapshot);

            Assert.DoesNotContain(result.Items, i => PathEquals(i.PathOrKey, container));
            Assert.Contains(result.Items, i => PathEquals(i.PathOrKey, appDir));
            Assert.Contains(result.Items, i => PathEquals(i.PathOrKey, appPayload));
            Assert.Contains(result.Items, i => PathEquals(i.PathOrKey, appInstaller));
            Assert.DoesNotContain(result.Items, i => PathEquals(i.PathOrKey, unrelatedInstaller));
            Assert.DoesNotContain(result.Items, i => PathEquals(i.PathOrKey, unrelatedNotes));
        }
        finally
        {
            if (Directory.Exists(container))
            {
                Directory.Delete(container, true);
            }
        }
    }

    /// <summary>
    /// Defense in depth for the same regression: even if a stale or hand-built list still
    /// contains the shared container and an unrelated installer, purge must refuse them and
    /// delete only the app-named folder.
    /// </summary>
    [Fact]
    public async Task PurgeLeftovers_SharedInstallContainer_RefusesContainerAndUnrelatedFiles()
    {
        string container = Path.Combine(Path.GetTempPath(), $"DeltempoPurgeShared_{Guid.NewGuid():N}");
        string appDir = Path.Combine(container, "Contoso Widget");
        string appPayload = Path.Combine(appDir, "widget.dll");
        string unrelatedInstaller = Path.Combine(container, "OtherApp-Setup.exe");

        Directory.CreateDirectory(appDir);
        File.WriteAllText(appPayload, "payload");
        File.WriteAllText(unrelatedInstaller, "unrelated");

        try
        {
            var app = new InstalledAppItem
            {
                DisplayName = "Contoso Widget",
                InstallLocation = container
            };

            var items = new List<LeftoverItem>
            {
                new LeftoverItem { Type = LeftoverType.Directory, PathOrKey = container, IsSelected = true },
                new LeftoverItem { Type = LeftoverType.File, PathOrKey = unrelatedInstaller, IsSelected = true },
                new LeftoverItem { Type = LeftoverType.Directory, PathOrKey = appDir, IsSelected = true }
            };

            var result = await RootLeftoverPurgeService.PurgeLeftoversAsync(items, app);

            Assert.True(Directory.Exists(container), "the shared install container must survive");
            Assert.True(File.Exists(unrelatedInstaller), "unrelated installers must survive");
            Assert.False(Directory.Exists(appDir), "the app-named folder should still be purged");
            Assert.True(result.ErrorsCount >= 2);
            Assert.True(result.ItemsPurgedCount >= 1);
        }
        finally
        {
            if (Directory.Exists(container))
            {
                Directory.Delete(container, true);
            }
        }
    }

    /// <summary>
    /// The ownership guard must not block a folder that really is the app's own install
    /// directory, otherwise legitimate leftover cleanup stops working.
    /// </summary>
    [Fact]
    public async Task PurgeLeftovers_AppOwnedInstallFolder_IsPurged()
    {
        string root = Path.Combine(Path.GetTempPath(), $"DeltempoOwnedPurge_{Guid.NewGuid():N}");
        string appDir = Path.Combine(root, "Contoso Widget");
        Directory.CreateDirectory(appDir);
        File.WriteAllText(Path.Combine(appDir, "widget.dll"), "payload");

        try
        {
            var app = new InstalledAppItem
            {
                DisplayName = "Contoso Widget",
                InstallLocation = appDir
            };

            var items = new List<LeftoverItem>
            {
                new LeftoverItem { Type = LeftoverType.Directory, PathOrKey = appDir, IsSelected = true }
            };

            var result = await RootLeftoverPurgeService.PurgeLeftoversAsync(items, app);

            Assert.False(Directory.Exists(appDir));
            Assert.Equal(1, result.ItemsPurgedCount);
            Assert.Equal(0, result.ErrorsCount);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Theory]
    [InlineData("PDFgear 1.2.3", "PDFgear", true)]
    [InlineData("PDFgear 1.2.3", "PDFgearPortable", true)]
    [InlineData("PDFgear 1.2.3", "Setups", false)]
    [InlineData("Adobe Acrobat Reader", "Adobe", false)]
    public void InstallLocationOwnership_DirectoryOwnership_IsNameBased(
        string displayName, string folderName, bool expected)
    {
        string parent = Path.Combine(Path.GetTempPath(), $"DeltempoOwnership_{Guid.NewGuid():N}");
        string candidate = Path.Combine(parent, folderName);
        var app = new InstalledAppItem { DisplayName = displayName };

        try
        {
            Assert.Equal(expected, InstallLocationOwnership.IsDirectoryOwnedByApp(app, candidate));
        }
        finally
        {
            if (Directory.Exists(parent))
            {
                Directory.Delete(parent, true);
            }
        }
    }

    private static bool PathEquals(string left, string right)
        => string.Equals(
            Path.GetFullPath(left).TrimEnd('\\', '/'),
            Path.GetFullPath(right).TrimEnd('\\', '/'),
            StringComparison.OrdinalIgnoreCase);
}
