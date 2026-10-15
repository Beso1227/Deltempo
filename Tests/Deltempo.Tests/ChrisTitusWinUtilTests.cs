using System.Diagnostics;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Deltempo links to CTT WinUtil; it must never download and execute it.
/// These tests are the regression guard for that decision, so they assert the
/// *absence* of the remote-code-execution shape rather than the presence of a command line.
/// </summary>
public class ChrisTitusWinUtilTests
{
    [Fact]
    public void CreateChrisTitusBrowseStartInfo_OpensAnHttpsUrlInTheDefaultBrowser()
    {
        ProcessStartInfo psi = SystemRepairService.CreateChrisTitusBrowseStartInfo();

        Assert.NotNull(psi);
        Assert.Equal("https://christitus.com/win", psi.FileName);
        Assert.StartsWith("https://", psi.FileName, StringComparison.Ordinal);
        Assert.True(psi.UseShellExecute);
    }

    [Fact]
    public void CreateChrisTitusBrowseStartInfo_NeverSpawnsAShellOrPipesRemoteScript()
    {
        ProcessStartInfo psi = SystemRepairService.CreateChrisTitusBrowseStartInfo();

        // The old implementation was: powershell.exe -NoProfile -ExecutionPolicy Bypass
        // -Command "irm https://christitus.com/win | iex" with Verb = runas.
        Assert.DoesNotContain("powershell", psi.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cmd.exe", psi.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("iex", psi.Arguments ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("irm", psi.Arguments ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ExecutionPolicy", psi.Arguments ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.True(string.IsNullOrWhiteSpace(psi.Arguments));
    }

    [Fact]
    public void CreateChrisTitusBrowseStartInfo_DoesNotRequestElevation()
    {
        ProcessStartInfo psi = SystemRepairService.CreateChrisTitusBrowseStartInfo();

        // Nothing Deltempo launches here should raise a UAC prompt, whether or not
        // the app is already elevated.
        Assert.NotEqual("runas", psi.Verb);
    }

    [Fact]
    public void ChrisTitusWinUtilUrl_IsPinnedToTheVendorSubdomain()
    {
        Assert.Equal("https://christitus.com/win", SystemRepairService.ChrisTitusWinUtilUrl);
    }
}
