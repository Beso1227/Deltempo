using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

public class SystemCoreWhitelistTests
{
    [Theory]
    [InlineData("csrss")]
    [InlineData("csrss.exe")]
    [InlineData("CSRSS.EXE")]
    [InlineData("lsass")]
    [InlineData("lsass.exe")]
    [InlineData("services")]
    [InlineData("svchost")]
    [InlineData("explorer")]
    [InlineData("dwm")]
    [InlineData("smss")]
    [InlineData("wininit")]
    [InlineData("winlogon")]
    [InlineData("System")]
    [InlineData("Idle")]
    [InlineData("Deltempo")]
    [InlineData("wintempcleaner")]
    public void IsProtectedProcess_ProtectsVitalWindowsProcesses(string processName)
    {
        // Strip .exe if provided to match Process.ProcessName semantics
        string cleanName = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;

        // Act
        bool isProtected = ProcessOptimizerService.IsProtectedProcess(cleanName);

        // Assert
        Assert.True(isProtected, $"Process '{processName}' must be strictly protected from termination");
    }

    [Theory]
    [InlineData("notepad")]
    [InlineData("chrome")]
    [InlineData("discord")]
    [InlineData("spotify")]
    [InlineData("game_client")]
    [InlineData("some_random_miner")]
    public void IsProtectedProcess_PermitsNonCoreProcesses(string processName)
    {
        // Act
        bool isProtected = ProcessOptimizerService.IsProtectedProcess(processName);

        // Assert
        Assert.False(isProtected, $"Non-core process '{processName}' should not be flagged as core system process");
    }
}
