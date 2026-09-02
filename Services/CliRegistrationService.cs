using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace WinTempCleaner.Services;

public static class CliRegistrationService
{
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint Msg,
        UIntPtr wParam,
        string lParam,
        uint fuFlags,
        uint uTimeout,
        out UIntPtr lpdwResult);

    private const int HWND_BROADCAST = 0xffff;
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    public static void EnsureCliRegistered()
    {
        try
        {
            var currentExePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(currentExePath) || !File.Exists(currentExePath))
                return;

            var exeDir = Path.GetDirectoryName(currentExePath);
            if (string.IsNullOrEmpty(exeDir))
                return;

            // 1. Create native console wrappers (.cmd and .ps1) in the app directory for synchronous terminal execution
            EnsureCliWrapperFiles(exeDir, currentExePath);

            // 2. Register in Windows App Paths (Enables Win+R "deltempo" & Windows Shell execution)
            RegisterAppPaths("deltempo.exe", currentExePath, exeDir);
            RegisterAppPaths("deltempo", currentExePath, exeDir);

            // 3. Ensure current folder is in User PATH environment variable
            RegisterToUserPath(exeDir);
        }
        catch
        {
            // Non-critical background registration failure
        }
    }

    private static void EnsureCliWrapperFiles(string exeDir, string exePath)
    {
        try
        {
            string exeName = Path.GetFileName(exePath);
            string cmdFile = Path.Combine(exeDir, "deltempo.cmd");
            string cmdContent = $"@echo off\r\n\"%~dp0{exeName}\" %*\r\n";
            if (!File.Exists(cmdFile) || File.ReadAllText(cmdFile) != cmdContent)
            {
                File.WriteAllText(cmdFile, cmdContent);
            }

            string ps1File = Path.Combine(exeDir, "deltempo.ps1");
            string ps1Content = $"& \"$PSScriptRoot\\{exeName}\" @args\r\n";
            if (!File.Exists(ps1File) || File.ReadAllText(ps1File) != ps1Content)
            {
                File.WriteAllText(ps1File, ps1Content);
            }
        }
        catch { }
    }

    private static void RegisterAppPaths(string appName, string exePath, string exeDir)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@$"Software\Microsoft\Windows\CurrentVersion\App Paths\{appName}");
            if (key != null)
            {
                key.SetValue("", exePath);
                key.SetValue("Path", exeDir);
            }
        }
        catch { }
    }

    private static void RegisterToUserPath(string exeDir)
    {
        try
        {
            var userPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
            var paths = userPath.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList();

            if (!paths.Any(p => string.Equals(p, exeDir, StringComparison.OrdinalIgnoreCase)))
            {
                paths.Add(exeDir);
                var newPath = string.Join(";", paths);
                Environment.SetEnvironmentVariable("Path", newPath, EnvironmentVariableTarget.User);

                // Broadcast change to Windows shell and running terminals
                SendMessageTimeout(
                    (IntPtr)HWND_BROADCAST,
                    WM_SETTINGCHANGE,
                    UIntPtr.Zero,
                    "Environment",
                    SMTO_ABORTIFHUNG,
                    1000,
                    out _);
            }
        }
        catch { }
    }
}
