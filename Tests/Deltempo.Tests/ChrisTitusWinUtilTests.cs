using System.Diagnostics;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

public class ChrisTitusWinUtilTests
{
    [Fact]
    public void CreateChrisTitusProcessStartInfo_ReturnsValidPowerShellExecution()
    {
        ProcessStartInfo psi = SystemRepairService.CreateChrisTitusProcessStartInfo();

        Assert.NotNull(psi);
        Assert.Equal("powershell.exe", psi.FileName);
        Assert.True(psi.UseShellExecute);
        Assert.Contains("-ExecutionPolicy Bypass", psi.Arguments);
        Assert.Contains("irm https://christitus.com/win | iex", psi.Arguments);
    }

    [Fact]
    public void CreateChrisTitusProcessStartInfo_HasProperElevationConfiguration()
    {
        ProcessStartInfo psi = SystemRepairService.CreateChrisTitusProcessStartInfo();

        if (ElevationService.IsAdministrator)
        {
            Assert.Equal(string.Empty, psi.Verb);
        }
        else
        {
            Assert.Equal("runas", psi.Verb);
        }
    }
}
