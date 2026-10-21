using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WinTempCleaner.Services;

/// <summary>
/// DPAPI (CurrentUser-scope) protection for secrets persisted in settings.json.
/// Stored format: "dpapi:&lt;base64 ciphertext&gt;". Legacy plaintext values are
/// detected via <see cref="IsProtected"/> and migrated to protected form on the
/// next save, so plaintext keys stop persisting to disk immediately after upgrade.
/// </summary>
internal static class SettingsSecretProtector
{
    private const string Prefix = "dpapi:";

    // Application-specific entropy: a generic infostealer running as the user must
    // also know this constant to decrypt; raises the bar without user friction.
    private static readonly byte[] Entropy =
    {
        0x9D, 0x3E, 0x71, 0xC4, 0x58, 0xA2, 0xB6, 0x0F,
        0xE1, 0x7C, 0x24, 0x93, 0x6A, 0xD8, 0x41, 0xFB
    };

    /// <summary>
    /// Whether the current user profile can use Windows DPAPI. Restricted hosts
    /// can deny DPAPI; no plaintext fallback is ever used in that case.
    /// </summary>
    public static bool IsAvailable
    {
        get
        {
            try
            {
                byte[] probe = ProtectedData.Protect(new byte[] { 0 }, Entropy, DataProtectionScope.CurrentUser);
                _ = ProtectedData.Unprotect(probe, Entropy, DataProtectionScope.CurrentUser);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>True when the stored value is already in DPAPI-protected form.</summary>
    public static bool IsProtected(string? stored) =>
        !string.IsNullOrEmpty(stored) && stored.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>
    /// Protects a secret. Empty stays empty; already-protected values pass through
    /// unchanged (idempotent). Fails closed to empty rather than persisting plaintext.
    /// </summary>
    public static string Protect(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return string.Empty;
        if (IsProtected(plaintext)) return plaintext;

        try
        {
            byte[] plain = Encoding.UTF8.GetBytes(plaintext);
            byte[] cipher = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            return Prefix + Convert.ToBase64String(cipher);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Settings] Secret protection failed (secret not persisted): {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Restores a secret to plaintext for in-memory use only. Legacy plaintext
    /// values pass through unchanged (pre-DPAPI settings files). Corrupt or
    /// foreign-user payloads fail closed to empty rather than returning garbage.
    /// </summary>
    public static string Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return string.Empty;
        if (!IsProtected(stored)) return stored;

        try
        {
            byte[] cipher = Convert.FromBase64String(stored[Prefix.Length..]);
            byte[] plain = ProtectedData.Unprotect(cipher, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Settings] Secret unprotection failed (key treated as absent): {ex.Message}");
            return string.Empty;
        }
    }
}

/// <summary>
/// Crash-safe settings persistence: saves through a temp file + NTFS atomic
/// replace (previous content preserved as .bak), and loads with .bak fallback
/// recovery when the primary file is missing or unparseable.
/// </summary>
internal static class SettingsFileStore
{
    /// <summary>
    /// Persists JSON atomically. On NTFS, <see cref="File.Replace"/> swaps the
    /// destination and keeps the previous content as .bak in one metadata operation,
    /// so a crash mid-write can never leave a torn settings.json.
    /// </summary>
    public static void SaveAtomic(string settingsPath, string json)
    {
        string? dir = Path.GetDirectoryName(settingsPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        string tmpPath = settingsPath + ".tmp";
        string bakPath = settingsPath + ".bak";

        File.WriteAllText(tmpPath, json);

        if (File.Exists(settingsPath))
        {
            File.Replace(tmpPath, settingsPath, bakPath, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(tmpPath, settingsPath);
        }
    }

    /// <summary>
    /// Deserializes settings with recovery: primary file first, then the .bak
    /// written by the previous successful save. Returns null when neither parses.
    /// Unknown JSON fields are ignored (forward compatible with newer versions).
    /// </summary>
    public static AppSettings? Load(string settingsPath)
    {
        foreach (string candidate in new[] { settingsPath, settingsPath + ".bak" })
        {
            string? content = TryReadAllText(candidate);
            if (content == null) continue;

            try
            {
                return JsonSerializer.Deserialize<AppSettings>(content);
            }
            catch (JsonException ex)
            {
                Trace.WriteLine($"[Settings] Parse failed for '{candidate}': {ex.Message}");
            }
        }

        return null;
    }

    private static string? TryReadAllText(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            string content = File.ReadAllText(path);
            return content.Length == 0 ? null : content;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Settings] Read failed for '{path}': {ex.Message}");
            return null;
        }
    }
}
