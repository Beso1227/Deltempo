using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public class AppAiReport
{
    public string AppName { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string DisplayVersion { get; set; } = string.Empty;
    public string WhatIsIt { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string SafetyVerdict { get; set; } = "Safe to Remove";
    public string RemovalImpact { get; set; } = string.Empty;
    public string ProviderUsed { get; set; } = "Offline Catalog";
    public DateTime AnalyzedAtUtc { get; set; } = DateTime.UtcNow;

    public string BadgeColor { get; set; } = "#10B981";
    public string BadgeBackground { get; set; } = "#0D2818";
    public string BadgeBorder { get; set; } = "#10B981";

    public string Description => WhatIsIt;
    public string SafetyAdvice => RemovalImpact;
    public bool IsCoreRuntime => (SafetyVerdict ?? string.Empty).Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                                 (SafetyVerdict ?? string.Empty).Contains("Keep", StringComparison.OrdinalIgnoreCase) ||
                                 (SafetyVerdict ?? string.Empty).Contains("Driver", StringComparison.OrdinalIgnoreCase) ||
                                 (SafetyVerdict ?? string.Empty).Contains("High Risk", StringComparison.OrdinalIgnoreCase);

    public bool IsAiGenerated => !string.Equals(ProviderUsed, "Built-in Catalog", StringComparison.OrdinalIgnoreCase) &&
                                 !string.Equals(ProviderUsed, "Heuristic Classifier", StringComparison.OrdinalIgnoreCase) &&
                                 !string.Equals(ProviderUsed, "Offline Catalog", StringComparison.OrdinalIgnoreCase) &&
                                 !string.Equals(ProviderUsed, "Local Intelligence (Fallback)", StringComparison.OrdinalIgnoreCase);

    public void ApplyBadgeColors()
    {
        string verdict = (SafetyVerdict ?? string.Empty).ToLowerInvariant();
        if (verdict.Contains("core") || verdict.Contains("keep") || verdict.Contains("driver") || verdict.Contains("critical"))
        {
            BadgeColor = "#EF4444";       // Destructive Red
            BadgeBackground = "#2A0E0E";
            BadgeBorder = "#EF4444";
        }
        else if (verdict.Contains("caution") || verdict.Contains("shared") || verdict.Contains("warning"))
        {
            BadgeColor = "#F59E0B";       // Warning Amber
            BadgeBackground = "#2A1E0D";
            BadgeBorder = "#F59E0B";
        }
        else if (verdict.Contains("bloatware") || verdict.Contains("unneeded") || verdict.Contains("optional"))
        {
            BadgeColor = "#06B6D4";       // Cyan
            BadgeBackground = "#0C2329";
            BadgeBorder = "#06B6D4";
        }
        else
        {
            BadgeColor = "#10B981";       // Emerald Green
            BadgeBackground = "#0D2818";
            BadgeBorder = "#10B981";
        }
    }
}

public static class AppIntelligenceService
{
    private static readonly HttpClient HttpClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2)
    })
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private static readonly string CacheFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Deltempo",
        "app_intelligence_cache.json");

    private static readonly ConcurrentDictionary<string, AppAiReport> MemoryCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ReaderWriterLockSlim CacheLock = new();

    static AppIntelligenceService()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Deltempo-AppIntelligence", "1.6.5"));
        LoadCache();
    }

    public static string GetCacheKey(string publisher, string displayName)
    {
        return $"{publisher?.Trim()}::{displayName?.Trim()}".ToLowerInvariant();
    }

    /// <summary>
    /// Cache identity: the app PLUS the provenance of the answer.
    /// </summary>
    /// <remarks>
    /// The key was publisher::displayName alone, so a description produced by one provider
    /// survived a switch to another provider, a model change or a rotated key, with no way
    /// to invalidate it except ClearCache() - which also discards every unrelated answer.
    /// </remarks>
    public static string GetCacheKey(string publisher, string displayName, AppSettings settings)
    {
        string provider = string.IsNullOrWhiteSpace(settings.AiProvider) ? "BuiltIn" : settings.AiProvider;
        string model = AiModelCatalog.ResolveModelId(settings.AiProvider, settings.AiModelName);
        return $"{provider}|{model}|{GetCacheKey(publisher, displayName)}";
    }

    public static AppAiReport? GetCachedReport(InstalledAppItem app)
    {
        string key = GetCacheKey(app.Publisher, app.DisplayName, SettingsService.Current);
        return MemoryCache.TryGetValue(key, out var report) ? report : null;
    }

    public static async Task<AppAiReport> AnalyzeAppAsync(InstalledAppItem app, bool forceOnline = false, CancellationToken ct = default)
    {
        var settings = SettingsService.Current;
        string cacheKey = GetCacheKey(app.Publisher, app.DisplayName, settings);

        AppAiReport report;
        bool fromCache = false;

        // Route to AI provider if enabled and configured. The cache is consulted only when
        // AI is enabled, so turning it off genuinely stops using cached cloud answers.
        if (!settings.EnableOnlineAiSafety)
        {
            report = GenerateLocalIntelligenceReport(app);
        }
        else if (!forceOnline && MemoryCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }
        else
        {
            try
            {
                report = await QueryProviderAsync(app, settings, ct);
            }
            catch (OperationCanceledException)
            {
                // Never convert a cancellation into a cached answer.
                throw;
            }
            catch (AiProviderException ex)
            {
                LastProviderError = ex;
                System.Diagnostics.Trace.WriteLine(
                    $"[Deltempo App AI] Provider failure ({settings.AiProvider}/{ex.Kind}): {ex.Message}");

                report = GenerateLocalIntelligenceReport(app);
                report.ProviderUsed = $"Local Intelligence (Fallback - {ex.Kind})";
            }
            catch (Exception ex)
            {
                LastProviderError = null;
                System.Diagnostics.Trace.WriteLine($"[Deltempo App AI] Query failed ({settings.AiProvider}): {ex.Message}. Falling back to Local Intelligence.");
                report = GenerateLocalIntelligenceReport(app);
                report.ProviderUsed = "Local Intelligence (Fallback)";
            }
        }

        report.AppName = app.DisplayName;
        report.Publisher = app.Publisher;
        report.DisplayVersion = app.DisplayVersion;
        report.ApplyBadgeColors();

        // Only a real result is cached. Caching a fallback pinned the app to an offline
        // answer that could never be refreshed without clearing every unrelated entry.
        if (!fromCache && !report.ProviderUsed.Contains("(Fallback", StringComparison.Ordinal))
        {
            MemoryCache[cacheKey] = report;
            SaveCache();
        }

        return report;
    }

    /// <summary>Most recent provider failure, for the UI to surface. Cleared on success.</summary>
    public static AiProviderException? LastProviderError { get; private set; }

    private static async Task<AppAiReport> QueryProviderAsync(InstalledAppItem app, AppSettings settings, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.AiProvider) ||
            settings.AiProvider.Equals("BuiltIn", StringComparison.OrdinalIgnoreCase))
        {
            LastProviderError = null;
            return GenerateLocalIntelligenceReport(app);
        }

        if (!AiModelCatalog.TryGetProvider(settings.AiProvider, out var descriptor))
        {
            throw new AiProviderException(AiFailureKind.ModelUnavailable,
                $"Unknown AI provider '{settings.AiProvider}'.");
        }

        // Previously the key gated each provider arm, so a provider with a blank key fell
        // through to the offline branch and reported success.
        if (descriptor.RequiresApiKey && string.IsNullOrWhiteSpace(settings.AiApiKey))
        {
            throw new AiProviderException(AiFailureKind.Authentication,
                $"No API key configured for {settings.AiProvider}.");
        }

        string model = AiModelCatalog.ResolveModelId(settings.AiProvider, settings.AiModelName);

        string rawText = await AiProviderClient.CompleteAsync(new AiChatRequest(
            Provider: settings.AiProvider,
            ApiKey: settings.AiApiKey,
            ModelId: model,
            SystemPrompt: "You are a Windows software safety expert. Output valid JSON only.",
            UserPrompt: BuildAppPrompt(app),
            Endpoint: settings.AiOllamaEndpoint), ct);

        LastProviderError = null;

        var report = ParseAiStructuredJson(rawText, app);
        report.ProviderUsed = $"{settings.AiProvider} ({model})";
        return report;
    }

    public static AppAiReport GenerateLocalIntelligenceReport(InstalledAppItem app)
    {
        var catalogEntry = AppDescriptionCatalog.FindCatalogEntry(app.DisplayName, app.Publisher);
        if (catalogEntry != null)
        {
            return new AppAiReport
            {
                AppName = app.DisplayName,
                Publisher = app.Publisher,
                DisplayVersion = app.DisplayVersion,
                WhatIsIt = catalogEntry.Description,
                Category = catalogEntry.Category,
                SafetyVerdict = catalogEntry.IsCoreRuntime ? "Core Runtime / Driver - Keep" : "Safe to Remove",
                RemovalImpact = !string.IsNullOrWhiteSpace(catalogEntry.SafetyAdvice)
                    ? catalogEntry.SafetyAdvice
                    : (catalogEntry.IsCoreRuntime ? "Uninstalling may break dependent games and applications." : "Reclaims disk space with zero impact on other software."),
                ProviderUsed = "Built-in Catalog"
            };
        }

        var (desc, cat, advice, isCore) = AppDescriptionCatalog.ClassifyUnknownApp(
            app.DisplayName,
            app.Publisher,
            app.InstallLocation,
            app.UninstallEngine);

        return new AppAiReport
        {
            AppName = app.DisplayName,
            Publisher = app.Publisher,
            DisplayVersion = app.DisplayVersion,
            WhatIsIt = desc,
            Category = cat,
            SafetyVerdict = isCore ? "Core Runtime / Driver - Keep" : "Safe to Remove",
            RemovalImpact = advice,
            ProviderUsed = "Heuristic Classifier"
        };
    }

    #region Cloud AI Providers

    /// <summary>Builds the user-turn prompt for an installed-app analysis.</summary>
    /// <remarks>
    /// Recreated from the previous transport code, which was deleted along with the rest of
    /// the per-provider copies. The prompt text is unchanged - what changed is that one
    /// shared transport now sends it, with a per-provider timeout and classified errors.
    /// </remarks>
    private static string BuildAppPrompt(InstalledAppItem app)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Analyze the following installed Windows application and provide concise, accurate information in strict JSON.");
        sb.AppendLine();
        sb.AppendLine($"Application Name: {app.DisplayName}");
        sb.AppendLine($"Publisher: {app.Publisher}");
        sb.AppendLine($"Version: {app.DisplayVersion}");
        sb.AppendLine($"Install Location: {app.InstallLocation}");
        sb.AppendLine($"Uninstall Engine: {app.UninstallEngine}");
        sb.AppendLine();
        sb.AppendLine("Respond with a valid JSON object only, matching this exact structure:");
        sb.AppendLine("{");
        sb.AppendLine("  \"whatIsIt\": \"A clear, plain-English 1-2 sentence description of what this software actually is and what it does for the user.\",");
        sb.AppendLine("  \"category\": \"One of: Browsers, Gaming, Development, Productivity, Media, System & Hardware, Runtimes, Utilities, General\",");
        sb.AppendLine("  \"safetyVerdict\": \"One of: Safe to Remove, Core Runtime / Driver - Keep, Caution - Shared Tool, Potential Bloatware\",");
        sb.AppendLine("  \"removalImpact\": \"A concise 1-sentence explanation of what happens if uninstalled (e.g. will other games/software stop working, or is it completely safe).\"");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static AppAiReport ParseAiStructuredJson(string rawJson, InstalledAppItem app)
    {
        try
        {
            int start = rawJson.IndexOf('{');
            int end = rawJson.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                rawJson = rawJson.Substring(start, end - start + 1);
            }

            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            string whatIsIt = root.TryGetProperty("whatIsIt", out var w) ? w.GetString() ?? "" : "";
            string category = root.TryGetProperty("category", out var c) ? c.GetString() ?? "General" : "General";
            string verdict = root.TryGetProperty("safetyVerdict", out var v) ? v.GetString() ?? "Safe to Remove" : "Safe to Remove";
            string impact = root.TryGetProperty("removalImpact", out var i) ? i.GetString() ?? "" : "";

            if (string.IsNullOrWhiteSpace(whatIsIt))
            {
                var fallback = GenerateLocalIntelligenceReport(app);
                whatIsIt = fallback.WhatIsIt;
                category = fallback.Category;
            }

            return new AppAiReport
            {
                AppName = app.DisplayName,
                Publisher = app.Publisher,
                DisplayVersion = app.DisplayVersion,
                WhatIsIt = whatIsIt,
                Category = category,
                SafetyVerdict = verdict,
                RemovalImpact = impact
            };
        }
        catch
        {
            return GenerateLocalIntelligenceReport(app);
        }
    }

    #endregion

    #region Disk Cache Management

    private static void LoadCache()
    {
        try
        {
            if (!File.Exists(CacheFile)) return;

            string json = File.ReadAllText(CacheFile);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, AppAiReport>>(json);
            if (loaded != null)
            {
                foreach (var (k, v) in loaded)
                {
                    v.ApplyBadgeColors();
                    MemoryCache[k] = v;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo App AI] Cache load failed: {ex.Message}");
        }
    }

    private static void SaveCache()
    {
        try
        {
            CacheLock.EnterWriteLock();
            try
            {
                string? dir = Path.GetDirectoryName(CacheFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var dict = new Dictionary<string, AppAiReport>(MemoryCache);
                string json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(CacheFile, json);
            }
            finally
            {
                CacheLock.ExitWriteLock();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo App AI] Cache save failed: {ex.Message}");
        }
    }

    #endregion
}
