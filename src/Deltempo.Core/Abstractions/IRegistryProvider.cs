using Microsoft.Win32;

namespace WinTempCleaner.Core.Abstractions;

/// <summary>
/// Abstraction for accessing Windows Registry safely without hardcoded coupling.
/// </summary>
public interface IRegistryProvider
{
    object? GetCurrentUserValue(string subKey, string valueName);
    void SetCurrentUserValue(string subKey, string valueName, object value, RegistryValueKind valueKind = RegistryValueKind.String);
    void DeleteCurrentUserValue(string subKey, string valueName);
}

/// <summary>
/// Production registry provider targeting HKEY_CURRENT_USER.
/// </summary>
public class WindowsRegistryProvider : IRegistryProvider
{
    public static readonly WindowsRegistryProvider Instance = new();

    public object? GetCurrentUserValue(string subKey, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(subKey);
        return key?.GetValue(valueName);
    }

    public void SetCurrentUserValue(string subKey, string valueName, object value, RegistryValueKind valueKind = RegistryValueKind.String)
    {
        using var key = Registry.CurrentUser.CreateSubKey(subKey);
        key.SetValue(valueName, value, valueKind);
    }

    public void DeleteCurrentUserValue(string subKey, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(subKey, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
