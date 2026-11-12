using System;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

public class DwmBackdropServiceTests
{
    [Fact]
    public void IsWindows11OrGreater_MatchesTheWindows11BuildRule()
    {
        bool isWin11 = DwmBackdropService.IsWindows11OrGreater();

        // This used to assert `isWin11 || !isWin11`, which is true for every possible
        // value and therefore could never fail. The documented contract is "Windows 11
        // means build 22000 or later", so pin the answer against the OS version. A change
        // to the threshold in the service now fails here.
        Version os = Environment.OSVersion.Version;
        bool expected = os.Major >= 10 && os.Build >= 22000;

        Assert.Equal(expected, isWin11);
    }

    [Theory]
    [InlineData(BackdropType.None)]
    [InlineData(BackdropType.Mica)]
    [InlineData(BackdropType.Acrylic)]
    [InlineData(BackdropType.Tabbed)]
    public void ApplyBackdrop_WithZeroHwnd_ReturnsFalseSafely(BackdropType type)
    {
        bool result = DwmBackdropService.ApplyBackdrop(IntPtr.Zero, type, isDarkMode: true);
        Assert.False(result);
    }
}
