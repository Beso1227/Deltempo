using System.Globalization;

namespace WinTempCleaner.Core.Abstractions;

/// <summary>
/// Parses the <c>MemoryGlobalHotkey</c> setting into the modifier mask and virtual key that
/// <c>RegisterHotKey</c> expects.
/// </summary>
/// <remarks>
/// The setting shipped as a readable string (<c>"CTRL+SHIFT+M"</c>) but nothing ever read it:
/// the hotkey was hardcoded to Ctrl+Shift+M in MainWindow. Parsing the stored value is what
/// makes the setting real.
/// </remarks>
public static class HotkeyParser
{
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    /// <summary>Windows virtual-key code for 'A'.</summary>
    private const uint VK_A = 0x41;

    /// <summary>
    /// Attempts to parse a hotkey specification such as <c>"CTRL+SHIFT+M"</c>.
    /// </summary>
    /// <returns>
    /// True when a usable hotkey was produced. A non-modifier key and at least one modifier
    /// are both required: a bare key would swallow ordinary typing system-wide, and a
    /// modifier-only combination cannot be pressed on its own.
    /// </returns>
    public static bool TryParse(string? spec, out uint modifiers, out uint virtualKey)
    {
        // Only ever assign on success: leaving partial values in the out parameters on a
        // failed parse is a footgun, and callers reasonably assume 0/0 means "no hotkey".
        modifiers = 0;
        virtualKey = 0;

        if (string.IsNullOrWhiteSpace(spec))
        {
            return false;
        }

        uint parsedModifiers = 0;
        uint parsedKey = 0;

        foreach (string rawPart in spec.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (rawPart.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    parsedModifiers |= MOD_CONTROL;
                    continue;
                case "ALT":
                    parsedModifiers |= MOD_ALT;
                    continue;
                case "SHIFT":
                    parsedModifiers |= MOD_SHIFT;
                    continue;
                case "WIN":
                case "WINDOWS":
                    parsedModifiers |= MOD_WIN;
                    continue;
            }

            if (!TryParseKey(rawPart, out parsedKey))
            {
                return false;
            }
        }

        // A bare key would swallow ordinary typing system-wide; a modifier-only
        // combination cannot be pressed on its own.
        if (parsedKey == 0 || parsedModifiers == 0)
        {
            return false;
        }

        // Suppress the auto-repeat storm that a held hotkey otherwise generates.
        modifiers = parsedModifiers | MOD_NOREPEAT;
        virtualKey = parsedKey;
        return true;
    }

    private static bool TryParseKey(string token, out uint virtualKey)
    {
        virtualKey = 0;

        if (NamedKeys.TryGetValue(token.ToUpperInvariant(), out uint named))
        {
            virtualKey = named;
            return true;
        }

        // F1-F24 live at 0x70-0x87.
        if (token.Length is >= 2 and <= 3
            && (token[0] == 'F' || token[0] == 'f')
            && int.TryParse(token.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int fn)
            && fn is >= 1 and <= 24)
        {
            virtualKey = (uint)(0x70 + fn - 1);
            return true;
        }

        if (token.Length != 1)
        {
            return false;
        }

        char c = char.ToUpperInvariant(token[0]);

        if (c is >= 'A' and <= 'Z')
        {
            virtualKey = VK_A + (uint)(c - 'A');
            return true;
        }

        if (c is >= '0' and <= '9')
        {
            virtualKey = 0x30 + (uint)(c - '0');
            return true;
        }

        return false;
    }

    /// <summary>Non-modifier keys that have no single-character spelling.</summary>
    private static readonly Dictionary<string, uint> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BACKSPACE"] = 0x08,
        ["BKSP"] = 0x08,
        ["TAB"] = 0x09,
        ["ENTER"] = 0x0D,
        ["RETURN"] = 0x0D,
        ["PAUSE"] = 0x13,
        ["CAPSLOCK"] = 0x14,
        ["ESC"] = 0x1B,
        ["ESCAPE"] = 0x1B,
        ["SPACE"] = 0x20,
        ["PAGEUP"] = 0x21,
        ["PGUP"] = 0x21,
        ["PAGEDOWN"] = 0x22,
        ["PGDN"] = 0x22,
        ["END"] = 0x23,
        ["HOME"] = 0x24,
        ["LEFT"] = 0x25,
        ["UP"] = 0x26,
        ["RIGHT"] = 0x27,
        ["DOWN"] = 0x28,
        ["INSERT"] = 0x2D,
        ["INS"] = 0x2D,
        ["DELETE"] = 0x2E,
        ["DEL"] = 0x2E,
    };
}
