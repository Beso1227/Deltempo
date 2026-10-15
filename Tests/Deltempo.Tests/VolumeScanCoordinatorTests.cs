using System.IO;
using WinTempCleaner.Core.Scanning;
using WinTempCleaner.Models;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Volume partitioning is a performance property that no existing test observed. It is also
/// easy to break silently: the scan is partitioned on TargetFolderInfo.FolderPath, and for
/// most cleanup scopes that field is a DISPLAY STRING rather than a path, so every one of
/// them resolved to the same "DEFAULT" partition and the whole scan ran at a single
/// partition's parallelism no matter how many drives were present.
/// </summary>
[Collection("SettingsService")]
public class VolumeScanCoordinatorTests
{
    private static TargetFolderInfo PoolTarget(string displayFolderPath) => new()
    {
        Id = displayFolderPath,
        Name = displayFolderPath,
        FolderPath = displayFolderPath
    };

    [Fact]
    public void ResolveVolumeKeyForTarget_RealPath_UsesThatDrive()
    {
        var target = PoolTarget(@"D:\Temp");

        string key = VolumeScanCoordinator.ResolveVolumeKeyForTarget(
            target, _ => Array.Empty<string>());

        Assert.Equal("D:", key);
    }

    [Theory]
    [InlineData("Browser Web Caches")]
    [InlineData("App Caches Pool")]
    [InlineData("VSS Shadow Storage (System Volume Information)")]
    [InlineData("Windows Logs Pool")]
    public void ResolveVolumeKeyForTarget_DisplayStringAlone_ResolvesThroughItsDirectories(string displayString)
    {
        // The display string carries no path root, so without directory resolution this
        // falls through to "DEFAULT" - which is how seventeen scopes ended up sharing one
        // partition and one parallelism cap.
        var target = PoolTarget(displayString);

        string key = VolumeScanCoordinator.ResolveVolumeKeyForTarget(
            target, _ => new[] { @"D:\Users\Someone\AppData\Local\Cache\thing" });

        Assert.Equal("D:", key);
    }

    [Fact]
    public void ResolveVolumeKeyForTarget_DisplayString_OnSystemDrive_ResolvesToC()
    {
        var target = PoolTarget("Browser Web Caches");

        string key = VolumeScanCoordinator.ResolveVolumeKeyForTarget(
            target, _ => new[] { @"C:\Users\Someone\AppData\Local\Google\Chrome\User Data" });

        Assert.Equal("C:", key);
    }

    /// <summary>
    /// The regression that matters: two display-string scopes on DIFFERENT drives must land
    /// in different partitions, not share "DEFAULT".
    /// </summary>
    [Fact]
    public void ResolveVolumeKeyForTarget_DistinctDrives_ProduceDistinctPartitions()
    {
        var browserCaches = PoolTarget("Browser Web Caches");
        var mobileDev = PoolTarget("Mobile & Dev Residuals Pool");

        string browserKey = VolumeScanCoordinator.ResolveVolumeKeyForTarget(
            browserCaches, _ => new[] { @"C:\Users\Someone\AppData\Local\Google\Chrome" });
        string mobileKey = VolumeScanCoordinator.ResolveVolumeKeyForTarget(
            mobileDev, _ => new[] { @"D:\Dev\dotnet" });

        Assert.Equal("C:", browserKey);
        Assert.Equal("D:", mobileKey);
        Assert.NotEqual(browserKey, mobileKey);
    }

    [Fact]
    public void ResolveVolumeKeyForTarget_ShellTarget_IsNeverMergedWithRealPaths()
    {
        var recycleBin = new TargetFolderInfo
        {
            Id = "RecycleBin",
            Name = "Recycle Bin",
            FolderPath = "Recycle Bin",
            IsSpecialShellTarget = true
        };

        string key = VolumeScanCoordinator.ResolveVolumeKeyForTarget(
            recycleBin, _ => new[] { @"C:\$Recycle.Bin" });

        Assert.Equal("VIRTUAL_SHELL", key);
    }

    [Fact]
    public void ResolveVolumeKeyForTarget_NoDirectories_DegradesToDefault()
    {
        var target = PoolTarget("Some Pool With Nothing Resolvable");

        string key = VolumeScanCoordinator.ResolveVolumeKeyForTarget(target, _ => Array.Empty<string>());

        Assert.Equal("DEFAULT", key);
    }

    [Fact]
    public void ResolveVolumeKeyForTarget_ThrowingResolver_FailsClosedToDefault()
    {
        var target = PoolTarget("Exploding Pool");

        string key = VolumeScanCoordinator.ResolveVolumeKeyForTarget(
            target, _ => throw new IOException("drive not ready"));

        // A resolver failure must not be reported as a real partition, and must not throw.
        Assert.Equal("DEFAULT", key);
    }

    [Fact]
    public void ResolveVolumeKeyForTarget_NullAndBlankDirectories_AreSkipped()
    {
        var target = PoolTarget("Mixed Pool");

        string key = VolumeScanCoordinator.ResolveVolumeKeyForTarget(
            target, _ => new[] { "", "   ", @"E:\Temp" });

        Assert.Equal("E:", key);
    }

    [Fact]
    public void ResolveVolumeKey_ShellAliases_MapToVirtualShell()
    {
        Assert.Equal("VIRTUAL_SHELL", VolumeScanCoordinator.ResolveVolumeKey("Recycle Bin"));
        Assert.Equal("VIRTUAL_SHELL", VolumeScanCoordinator.ResolveVolumeKey("VSS Shadow Storage"));
    }

    [Fact]
    public void ResolveVolumeKey_IsCaseInsensitiveOnTheDriveLetter()
    {
        Assert.Equal(
            VolumeScanCoordinator.ResolveVolumeKey(@"c:\temp"),
            VolumeScanCoordinator.ResolveVolumeKey(@"C:\TEMP"));
    }
}
