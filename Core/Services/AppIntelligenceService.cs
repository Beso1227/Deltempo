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

    public static AppAiReport? GetCachedReport(InstalledAppItem app)
    {
        string key = GetCacheKey(app.Publisher, app.DisplayName);
        return MemoryCache.TryGetValue(key, out var report) ? report : null;
    }

    public static async Task<AppAiReport> AnalyzeAppAsync(InstalledAppItem app, bool forceOnline = false, CancellationToken ct = default)
    {
        string cacheKey = GetCacheKey(app.Publisher, app.DisplayName);

        if (!forceOnline && MemoryCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var settings = SettingsService.Current;
        AppAiReport report;

        // Route to AI provider if enabled and configured
        if (!settings.EnableOnlineAiSafety)
        {
            report = GenerateLocalIntelligenceReport(app);
        }
        else
        {
            try
            {
                report = settings.AiProvider switch
                {
                    "Gemini" when !string.IsNullOrWhiteSpace(settings.AiApiKey) =>
                        await QueryGeminiAsync(app, settings.AiApiKey, settings.AiModelName, ct),

                    "OpenAI" when !string.IsNullOrWhiteSpace(settings.AiApiKey) =>
                        await QueryOpenAiCompatibleAsync(app, "https://api.openai.com/v1/chat/completions", settings.AiApiKey,
                            string.IsNullOrWhiteSpace(settings.AiModelName) ? "gpt-4o-mini" : settings.AiModelName, ct),

                    "Groq" when !string.IsNullOrWhiteSpace(settings.AiApiKey) =>
                        await QueryOpenAiCompatibleAsync(app, "https://api.groq.com/openai/v1/chat/completions", settings.AiApiKey,
                            string.IsNullOrWhiteSpace(settings.AiModelName) ? "llama-3.3-70b-versatile" : settings.AiModelName, ct),

                    "OpenRouter" when !string.IsNullOrWhiteSpace(settings.AiApiKey) =>
                        await QueryOpenAiCompatibleAsync(app, "https://openrouter.ai/api/v1/chat/completions", settings.AiApiKey,
                            string.IsNullOrWhiteSpace(settings.AiModelName) ? "meta-llama/llama-3.3-70b-instruct" : settings.AiModelName, ct, isOpenRouter: true),

                    "Ollama" =>
                        await QueryOllamaAsync(app, settings.AiOllamaEndpoint, settings.AiModelName, ct),

                    _ => GenerateLocalIntelligenceReport(app)
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo App AI] Query failed ({settings.AiProvider}): {ex.Message}. Falling back to Local Intelligence.");
                report = GenerateLocalIntelligenceReport(app);
                report.ProviderUsed = "Local Intelligence (Fallback)";
            }
        }

        report.AppName = app.DisplayName;
        report.Publisher = app.Publisher;
        report.DisplayVersion = app.DisplayVersion;
        report.ApplyBadgeColors();

        MemoryCache[cacheKey] = report;
        SaveCache();

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

    private static string BuildPrompt(InstalledAppItem app)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a Windows application architecture and software security specialist.");
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

    private static async Task<AppAiReport> QueryGeminiAsync(
        InstalledAppItem app,
        string apiKey,
        string? modelOverride,
        CancellationToken ct)
    {
        string model = string.IsNullOrWhiteSpace(modelOverride) ? "gemini-2.0-flash" : modelOverride;
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        string prompt = BuildPrompt(app);

        var requestBody = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = 0.1
            }
        };

        string jsonPayload = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        using var res = await HttpClient.PostAsync(url, content, ct);
        if (!res.IsSuccessStatusCode)
        {
            string err = await res.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Gemini API error ({res.StatusCode}): {err}");
        }

        string responseJson = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);

        var candidates = doc.RootElement.GetProperty("candidates");
        if (candidates.GetArrayLength() == 0) throw new InvalidOperationException("Gemini returned empty response.");

        string rawText = candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "{}";
        var report = ParseAiStructuredJson(rawText, app);
        report.ProviderUsed = $"Google Gemini ({model})";
        return report;
    }

    private static async Task<AppAiReport> QueryOpenAiCompatibleAsync(
        InstalledAppItem app,
        string endpoint,
        string apiKey,
        string model,
        CancellationToken ct,
        bool isOpenRouter = false)
    {
        string prompt = BuildPrompt(app);

        var requestBody = new
        {
            model = model,
            messages = new[]
            {
                new { role = "system", content = "You are a Windows software safety expert. Output valid JSON only." },
                new { role = "user", content = prompt }
            },
            temperature = 0.1,
            response_format = new { type = "json_object" }
        };

        string jsonPayload = JsonSerializer.Serialize(requestBody);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        if (isOpenRouter || endpoint.Contains("openrouter.ai"))
        {
            request.Headers.Add("HTTP-Referer", "https://github.com/Beso1227/Deltempo");
            request.Headers.Add("X-Title", "Deltempo App Intelligence");
        }
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        using var res = await HttpClient.SendAsync(request, ct);
        if (!res.IsSuccessStatusCode)
        {
            string err = await res.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"AI API error ({res.StatusCode}): {err}");
        }

        string responseJson = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);

        var choices = doc.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() == 0) throw new InvalidOperationException("AI returned empty choices.");

        string rawText = choices[0].GetProperty("message").GetProperty("content").GetString() ?? "{}";
        var report = ParseAiStructuredJson(rawText, app);
        report.ProviderUsed = endpoint.Contains("groq") ? $"Groq ({model})" : (isOpenRouter ? $"OpenRouter ({model})" : $"OpenAI ({model})");
        return report;
    }

    private static async Task<AppAiReport> QueryOllamaAsync(
        InstalledAppItem app,
        string? endpointOverride,
        string? modelOverride,
        CancellationToken ct)
    {
        string endpoint = string.IsNullOrWhiteSpace(endpointOverride) ? "http://localhost:11434" : endpointOverride.TrimEnd('/');
        string model = string.IsNullOrWhiteSpace(modelOverride) ? "llama3.2" : modelOverride;
        string url = $"{endpoint}/api/generate";

        string prompt = BuildPrompt(app);

        var requestBody = new
        {
            model = model,
            prompt = prompt,
            stream = false,
            format = "json",
            options = new { temperature = 0.1 }
        };

        string jsonPayload = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        using var res = await HttpClient.PostAsync(url, content, ct);
        if (!res.IsSuccessStatusCode)
        {
            string err = await res.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Ollama API error ({res.StatusCode}): {err}");
        }

        string responseJson = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);

        string rawText = doc.RootElement.GetProperty("response").GetString() ?? "{}";
        var report = ParseAiStructuredJson(rawText, app);
        report.ProviderUsed = $"Ollama ({model})";
        return report;
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
