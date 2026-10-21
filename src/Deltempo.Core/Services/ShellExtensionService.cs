using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace WinTempCleaner.Services;

[SupportedOSPlatform("windows")]
public static class ShellExtensionService
{
    private const string MenuTitle = "Scan with Deltempo";
    private const string RegKeyDirectory = @"Software\Classes\Directory\shell\Deltempo";
    private const string RegKeyBackground = @"Software\Classes\Directory\Background\shell\Deltempo";
    private const string RegKeyFiles = @"Software\Classes\*\shell\Deltempo";

    public static bool IsRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKeyDirectory);
            return key != null;
        }
        catch
        {
            return false;
        }
    }

    public static (bool Success, string Message) Register()
    {
        try
        {
            string exePath = GetExecutablePath();
            if (!File.Exists(exePath))
            {
                return (false, $"Deltempo binary not found at '{exePath}'");
            }

            string command = $"\"{exePath}\" --scan \"%1\"";
            string backgroundCommand = $"\"{exePath}\" --scan \"%V\"";

            // 1. Directory context menu
            RegisterKey(Registry.CurrentUser, RegKeyDirectory, MenuTitle, exePath, command);

            // 2. Directory background context menu
            RegisterKey(Registry.CurrentUser, RegKeyBackground, MenuTitle, exePath, backgroundCommand);

            // 3. Any file context menu
            string fileInspectCommand = $"\"{exePath}\" --inspect \"%1\"";
            RegisterKey(Registry.CurrentUser, RegKeyFiles, "Inspect with Deltempo", exePath, fileInspectCommand);

            return (true, "Explorer context menu registered successfully. Right-click any folder or file to inspect with Deltempo.");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to register context menu: {ex.Message}");
        }
    }

    public static (bool Success, string Message) Unregister()
    {
        try
        {
            UnregisterKey(Registry.CurrentUser, RegKeyDirectory);
            UnregisterKey(Registry.CurrentUser, RegKeyBackground);
            UnregisterKey(Registry.CurrentUser, RegKeyFiles);

            return (true, "Explorer context menu unregistered successfully.");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to unregister context menu: {ex.Message}");
        }
    }

    private static void RegisterKey(RegistryKey root, string subKeyPath, string text, string iconPath, string command)
    {
        using var key = root.CreateSubKey(subKeyPath);
        key.SetValue("", text);
        key.SetValue("Icon", iconPath);

        using var cmdKey = key.CreateSubKey("command");
        cmdKey.SetValue("", command);
    }

    private static void UnregisterKey(RegistryKey root, string subKeyPath)
    {
        try
        {
            root.DeleteSubKeyTree(subKeyPath, throwOnMissingSubKey: false);
        }
        catch { }
    }

    private static string GetExecutablePath()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string guiPath = Path.Combine(baseDir, "Deltempo.exe");
        if (File.Exists(guiPath)) return guiPath;

        string cliPath = Path.Combine(baseDir, "deltempo_cli.exe");
        if (File.Exists(cliPath)) return cliPath;

        return Process.GetCurrentProcess().MainModule?.FileName ?? guiPath;
    }
}
