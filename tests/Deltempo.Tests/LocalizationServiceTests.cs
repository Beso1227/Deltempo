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

    // ─── Dictionary completeness & change signal ──────────────────────

    /// <summary>
    /// Get() hides a missing translation by falling back to English, so a key that only
    /// exists in "en" looks fine in the UI until the user switches language and sees one
    /// English label between translated ones. This walks the raw dictionaries instead and
    /// names every gap. It is the guard the WinUI shell's attached-key layer leans on:
    /// that layer only proves a key resolves, not that it is translated.
    /// </summary>
    [Fact]
    public void EveryEnglishKey_IsTranslatedInAllOtherLanguages()
    {
        string[] languages = { "ar", "es", "fr", "de" };
        var missing = new List<string>();

        foreach (string lang in languages)
        {
            foreach (string key in LocalizationService.TranslationsByLanguage["en"].Keys)
            {
                if (!LocalizationService.TranslationsByLanguage[lang].TryGetValue(key, out string? value) ||
                    string.IsNullOrWhiteSpace(value))
                {
                    missing.Add($"{lang}:{key}");
                }
            }
        }

        Assert.True(
            missing.Count == 0,
            $"Untranslated keys ({missing.Count}): {string.Join(", ", missing)}");
    }

    [Fact]
    public void Translations_CoverExactlyTheFiveSupportedLanguages()
    {
        Assert.Equal(
            new[] { "ar", "de", "en", "es", "fr" },
            LocalizationService.TranslationsByLanguage.Keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    /// <summary>
    /// Get() falls back to the key for anything it does not know, so it cannot be used to
    /// prove a binding resolves - English values such as "Cancel" and "Close" are their own
    /// key. ContainsKey is the honest answer, and the shell's self-test relies on it.
    /// </summary>
    [Theory]
    [InlineData("Cancel", true)]
    [InlineData("NavCleaner", true)]
    [InlineData("CancelButton", false)]
    [InlineData("", false)]
    public void ContainsKey_AnswersFromTheEnglishDictionary(string key, bool expected)
    {
        Assert.Equal(expected, LocalizationService.ContainsKey(key));
    }

    /// <summary>
    /// Shells subscribe once and re-paint on the event, so it must fire on a real change
    /// and must NOT fire when a settings round-trip re-assigns the current value.
    /// </summary>
    [Fact]
    public void CurrentLanguage_RaisesLanguageChangedOnlyOnRealChange()
    {
        LocalizationService.CurrentLanguage = "en";

        int raised = 0;
        EventHandler handler = (_, _) => raised++;
        LocalizationService.LanguageChanged += handler;

        try
        {
            LocalizationService.CurrentLanguage = "en";
            Assert.Equal(0, raised);

            LocalizationService.CurrentLanguage = "de";
            Assert.Equal(1, raised);

            LocalizationService.CurrentLanguage = "de";
            Assert.Equal(1, raised);
        }
        finally
        {
            LocalizationService.LanguageChanged -= handler;
            LocalizationService.CurrentLanguage = "en";
        }
    }

    [Theory]
    [InlineData("ar", true)]
    [InlineData("en", false)]
    [InlineData("es", false)]
    [InlineData("fr", false)]
    [InlineData("de", false)]
    public void IsRightToLeft_IsTrueOnlyForArabic(string lang, bool expected)
    {
        LocalizationService.CurrentLanguage = lang;
        Assert.Equal(expected, LocalizationService.IsRightToLeft);
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
