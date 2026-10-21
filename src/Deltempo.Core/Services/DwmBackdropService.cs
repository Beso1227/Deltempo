using System;
using System.Runtime.InteropServices;

namespace WinTempCleaner.Services;

public enum BackdropType
{
    None = 0,
    Mica = 1,
    Acrylic = 2,
    Tabbed = 3
}

public static class DwmBackdropService
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    public const int DWMWCP_DEFAULT = 0;
    public const int DWMWCP_DONOTROUND = 1;
    public const int DWMWCP_ROUND = 2;
    public const int DWMWCP_ROUNDSMALL = 3;

    [DllImport("dwmapi.dll", SetLastError = false, ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    /// <summary>
    /// Checks if the operating system is Windows 11 (Build 22000+) or later.
    /// </summary>
    public static bool IsWindows11OrGreater()
    {
        return Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 22000;
    }

    /// <summary>
    /// Applies native Windows 11 DWM rounded corner preference to the specified window handle.
    /// </summary>
    public static bool ApplyWindowCorners(IntPtr hWnd, int cornerPreference = DWMWCP_ROUND)
    {
        if (hWnd == IntPtr.Zero || !IsWindows11OrGreater())
        {
            return false;
        }

        try
        {
            int hr = DwmSetWindowAttribute(hWnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));
            return hr == 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] DWM corner preference application failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Applies native Windows 11 immersive dark mode attribute to the specified window handle.
    /// </summary>
    public static bool ApplyDarkMode(IntPtr hWnd, bool isDarkMode = true)
    {
        if (hWnd == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            int darkModeValue = isDarkMode ? 1 : 0;
            int hr = DwmSetWindowAttribute(hWnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkModeValue, sizeof(int));
            return hr == 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] DWM dark mode application failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Applies native Windows 11 DWM backdrop (Mica / Acrylic / Tabbed) to the specified window handle.
    /// Returns true if applied successfully, false otherwise.
    /// </summary>
    public static bool ApplyBackdrop(IntPtr hWnd, BackdropType type, bool isDarkMode = true)
    {
        if (hWnd == IntPtr.Zero || !IsWindows11OrGreater())
        {
            return false;
        }

        try
        {
            // Set dark mode attribute first
            ApplyDarkMode(hWnd, isDarkMode);

            // Apply rounded corner preference
            ApplyWindowCorners(hWnd, DWMWCP_ROUND);

            // Map BackdropType to DWM_SYSTEMBACKDROP_TYPE:
            // 0 = Auto, 1 = None, 2 = MainWindow (Mica), 3 = TransientWindow (Acrylic), 4 = TabbedWindow (Mica Alt)
            int backdropValue = type switch
            {
                BackdropType.Mica => 2,
                BackdropType.Acrylic => 3,
                BackdropType.Tabbed => 4,
                _ => 1
            };

            int hr = DwmSetWindowAttribute(hWnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropValue, sizeof(int));
            return hr == 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] DWM backdrop application failed: {ex.Message}");
            return false;
        }
    }
}
