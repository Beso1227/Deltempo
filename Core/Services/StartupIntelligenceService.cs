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

namespace WinTempCleaner.Services;

public class StartupAiReport
{
    public string ItemName { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string ExePath { get; set; } = string.Empty;
    public string WhatIsIt { get; set; } = string.Empty;
    public string Description
    {
        get => WhatIsIt;
        set => WhatIsIt = value;
    }
    public StartupDisableVerdict Verdict { get; set; } = StartupDisableVerdict.SafeToDisable;
    public string VerdictDisplay { get; set; } = "Safe to Disable";
    public string ImpactIfDisabled { get; set; } = string.Empty;
    public string DisableImpact
    {
        get => ImpactIfDisabled;
        set => ImpactIfDisabled = value;
    }
    public string Recommendation { get; set; } = string.Empty;
    public string ProviderUsed { get; set; } = "Built-in Catalog";
    public DateTime AnalyzedAtUtc { get; set; } = DateTime.UtcNow;

    public string BadgeColor { get; set; } = "#10B981";
    public string BadgeBackground { get; set; } = "#0D2818";
    public string BadgeBorder { get; set; } = "#10B981";

    public bool IsAiGenerated => !string.Equals(ProviderUsed, "Built-in Catalog", StringComparison.OrdinalIgnoreCase) &&
                                 !string.Equals(ProviderUsed, "Heuristic Classifier", StringComparison.OrdinalIgnoreCase) &&
                                 !string.Equals(ProviderUsed, "Local Intelligence (Fallback)", StringComparison.OrdinalIgnoreCase);

    public void ApplyBadgeColors()
    {
        switch (Verdict)
        {
            case StartupDisableVerdict.DoNotDisable:
                VerdictDisplay = "Essential / Keep";
                BadgeColor = "#EF4444";       // Destructive Red
                BadgeBackground = "#2A0E0E";
                BadgeBorder = "#EF4444";
                break;
            case StartupDisableVerdict.Caution:
                VerdictDisplay = "Caution / Sync";
                BadgeColor = "#F59E0B";       // Warning Amber
                BadgeBackground = "#2A1E0D";
                BadgeBorder = "#F59E0B";
                break;
            default:
                VerdictDisplay = "Safe to Disable";
                BadgeColor = "#10B981";       // Emerald Green
                BadgeBackground = "#0D2818";
                BadgeBorder = "#10B981";
                break;
        }
    }
}

public static class StartupIntelligenceService
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
        "startup_intelligence_cache.json");

    private static readonly ConcurrentDictionary<string, StartupAiReport> MemoryCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ReaderWriterLockSlim CacheLock = new();

    static StartupIntelligenceService()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Deltempo-StartupIntelligence", "1.6.5"));
        LoadCache();
    }

    /// <summary>
    /// Cache identity: the startup item PLUS the provenance of the answer.
    /// </summary>
    /// <remarks>
    /// The key was name|exePath alone, so an analysis produced by one provider survived a
    /// switch to another provider, a model change or a rotated key, with no way to
    /// invalidate it except ClearCache() - which also discards every unrelated answer.
    /// </remarks>
    public static string GetCacheKey(string name, string exePath, AppSettings settings)
    {
        string provider = string.IsNullOrWhiteSpace(settings.AiProvider) ? "BuiltIn" : settings.AiProvider;
        string model = AiModelCatalog.ResolveModelId(settings.AiProvider, settings.AiModelName);
        return $"{provider}|{model}|{GetCacheKey(name, exePath)}";
    }

    public static string GetCacheKey(string name, string exePath)
    {
        return $"{name?.Trim()}::{Path.GetFileName(exePath)?.Trim()}".ToLowerInvariant();
    }

    public static StartupAiReport? GetCachedReport(StartupItem item)
    {
        string key = GetCacheKey(item.Name, item.ExePath, SettingsService.Current);
        return MemoryCache.TryGetValue(key, out var report) ? report : null;
    }

    public static async Task<StartupAiReport> AnalyzeStartupItemAsync(StartupItem item, bool forceOnline = false, CancellationToken ct = default)
    {
        var settings = SettingsService.Current;
        string cacheKey = GetCacheKey(item.Name, item.ExePath, settings);

        StartupAiReport report;

        // The cache is consulted only when AI is enabled, so turning it off genuinely stops
        // using cached cloud answers.
        if (!settings.EnableOnlineAiSafety)
        {
            report = GenerateLocalIntelligenceReport(item);
        }
        else if (!forceOnline && MemoryCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }
        else
        {
            try
            {
                report = await QueryProviderAsync(item, settings, ct);
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
                    $"[Deltempo Startup AI] Provider failure ({settings.AiProvider}/{ex.Kind}): {ex.Message}");

                report = GenerateLocalIntelligenceReport(item);
                report.ProviderUsed = $"Local Intelligence (Fallback - {ex.Kind})";
            }
            catch (Exception ex)
            {
                LastProviderError = null;
                System.Diagnostics.Trace.WriteLine($"[Deltempo Startup AI] Query failed ({settings.AiProvider}): {ex.Message}. Falling back to Local Intelligence.");
                report = GenerateLocalIntelligenceReport(item);
                report.ProviderUsed = "Local Intelligence (Fallback)";
            }
        }

        report.ItemName = item.Name;
        report.Publisher = item.Publisher;
        report.Command = item.Command;
        report.ExePath = item.ExePath;
        report.ApplyBadgeColors();

        // Only a real result is cached. Caching a fallback pinned the item to an offline
        // answer that could never be refreshed without clearing every unrelated entry.
        if (!report.ProviderUsed.Contains("(Fallback", StringComparison.Ordinal))
        {
            MemoryCache[cacheKey] = report;
            SaveCache();
        }

        return report;
    }

    /// <summary>Most recent provider failure, for the UI to surface. Cleared on success.</summary>
    public static AiProviderException? LastProviderError { get; private set; }

    private static async Task<StartupAiReport> QueryProviderAsync(StartupItem item, AppSettings settings, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.AiProvider) ||
            settings.AiProvider.Equals("BuiltIn", StringComparison.OrdinalIgnoreCase))
        {
            LastProviderError = null;
            return GenerateLocalIntelligenceReport(item);
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
            SystemPrompt: BuildSystemPrompt(),
            UserPrompt: BuildUserMessage(item),
            Endpoint: settings.AiOllamaEndpoint,
            Temperature: 0.2), ct);

        LastProviderError = null;

        var report = ParseStructuredAiResponse(rawText, $"{settings.AiProvider} ({model})");
        return report;
    }

    public static StartupAiReport GenerateLocalIntelligenceReport(StartupItem item)
    {
        var entry = StartupIntelligenceCatalog.FindCatalogEntry(item.Name, item.ExePath, item.Command) ??
                    StartupIntelligenceCatalog.ClassifyUnknownStartup(item.Name, item.ExePath, item.Command, item.Publisher);

        var report = new StartupAiReport
        {
            ItemName = item.Name,
            Publisher = item.Publisher,
            Command = item.Command,
            ExePath = item.ExePath,
            WhatIsIt = entry.WhatIsIt,
            Verdict = entry.Verdict,
            ImpactIfDisabled = entry.ImpactIfDisabled,
            Recommendation = entry.Recommendation,
            ProviderUsed = "Built-in Catalog"
        };
        report.ApplyBadgeColors();
        return report;
    }

    #region Multi-Provider AI Implementation

    private static string BuildSystemPrompt()
    {
        return @"You are Deltempo's Windows Startup Intelligence & Boot Safety Engine.
Your task is to analyze a Windows startup program or background service, and provide the user with clear, accurate answers to:
1. What does this program do when Windows boots?
2. WILL ANYTHING GO WRONG IF THE USER DISABLES THIS STARTUP ITEM?
3. What is the safety verdict (SafeToDisable, Caution, or DoNotDisable)?

Rules:
- Output MUST be valid JSON only. No markdown formatting, no code blocks, no other text.
- JSON structure:
{
  ""what_is_it"": ""1-2 sentences explaining what the program/service does."",
  ""disable_verdict"": ""SafeToDisable"" | ""Caution"" | ""DoNotDisable"",
  ""impact_if_disabled"": ""Direct answer: What happens if disabled? For games/chat (Steam, Discord, Spotify), clearly state nothing breaks and they can launch it manually anytime. For cloud sync (OneDrive) or peripherals (Logitech G HUB), explain the sync/profile delay."",
  ""recommendation"": ""Clear recommendation on whether to disable for boot optimization.""
}";
    }

    private static string BuildUserMessage(StartupItem item)
    {
        return $@"Analyze this Windows startup program:
Name: {item.Name}
Display Title: {item.DisplayTitle}
Publisher: {item.Publisher}
Executable Path: {item.ExePath}
Command Line: {item.Command}
Location: {item.LocationDisplay}
Boot Impact: {item.ImpactText}";
    }


    private static StartupAiReport ParseStructuredAiResponse(string rawJson, string provider)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            string whatIsIt = root.TryGetProperty("what_is_it", out var w) ? w.GetString() ?? "" : "";
            string verdictStr = root.TryGetProperty("disable_verdict", out var v) ? v.GetString() ?? "SafeToDisable" : "SafeToDisable";
            string impact = root.TryGetProperty("impact_if_disabled", out var imp) ? imp.GetString() ?? "" : "";
            string rec = root.TryGetProperty("recommendation", out var r) ? r.GetString() ?? "" : "";

            var verdict = StartupDisableVerdict.SafeToDisable;
            if (verdictStr.Contains("DoNotDisable", StringComparison.OrdinalIgnoreCase) ||
                verdictStr.Contains("Critical", StringComparison.OrdinalIgnoreCase) ||
                verdictStr.Contains("Essential", StringComparison.OrdinalIgnoreCase))
            {
                verdict = StartupDisableVerdict.DoNotDisable;
            }
            else if (verdictStr.Contains("Caution", StringComparison.OrdinalIgnoreCase) ||
                     verdictStr.Contains("Warning", StringComparison.OrdinalIgnoreCase))
            {
                verdict = StartupDisableVerdict.Caution;
            }

            var report = new StartupAiReport
            {
                WhatIsIt = whatIsIt,
                Verdict = verdict,
                ImpactIfDisabled = impact,
                Recommendation = rec,
                ProviderUsed = provider
            };
            report.ApplyBadgeColors();
            return report;
        }
        catch
        {
            return new StartupAiReport
            {
                WhatIsIt = "Windows startup entry.",
                Verdict = StartupDisableVerdict.SafeToDisable,
                ImpactIfDisabled = "Nothing will go wrong. The application won't launch at boot, but will function normally when opened.",
                Recommendation = "Safe to disable.",
                ProviderUsed = $"{provider} (Parsed Fallback)"
            };
        }
    }

    #endregion

    #region Persistent Cache Management

    private static void LoadCache()
    {
        try
        {
            if (!File.Exists(CacheFile)) return;

            string json = File.ReadAllText(CacheFile, Encoding.UTF8);
            var list = JsonSerializer.Deserialize<List<StartupAiReport>>(json);
            if (list == null) return;

            foreach (var r in list)
            {
                r.ApplyBadgeColors();
                string key = GetCacheKey(r.ItemName, r.ExePath);
                MemoryCache[key] = r;
            }
        }
        catch
        {
            // Ignore corrupted cache
        }
    }

    public static void SaveCache()
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

                var list = new List<StartupAiReport>(MemoryCache.Values);
                string json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(CacheFile, json, Encoding.UTF8);
            }
            finally
            {
                CacheLock.ExitWriteLock();
            }
        }
        catch
        {
            // Ignore disk write errors
        }
    }

    #endregion
}
