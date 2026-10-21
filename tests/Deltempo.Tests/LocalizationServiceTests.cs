using WinTempCleaner.Models;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Tests for the pure static localization engine. Covers translation lookup,
/// language fallback, key fallback, and target localization across all 5 languages.
/// </summary>
public class LocalizationServiceTests
{
    // ─── Get() lookup & fallback ───────────────────────────────────────

    [Theory]
    [InlineData("en", "AppTitle", "Deltempo")]
    [InlineData("ar", "AppTitle", "ديلتيمبو")]
    [InlineData("es", "AppTitle", "Deltempo")]
    [InlineData("fr", "AppTitle", "Deltempo")]
    [InlineData("de", "AppTitle", "Deltempo")]
    public void Get_KnownKey_ReturnsLocalizedValue(string lang, string key, string expected)
    {
        LocalizationService.CurrentLanguage = lang;
        Assert.Equal(expected, LocalizationService.Get(key));
    }

    [Fact]
    public void Get_UnknownKey_ReturnsKey()
    {
        LocalizationService.CurrentLanguage = "en";
        Assert.Equal("NonExistentKey", LocalizationService.Get("NonExistentKey"));
    }

    [Fact]
    public void Get_UnknownLanguage_FallsBackToEnglish()
    {
        LocalizationService.CurrentLanguage = "xx";
        // English fallback for a known key
        Assert.Equal("Deltempo", LocalizationService.Get("AppTitle"));
    }

    [Fact]
    public void Get_AllLanguages_LocalizeCriticalUI()
    {
        // Every language must produce a non-empty value for core UI keys
        string[] keys = { "AppTitle", "SmartClean", "CleanSelected", "Clear", "ActivityLog" };
        foreach (string lang in new[] { "en", "ar", "es", "fr", "de" })
        {
            LocalizationService.CurrentLanguage = lang;
            foreach (string key in keys)
            {
                string value = LocalizationService.Get(key);
                Assert.False(string.IsNullOrWhiteSpace(value), $"lang={lang} key={key} returned empty");
            }
        }
    }

    [Fact]
    public void Get_Translations_DifferAcrossLanguages()
    {
        // Verify specific known translations differ from English
        LocalizationService.CurrentLanguage = "en";
        Assert.Equal("Smart Clean", LocalizationService.Get("SmartClean"));
        Assert.Equal("Clean Selected", LocalizationService.Get("CleanSelected"));

        LocalizationService.CurrentLanguage = "ar";
        Assert.Equal("تنظيف ذكي", LocalizationService.Get("SmartClean"));
        Assert.Equal("تنظيف المحدد", LocalizationService.Get("CleanSelected"));

        LocalizationService.CurrentLanguage = "de";
        Assert.Equal("Smart-Bereinigung", LocalizationService.Get("SmartClean"));
        Assert.Equal("Ausgewählte Bereinigen", LocalizationService.Get("CleanSelected"));

        LocalizationService.CurrentLanguage = "es";
        Assert.Equal("Limpieza Inteligente", LocalizationService.Get("SmartClean"));

        LocalizationService.CurrentLanguage = "fr";
        Assert.Equal("Nettoyage Intelligent", LocalizationService.Get("SmartClean"));

        LocalizationService.CurrentLanguage = "en";
    }

    [Fact]
    public void Get_LanguageSwitch_UpdatesActiveTranslation()
    {
        LocalizationService.CurrentLanguage = "en";
        Assert.Equal("Smart Clean", LocalizationService.Get("SmartClean"));

        LocalizationService.CurrentLanguage = "ar";
        Assert.Equal("تنظيف ذكي", LocalizationService.Get("SmartClean"));

        LocalizationService.CurrentLanguage = "de";
        Assert.Equal("Smart-Bereinigung", LocalizationService.Get("SmartClean"));

        // Restore default
        LocalizationService.CurrentLanguage = "en";
    }

    // ─── LocalizeTarget() ──────────────────────────────────────────────

    [Fact]
    public void LocalizeTarget_UserTemp_English()
    {
        LocalizationService.CurrentLanguage = "en";
        var target = new TargetFolderInfo { Id = "UserTemp" };
        LocalizationService.LocalizeTarget(target);
        Assert.Equal("User Temp & Scratchpad", target.Name);
        Assert.Equal("User Cache", target.Category);
        Assert.Contains("%TEMP%", target.Description);
    }

    [Fact]
    public void LocalizeTarget_UserTemp_Arabic()
    {
        LocalizationService.CurrentLanguage = "ar";
        var target = new TargetFolderInfo { Id = "UserTemp" };
        LocalizationService.LocalizeTarget(target);
        Assert.Equal("ملفات المستخدم المؤقتة (%TEMP%)", target.Name);
        Assert.Equal("كاش المستخدم", target.Category);
        LocalizationService.CurrentLanguage = "en";
    }

    [Fact]
    public void LocalizeTarget_WinTemp_AllLanguages()
    {
        foreach (string lang in new[] { "en", "ar", "es", "fr", "de" })
        {
            LocalizationService.CurrentLanguage = lang;
            var target = new TargetFolderInfo { Id = "WinTemp" };
            LocalizationService.LocalizeTarget(target);
            Assert.False(string.IsNullOrWhiteSpace(target.Name), $"WinTemp Name empty for {lang}");
            Assert.False(string.IsNullOrWhiteSpace(target.Category), $"WinTemp Category empty for {lang}");
            Assert.False(string.IsNullOrWhiteSpace(target.Description), $"WinTemp Description empty for {lang}");
        }
        LocalizationService.CurrentLanguage = "en";
    }

    [Fact]
    public void LocalizeTarget_AllTargets_AllLanguages_ProduceOutput()
    {
        // Exercise every target case across every language — covers the full switch
        string[] targets = {
            "UserTemp", "WinTemp", "WinPrefetch", "WinUpdateCache", "WinUpgradeLeftovers",
            "WinDeliveryOpt", "WinComponentCaches", "DeviceDriverPackages", "DefenderAntivirus",
            "WinSystemLogs", "SystemDumps", "TemporaryInternetFiles", "GpuShaderCaches",
            "GamingLaunchers", "MediaCreatorCaches", "AppCacheSweeper", "WinStoreAppCaches",
            "MessagingAppCaches", "BrowserCaches", "DevPackageCaches", "MobileDevResiduals",
            "CrashDumps", "Thumbnails", "SystemUsageTraces", "RecycleBin", "OrphanedAppData"
        };

        foreach (string lang in new[] { "en", "ar", "es", "fr", "de" })
        {
            LocalizationService.CurrentLanguage = lang;
            foreach (string id in targets)
            {
                var target = new TargetFolderInfo { Id = id };
                LocalizationService.LocalizeTarget(target);
                Assert.False(string.IsNullOrWhiteSpace(target.Name), $"{id}/{lang} Name empty");
                Assert.False(string.IsNullOrWhiteSpace(target.Category), $"{id}/{lang} Category empty");
            }
        }
        LocalizationService.CurrentLanguage = "en";
    }

    [Fact]
    public void LocalizeTarget_UnknownTarget_LeavesUnchanged()
    {
        LocalizationService.CurrentLanguage = "en";
        var target = new TargetFolderInfo { Id = "UnknownTarget", Name = "Original", Category = "Cat" };
        LocalizationService.LocalizeTarget(target);
        Assert.Equal("Original", target.Name);
        Assert.Equal("Cat", target.Category);
    }
}
