using System.IO;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Crash-safety and secret-protection tests for the settings persistence layer:
/// DPAPI secret round-trips and atomic save / load-with-backup-recovery behavior.
/// </summary>
public class SettingsStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _settingsPath;

    public SettingsStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "deltempo-settings-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _settingsPath = Path.Combine(_dir, "settings.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void Protect_Unprotect_RoundTripsSecret()
    {
        string secret = "sk-test-abc123!@# ünïcode";

        string stored = SettingsSecretProtector.Protect(secret);

        if (!SettingsSecretProtector.IsAvailable)
        {
            Assert.Equal(string.Empty, stored);
            return;
        }

        Assert.NotEqual(secret, stored);
        Assert.True(SettingsSecretProtector.IsProtected(stored));
        Assert.StartsWith("dpapi:", stored, StringComparison.Ordinal);
        Assert.Equal(secret, SettingsSecretProtector.Unprotect(stored));
    }

    [Fact]
    public void Protect_IsIdempotent_ForAlreadyProtectedValues()
    {
        string stored = SettingsSecretProtector.Protect("secret");

        Assert.Equal(stored, SettingsSecretProtector.Protect(stored));
    }

    [Fact]
    public void Unprotect_LegacyPlaintext_PassesThrough()
    {
        Assert.Equal("legacy-plain-key", SettingsSecretProtector.Unprotect("legacy-plain-key"));
        Assert.False(SettingsSecretProtector.IsProtected("legacy-plain-key"));
    }

    [Fact]
    public void Unprotect_CorruptPayloads_FailClosedToEmpty()
    {
        string wrongKey = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 });

        Assert.Equal(string.Empty, SettingsSecretProtector.Unprotect("dpapi:not-valid-base64!!!"));
        Assert.Equal(string.Empty, SettingsSecretProtector.Unprotect("dpapi:" + wrongKey));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Protect_EmptySecret_StaysEmpty(string? secret)
    {
        Assert.Equal(string.Empty, SettingsSecretProtector.Protect(secret));
        Assert.False(SettingsSecretProtector.IsProtected(SettingsSecretProtector.Protect(secret)));
    }

    [Fact]
    public void SaveAtomic_FirstSave_CreatesFileWithoutBackup()
    {
        SettingsFileStore.SaveAtomic(_settingsPath, "{\"v\":1}");

        Assert.True(File.Exists(_settingsPath));
        Assert.False(File.Exists(_settingsPath + ".bak"));
        Assert.False(File.Exists(_settingsPath + ".tmp"));
        Assert.Equal("{\"v\":1}", File.ReadAllText(_settingsPath));
    }

    [Fact]
    public void SaveAtomic_SecondSave_KeepsPreviousContentAsBackup()
    {
        SettingsFileStore.SaveAtomic(_settingsPath, "{\"v\":1}");

        SettingsFileStore.SaveAtomic(_settingsPath, "{\"v\":2}");

        Assert.Equal("{\"v\":2}", File.ReadAllText(_settingsPath));
        Assert.Equal("{\"v\":1}", File.ReadAllText(_settingsPath + ".bak"));
    }

    [Fact]
    public void Load_ValidPrimary_ReturnsDeserializedSettings()
    {
        File.WriteAllText(_settingsPath, "{\"Language\":\"ar\"}");

        var loaded = SettingsFileStore.Load(_settingsPath);

        Assert.NotNull(loaded);
        Assert.Equal("ar", loaded!.Language);
    }

    [Fact]
    public void Load_CorruptPrimary_RecoversFromBackup()
    {
        File.WriteAllText(_settingsPath, "{corrupt!!");
        File.WriteAllText(_settingsPath + ".bak", "{\"Language\":\"fr\"}");

        var loaded = SettingsFileStore.Load(_settingsPath);

        Assert.NotNull(loaded);
        Assert.Equal("fr", loaded!.Language);
    }

    [Fact]
    public void Load_MissingOrCorruptWithoutBackup_ReturnsNull()
    {
        Assert.Null(SettingsFileStore.Load(_settingsPath));

        File.WriteAllText(_settingsPath, "not json at all");

        Assert.Null(SettingsFileStore.Load(_settingsPath));
    }

    [Fact]
    public void Load_UnknownFields_AreForwardCompatible()
    {
        File.WriteAllText(_settingsPath, "{\"BrandNewFutureField\":42,\"Language\":\"de\"}");

        var loaded = SettingsFileStore.Load(_settingsPath);

        Assert.NotNull(loaded);
        Assert.Equal("de", loaded!.Language);
    }
}
