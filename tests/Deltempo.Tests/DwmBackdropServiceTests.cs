using System;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

public class DwmBackdropServiceTests
{
    [Fact]
    public void IsWindows11OrGreater_ReturnsBooleanWithoutCrashing()
    {
        bool isWin11 = DwmBackdropService.IsWindows11OrGreater();
        Assert.True(isWin11 || !isWin11);
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
