using WinTempCleaner.Core.Abstractions;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// The MemoryGlobalHotkey setting shipped as "CTRL+SHIFT+M" but nothing ever read it -
/// the combination was hardcoded. These tests pin the parsing that now makes it real,
/// including the safety rule: a bare key would swallow ordinary typing system-wide.
/// </summary>
public class HotkeyParserTests
{
    [Theory]
    [InlineData("CTRL+SHIFT+M", HotkeyParser.MOD_CONTROL | HotkeyParser.MOD_SHIFT | HotkeyParser.MOD_NOREPEAT, 0x4D)]
    [InlineData("CTRL+ALT+DEL", HotkeyParser.MOD_CONTROL | HotkeyParser.MOD_ALT | HotkeyParser.MOD_NOREPEAT, 0x2E)]
    [InlineData("CTRL+SHIFT+F12", HotkeyParser.MOD_CONTROL | HotkeyParser.MOD_SHIFT | HotkeyParser.MOD_NOREPEAT, 0x7B)]
    [InlineData("WIN+K", HotkeyParser.MOD_WIN | HotkeyParser.MOD_NOREPEAT, 0x4B)]
    [InlineData("ALT+7", HotkeyParser.MOD_ALT | HotkeyParser.MOD_NOREPEAT, 0x37)]
    public void TryParse_AcceptsRealCombinations(string spec, uint expectedModifiers, uint expectedKey)
    {
        Assert.True(HotkeyParser.TryParse(spec, out uint modifiers, out uint key));
        Assert.Equal(expectedModifiers, modifiers);
        Assert.Equal(expectedKey, key);
    }

    [Theory]
    [InlineData("ctrl+shift+m")]   // lower case
    [InlineData("  CTRL + SHIFT + M  ")] // stray whitespace
    [InlineData("CONTROL+SHIFT+M")]  // long-form modifier
    public void TryParse_IsCaseAndWhitespaceTolerant(string spec)
    {
        Assert.True(HotkeyParser.TryParse(spec, out uint modifiers, out uint key));
        Assert.Equal(0x4Du, key);
        Assert.True((modifiers & HotkeyParser.MOD_CONTROL) != 0);
        Assert.True((modifiers & HotkeyParser.MOD_SHIFT) != 0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("M")]           // no modifier: would swallow every 'M' typed system-wide
    [InlineData("CTRL")]        // modifier only: cannot be pressed on its own
    [InlineData("CTRL+SHIFT+")]
    [InlineData("CTRL+NOPE")]   // unknown key token
    [InlineData("CTRL+F25")]   // outside F1-F24
    public void TryParse_RejectsUnsafeOrUnusableSpecifications(string? spec)
    {
        Assert.False(HotkeyParser.TryParse(spec, out uint modifiers, out uint key));
        Assert.Equal(0u, modifiers);
        Assert.Equal(0u, key);
    }

    [Fact]
    public void TryParse_AlwaysSetsNoRepeatToSuppressHeldKeyStorms()
    {
        Assert.True(HotkeyParser.TryParse("CTRL+SHIFT+M", out uint modifiers, out _));
        Assert.True((modifiers & HotkeyParser.MOD_NOREPEAT) != 0);
    }

    [Fact]
    public void DefaultSetting_StillParses()
    {
        // The default persisted value must keep working, or the hotkey silently dies.
        var settings = WinTempCleaner.Services.SettingsService.Current;
        Assert.True(HotkeyParser.TryParse(settings.MemoryGlobalHotkey, out uint modifiers, out uint key));
        Assert.True((modifiers & HotkeyParser.MOD_CONTROL) != 0);
        Assert.True((modifiers & HotkeyParser.MOD_SHIFT) != 0);
        Assert.Equal(0x4Du, key);
    }
}
