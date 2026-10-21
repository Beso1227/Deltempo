using System.Diagnostics;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// CTT WinUtil is a third-party PowerShell utility by Chris Titus that Deltempo launches on
/// the user's behalf. These tests pin the launch shape and, just as importantly, pin the
/// attribution: Deltempo bundles and elevates someone else's tool, so the credit is a
/// correctness requirement, not decoration.
/// </summary>
public class ChrisTitusWinUtilTests
{
    [Fact]
    public void CreateChrisTitusProcessStartInfo_LaunchesElevatedPowerShellAgainstTheVendorUrl()
    {
        ProcessStartInfo psi = SystemRepairService.CreateChrisTitusProcessStartInfo();

        Assert.NotNull(psi);
        Assert.Equal("powershell.exe", psi.FileName);
        Assert.True(psi.UseShellExecute);
        Assert.Contains("-ExecutionPolicy Bypass", psi.Arguments);
        Assert.Contains($"irm {SystemRepairService.ChrisTitusWinUtilUrl} | iex", psi.Arguments);
    }

    [Fact]
    public void CreateChrisTitusProcessStartInfo_HasProperElevationConfiguration()
    {
        ProcessStartInfo psi = SystemRepairService.CreateChrisTitusProcessStartInfo();

        // WinUtil performs system-level operations, so it must run elevated. When Deltempo
        // is already elevated there is nothing to request; otherwise ask for runas.
        if (ElevationService.IsAdministrator)
        {
            Assert.Equal(string.Empty, psi.Verb);
        }
        else
        {
            Assert.Equal("runas", psi.Verb);
        }
    }

    [Fact]
    public void Attribution_NamesChrisTitusAndLinksToHisWork()
    {
        Assert.Equal("Chris Titus", SystemRepairService.ChrisTitusAuthor);
        Assert.Equal("https://christitus.com", SystemRepairService.ChrisTitusAuthorUrl);
        Assert.Equal("https://christitus.com/win", SystemRepairService.ChrisTitusWinUtilUrl);

        string credit = SystemRepairService.ChrisTitusCreditLine;
        Assert.Contains("Chris Titus", credit, StringComparison.Ordinal);
        Assert.Contains("https://christitus.com/win", credit, StringComparison.Ordinal);
    }

    [Fact]
    public void Attribution_AndLaunchUrlNeverDriftApart()
    {
        // The credit line and the URL actually fetched must name the same destination, so a
        // future change to one cannot silently leave the other pointing somewhere else.
        Assert.Contains(SystemRepairService.ChrisTitusWinUtilUrl, SystemRepairService.ChrisTitusCreditLine, StringComparison.Ordinal);
        Assert.Contains(SystemRepairService.ChrisTitusWinUtilUrl, SystemRepairService.CreateChrisTitusProcessStartInfo().Arguments, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenChrisTitusWinUtilPage_OffersTheInspectFirstPath()
    {
        // Users who prefer to read the script before running it elevated get a browser path
        // to the exact same vendor page.
        ProcessStartInfo psi = SystemRepairService.CreateChrisTitusBrowseStartInfo();

        Assert.Equal(SystemRepairService.ChrisTitusWinUtilUrl, psi.FileName);
        Assert.StartsWith("https://", psi.FileName, StringComparison.Ordinal);
        Assert.True(psi.UseShellExecute);
    }
}
