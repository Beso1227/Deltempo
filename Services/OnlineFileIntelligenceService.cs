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

public enum OnlineSafetyVerdict
{
    SafeToDelete,       // Safe to delete, disposable cache or old installer
    SafeIfUnused,       // Safe if user no longer uses or wants the parent app/game, can be re-downloaded
    ReviewRequired,     // User data, config, portable app, or uncertain impact
    CriticalDoNotDelete // Windows OS system file, hypervisor disk, critical project
}

public class OnlineSafetyReport
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public OnlineSafetyVerdict Verdict { get; set; } = OnlineSafetyVerdict.ReviewRequired;
    public string VerdictDisplay { get; set; } = "REVIEW REQUIRED";
    public int SafetyScore { get; set; } = 50;
    public string Origin { get; set; } = string.Empty;
    public string WhatIsIt { get; set; } = string.Empty;
    public string ImpactIfDeleted { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string BadgeColor { get; set; } = "#F59E0B";
    public string BadgeBackground { get; set; } = "#2A1E0D";
    public string BadgeBorder { get; set; } = "#F59E0B";
    public string ProviderUsed { get; set; } = "Web Knowledge";
    public DateTime AnalyzedAtUtc { get; set; } = DateTime.UtcNow;

    public void ApplyBadgeColors()
    {
        switch (Verdict)
        {
            case OnlineSafetyVerdict.SafeToDelete:
                BadgeColor = "#10B981";      // Emerald Green
                BadgeBackground = "#0D2818";
                BadgeBorder = "#10B981";
                VerdictDisplay = "SAFE TO CLEAN";
                break;
            case OnlineSafetyVerdict.SafeIfUnused:
                BadgeColor = "#06B6D4";      // Electric Cyan
                BadgeBackground = "#0C2329";
                BadgeBorder = "#06B6D4";
                VerdictDisplay = "SAFE IF UNUSED";
                break;
            case OnlineSafetyVerdict.CriticalDoNotDelete:
                BadgeColor = "#EF4444";      // Destructive Red
                BadgeBackground = "#2A0E0E";
                BadgeBorder = "#EF4444";
                VerdictDisplay = "DO NOT DELETE";
                break;
            case OnlineSafetyVerdict.ReviewRequired:
            default:
                BadgeColor = "#F59E0B";      // Warning Amber
                BadgeBackground = "#2A1E0D";
                BadgeBorder = "#F59E0B";
                VerdictDisplay = "REVIEW REQUIRED";
                break;
        }
    }
}

public static class OnlineFileIntelligenceService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private static readonly string CacheFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Deltempo",
        "ai_safety_cache.json");

    private static readonly ConcurrentDictionary<string, OnlineSafetyReport> MemoryCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ReaderWriterLockSlim CacheLock = new();

    static OnlineFileIntelligenceService()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Deltempo-Guardian", "1.5.1"));
        LoadCache();
    }

    public static int GetCacheCount() => MemoryCache.Count;

    public static void ClearCache()
    {
        MemoryCache.Clear();
        SaveCache();
    }

    private static string GetCacheKey(FileForensicProfile profile)
    {
        return $"{profile.FileName}_{profile.SizeBytes}_{profile.LastModified.Ticks}";
    }

    public static async Task<OnlineSafetyReport> AnalyzeFileAsync(
        string filePath,
        CancellationToken ct = default)
    {
        var profile = FileForensicsService.AnalyzeFile(filePath);
        string cacheKey = GetCacheKey(profile);

        if (MemoryCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var settings = SettingsService.Current;
        OnlineSafetyReport report;

        // Route to selected AI provider or fall back to built-in knowledge
        if (!settings.EnableOnlineAiSafety)
        {
            report = GenerateBuiltInKnowledgeReport(profile);
            report.ProviderUsed = "Local Intelligence (Offline)";
        }
        else
        {
            try
            {
                report = settings.AiProvider switch
                {
                    "Gemini" when !string.IsNullOrWhiteSpace(settings.AiApiKey) =>
                        await QueryGeminiAsync(profile, settings.AiApiKey, settings.AiModelName, ct),

                    "OpenAI" when !string.IsNullOrWhiteSpace(settings.AiApiKey) =>
                        await QueryOpenAiCompatibleAsync(profile, "https://api.openai.com/v1/chat/completions", settings.AiApiKey,
                            string.IsNullOrWhiteSpace(settings.AiModelName) ? "gpt-4o-mini" : settings.AiModelName, ct),

                    "Groq" when !string.IsNullOrWhiteSpace(settings.AiApiKey) =>
                        await QueryOpenAiCompatibleAsync(profile, "https://api.groq.com/openai/v1/chat/completions", settings.AiApiKey,
                            string.IsNullOrWhiteSpace(settings.AiModelName) ? "llama-3.3-70b-versatile" : settings.AiModelName, ct),

                    "OpenRouter" when !string.IsNullOrWhiteSpace(settings.AiApiKey) =>
                        await QueryOpenAiCompatibleAsync(profile, "https://openrouter.ai/api/v1/chat/completions", settings.AiApiKey,
                            string.IsNullOrWhiteSpace(settings.AiModelName) ? "meta-llama/llama-3.3-70b-instruct" : settings.AiModelName, ct, isOpenRouter: true),

                    "Ollama" =>
                        await QueryOllamaAsync(profile, settings.AiOllamaEndpoint, settings.AiModelName, ct),

                    _ => await QueryBuiltInKnowledgeAsync(profile, ct)
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo AI] Online query failed ({settings.AiProvider}): {ex.Message}. Falling back to Built-In Knowledge.");
                report = GenerateBuiltInKnowledgeReport(profile);
                report.ProviderUsed = "Web Knowledge (Fallback)";
            }
        }

        report.FilePath = filePath;
        report.FileName = profile.FileName;
        report.ApplyBadgeColors();

        MemoryCache[cacheKey] = report;
        SaveCache();

        return report;
    }

    #region Built-In Knowledge Engine (Free & Instant)

    private static async Task<OnlineSafetyReport> QueryBuiltInKnowledgeAsync(FileForensicProfile profile, CancellationToken ct)
    {
        // First check high-confidence local fingerprint knowledge base
        var localKnowledge = GenerateBuiltInKnowledgeReport(profile);
        if (localKnowledge.SafetyScore != 50 || !string.IsNullOrWhiteSpace(profile.DetectedEcosystem))
        {
            localKnowledge.ProviderUsed = "Deltempo Web Knowledge";
            return localKnowledge;
        }

        // If unknown, search DuckDuckGo Instant Answer for the file / software name
        try
        {
            string queryName = Path.GetFileNameWithoutExtension(profile.FileName);
            if (queryName.Length > 2)
            {
                string url = $"https://api.duckduckgo.com/?q={Uri.EscapeDataString(queryName + " file")}&format=json&no_html=1&skip_disambig=1";
                using var res = await HttpClient.GetAsync(url, ct);
                if (res.IsSuccessStatusCode)
                {
                    string json = await res.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    string? abstractText = doc.RootElement.TryGetProperty("AbstractText", out var abs) ? abs.GetString() : null;
                    string? heading = doc.RootElement.TryGetProperty("Heading", out var h) ? h.GetString() : null;

                    if (!string.IsNullOrWhiteSpace(abstractText))
                    {
                        localKnowledge.WhatIsIt = abstractText;
                        localKnowledge.Origin = string.IsNullOrWhiteSpace(heading) ? "Online Software Database" : heading;
                        localKnowledge.Recommendation = "Identified via public software knowledge base. Review before removing.";
                        localKnowledge.ProviderUsed = "Web Knowledge (DuckDuckGo)";
                        return localKnowledge;
                    }
                }
            }
        }
        catch { }

        localKnowledge.ProviderUsed = "Deltempo Software Knowledge";
        return localKnowledge;
    }

    public static OnlineSafetyReport GenerateBuiltInKnowledgeReport(FileForensicProfile profile)
    {
        var report = new OnlineSafetyReport
        {
            FilePath = profile.FilePath,
            FileName = profile.FileName,
            ProviderUsed = "Deltempo Knowledge Base"
        };

        string ext = profile.Extension;
        string name = profile.FileName.ToLowerInvariant();
        string dir = profile.DirectoryPath.ToLowerInvariant();

        // 1. Steam Games
        if (profile.DetectedEcosystem == "Steam Game Asset")
        {
            string game = profile.EcosystemItemName ?? "Steam Game";
            report.Origin = $"Steam: {game}";
            report.WhatIsIt = $"Game asset or installation package ({profile.FormattedSize}) for '{game}'.";
            report.ImpactIfDeleted = $"Steam will mark this game as corrupt or uninstalled. Launching will force Steam to re-download {profile.FormattedSize} over the internet.";
            report.Recommendation = $"Do NOT delete this single file. If you need the disk space, safely uninstall '{game}' through your Steam Library instead.";
            report.Verdict = OnlineSafetyVerdict.SafeIfUnused;
            report.SafetyScore = 40;
            return report;
        }

        // 2. Epic Games
        if (profile.DetectedEcosystem == "Epic Games Store Title")
        {
            string game = profile.EcosystemItemName ?? "Epic Game";
            report.Origin = $"Epic Games Store: {game}";
            report.WhatIsIt = $"Game content package ({profile.FormattedSize}) for '{game}'.";
            report.ImpactIfDeleted = $"Game files will be missing and Epic Games Launcher will require file verification and a large re-download.";
            report.Recommendation = $"Keep if currently playing. Uninstall properly from the Epic Games Launcher to free disk space cleanly.";
            report.Verdict = OnlineSafetyVerdict.SafeIfUnused;
            report.SafetyScore = 40;
            return report;
        }

        // 3. AI Models / Model Weights
        if (profile.DetectedEcosystem == "AI / Machine Learning Model Weights" || ext is ".gguf" or ".safetensors" or ".ckpt" or ".bin")
        {
            string tool = profile.EcosystemItemName ?? "AI Engine";
            report.Origin = $"AI Model Weight: {tool}";
            report.WhatIsIt = $"Neural network model weights / quantized GGUF ({profile.FormattedSize}) used for local AI inference (Ollama, LM Studio, HuggingFace, etc.).";
            report.ImpactIfDeleted = $"The specific AI model will no longer be available locally. However, it can be re-pulled from HuggingFace or Ollama if needed.";
            report.Recommendation = "Safe to delete if you no longer run this specific AI model in your local LLM client.";
            report.Verdict = OnlineSafetyVerdict.SafeIfUnused;
            report.SafetyScore = 75;
            return report;
        }

        // 4. Virtual Machine Disks & WSL
        if (profile.DetectedEcosystem == "Virtual Machine Disk Image" || ext is ".vmdk" or ".vhdx" or ".vdi" or ".qcow2")
        {
            string vm = profile.EcosystemItemName ?? "Virtual Machine";
            report.Origin = $"Virtualization: {vm}";
            report.WhatIsIt = $"Virtual machine hard drive disk image ({profile.FormattedSize}) containing an entire guest operating system, filesystem, and user files.";
            report.ImpactIfDeleted = "DESTRUCTIVE: Deleting this disk image permanently destroys the virtual machine and all data stored inside it.";
            report.Recommendation = "DO NOT DELETE unless you have deleted or backed up this virtual machine. If using WSL2, export your distro first.";
            report.Verdict = OnlineSafetyVerdict.CriticalDoNotDelete;
            report.SafetyScore = 10;
            return report;
        }

        // 5. Android SDK & Emulators
        if (profile.DetectedEcosystem == "Android Developer SDK / Emulator")
        {
            report.Origin = "Android Studio / SDK Emulator";
            report.WhatIsIt = $"Android Virtual Device (AVD) image or system image snapshot ({profile.FormattedSize}) used for mobile app development.";
            report.ImpactIfDeleted = "Android Studio emulator for this virtual phone/tablet will fail to boot or will reset to factory state.";
            report.Recommendation = "Safe to delete if you no longer test apps with this specific emulator in Android Studio.";
            report.Verdict = OnlineSafetyVerdict.SafeIfUnused;
            report.SafetyScore = 65;
            return report;
        }

        // 6. Unreal / Unity Engine Caches
        if (profile.DetectedEcosystem?.Contains("Unreal") == true || profile.DetectedEcosystem?.Contains("Unity") == true)
        {
            report.Origin = profile.DetectedEcosystem;
            report.WhatIsIt = $"Compiled shader cache or derived engine asset ({profile.FormattedSize}).";
            report.ImpactIfDeleted = "Zero permanent loss. The game engine editor will recompile or rebuild shaders next time the project opens (opening will take longer).";
            report.Recommendation = "Safe to delete to reclaim space if this project is closed or inactive.";
            report.Verdict = OnlineSafetyVerdict.SafeToDelete;
            report.SafetyScore = 85;
            return report;
        }

        // 7. Adobe Media Cache
        if (profile.DetectedEcosystem?.Contains("Adobe") == true)
        {
            report.Origin = "Adobe Creative Cloud";
            report.WhatIsIt = $"Peak files, audio conform cache, or video scratch cache ({profile.FormattedSize}) generated by Premiere Pro / After Effects.";
            report.ImpactIfDeleted = "Zero permanent data loss. Adobe will regenerate preview waveforms and conform caches next time project is opened.";
            report.Recommendation = "100% Safe to delete to free gigabytes of scratch space.";
            report.Verdict = OnlineSafetyVerdict.SafeToDelete;
            report.SafetyScore = 95;
            return report;
        }

        // 8. Downloaded ISO / Installers
        if (ext is ".iso" or ".img")
        {
            report.Origin = "Operating System / Disc Image";
            report.WhatIsIt = $"Disk image installer ({profile.FormattedSize}).";
            report.ImpactIfDeleted = "You will no longer have the offline setup image on disk. Re-downloading from the web will be necessary if needed.";
            report.Recommendation = "Safe to delete if you have already installed the OS or created your bootable USB flash drive.";
            report.Verdict = OnlineSafetyVerdict.SafeIfUnused;
            report.SafetyScore = 80;
            return report;
        }

        // 9. Windows Core System
        if (dir.Contains(@"\windows\winsxs\") || dir.Contains(@"\windows\system32\"))
        {
            report.Origin = "Microsoft Windows Operating System";
            report.WhatIsIt = $"Critical Windows OS component or driver ({profile.FormattedSize}).";
            report.ImpactIfDeleted = "FATAL SYSTEM FAILURE: Deleting Windows system files will cause Blue Screen of Death (BSOD) or boot failure.";
            report.Recommendation = "STRICTLY PROTECTED: Never delete system binaries directly. Use Deltempo's System Repair (DISM) tool instead.";
            report.Verdict = OnlineSafetyVerdict.CriticalDoNotDelete;
            report.SafetyScore = 0;
            return report;
        }

        // 10. Executables with PE Info
        if (!string.IsNullOrWhiteSpace(profile.ProductName) || !string.IsNullOrWhiteSpace(profile.CompanyName))
        {
            string vendor = profile.CompanyName ?? "Software Vendor";
            string prod = profile.ProductName ?? profile.FileName;
            report.Origin = $"{vendor} - {prod}";
            report.WhatIsIt = $"Application executable or package ({profile.FormattedSize}). Description: {profile.FileDescription ?? "Windows Application"}.";
            report.ImpactIfDeleted = "The associated application will be uninstalled or broken.";
            report.Recommendation = "Use Windows Settings > Installed Apps to uninstall properly instead of deleting file directly.";
            report.Verdict = OnlineSafetyVerdict.ReviewRequired;
            report.SafetyScore = 30;
            return report;
        }

        // 11. Generic Media / Videos
        if (ext is ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv")
        {
            report.Origin = "Personal Video / Media File";
            report.WhatIsIt = $"Large video recording, movie, or media clip ({profile.FormattedSize}).";
            report.ImpactIfDeleted = "Permanent loss of this video file unless restored from Recycle Bin.";
            report.Recommendation = "Verify whether this is a personal video recording or movie before deleting.";
            report.Verdict = OnlineSafetyVerdict.ReviewRequired;
            report.SafetyScore = 50;
            return report;
        }

        // Default Fallback
        report.Origin = !string.IsNullOrWhiteSpace(profile.DetectedMagicType) ? profile.DetectedMagicType : "Local File";
        report.WhatIsIt = $"File of type '{ext}' ({profile.FormattedSize}) located in {Path.GetFileName(profile.DirectoryPath)}.";
        report.ImpactIfDeleted = "Depends on whether the parent application actively relies on this file.";
        report.Recommendation = "Review the folder location in File Explorer before removing.";
        report.Verdict = OnlineSafetyVerdict.ReviewRequired;
        report.SafetyScore = 50;
        return report;
    }

    #endregion

    #region Cloud AI Providers (Gemini, OpenAI, Groq, Ollama)

    private static async Task<OnlineSafetyReport> QueryGeminiAsync(
        FileForensicProfile profile,
        string apiKey,
        string? modelOverride,
        CancellationToken ct)
    {
        string model = string.IsNullOrWhiteSpace(modelOverride) ? "gemini-2.0-flash" : modelOverride;
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        string prompt = BuildSystemPrompt(profile);

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

        string rawText = candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";
        var report = ParseAiStructuredJson(rawText, profile);
        report.ProviderUsed = $"Google Gemini ({model})";
        return report;
    }

    private static async Task<OnlineSafetyReport> QueryOpenAiCompatibleAsync(
        FileForensicProfile profile,
        string endpoint,
        string apiKey,
        string model,
        CancellationToken ct,
        bool isOpenRouter = false)
    {
        string prompt = BuildSystemPrompt(profile);

        var requestBody = new
        {
            model = model,
            messages = new[]
            {
                new { role = "system", content = "You are a Windows PC storage & file safety expert. Output valid JSON only." },
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
            request.Headers.Add("X-Title", "Deltempo File Safety");
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

        string rawText = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        var report = ParseAiStructuredJson(rawText, profile);
        report.ProviderUsed = endpoint.Contains("openrouter") ? $"OpenRouter ({model})" : endpoint.Contains("groq") ? $"Groq ({model})" : $"OpenAI ({model})";
        return report;
    }

    private static async Task<OnlineSafetyReport> QueryOllamaAsync(
        FileForensicProfile profile,
        string endpoint,
        string? modelOverride,
        CancellationToken ct)
    {
        string cleanEndpoint = (endpoint ?? "http://localhost:11434").TrimEnd('/');
        string url = $"{cleanEndpoint}/api/generate";
        string model = string.IsNullOrWhiteSpace(modelOverride) ? "llama3" : modelOverride;

        string prompt = BuildSystemPrompt(profile);

        var requestBody = new
        {
            model = model,
            prompt = prompt,
            format = "json",
            stream = false
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

        string rawText = doc.RootElement.GetProperty("response").GetString() ?? "";
        var report = ParseAiStructuredJson(rawText, profile);
        report.ProviderUsed = $"Ollama Local ({model})";
        return report;
    }

    private static string BuildSystemPrompt(FileForensicProfile profile)
    {
        return $@"Analyze the safety of deleting this large file from a Windows PC for disk cleanup.
EVIDENCE GATHERED:
{profile.BuildAnonymizedSummary()}

REQUIREMENTS:
Evaluate what this file is, who created it, and what happens if deleted.
You MUST respond with pure JSON only matching this exact structure:
{{
  ""what_is_it"": ""Clear 1-2 sentence explanation of what this file is and its role"",
  ""origin"": ""Parent Software or Studio name (e.g. Steam: Baldur's Gate 3, Docker Desktop, Adobe Premiere, Android Studio, Windows OS)"",
  ""verdict"": ""SAFE_TO_DELETE"" | ""SAFE_IF_UNUSED"" | ""REVIEW_REQUIRED"" | ""DO_NOT_DELETE"",
  ""safety_score"": <integer from 0 to 100 where 100 is completely safe disposable cache and 0 is critical OS component>,
  ""impact_if_deleted"": ""Exact consequences if the user deletes this file (e.g. game forces redownload, app will recreate cache, VM will be destroyed, Windows fails to boot)"",
  ""recommendation"": ""Concrete advice for the user (e.g. Safe to delete to free space, or Uninstall properly through Steam, or Keep file)""
}}";
    }

    private static OnlineSafetyReport ParseAiStructuredJson(string rawJson, FileForensicProfile profile)
    {
        try
        {
            // Clean markdown code fence if present
            string json = rawJson.Trim();
            if (json.StartsWith("```"))
            {
                int firstLine = json.IndexOf('\n');
                int lastFence = json.LastIndexOf("```");
                if (firstLine > 0 && lastFence > firstLine)
                    json = json.Substring(firstLine + 1, lastFence - firstLine - 1).Trim();
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string verdictStr = root.TryGetProperty("verdict", out var v) ? v.GetString() ?? "" : "";
            var verdict = verdictStr.ToUpperInvariant() switch
            {
                "SAFE_TO_DELETE" => OnlineSafetyVerdict.SafeToDelete,
                "SAFE_IF_UNUSED" => OnlineSafetyVerdict.SafeIfUnused,
                "DO_NOT_DELETE" => OnlineSafetyVerdict.CriticalDoNotDelete,
                _ => OnlineSafetyVerdict.ReviewRequired
            };

            int score = root.TryGetProperty("safety_score", out var s) && s.TryGetInt32(out int scoreVal)
                ? Math.Clamp(scoreVal, 0, 100)
                : 50;

            string whatIsIt = root.TryGetProperty("what_is_it", out var w) ? w.GetString() ?? "" : "";
            string origin = root.TryGetProperty("origin", out var o) ? o.GetString() ?? "" : "";
            string impact = root.TryGetProperty("impact_if_deleted", out var imp) ? imp.GetString() ?? "" : "";
            string rec = root.TryGetProperty("recommendation", out var r) ? r.GetString() ?? "" : "";

            return new OnlineSafetyReport
            {
                Verdict = verdict,
                SafetyScore = score,
                WhatIsIt = whatIsIt,
                Origin = origin,
                ImpactIfDeleted = impact,
                Recommendation = rec
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo AI] Failed to parse AI JSON: {ex.Message}. Raw: {rawJson}");
            var fallback = GenerateBuiltInKnowledgeReport(profile);
            return fallback;
        }
    }

    #endregion

    #region Connection Testing

    public static async Task<(bool Success, string Message)> TestConnectionAsync(
        string provider,
        string apiKey,
        string endpoint,
        string modelName)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));

            switch (provider)
            {
                case "Gemini":
                    if (string.IsNullOrWhiteSpace(apiKey)) return (false, "API Key is required for Google Gemini.");
                    string geminiModel = string.IsNullOrWhiteSpace(modelName) ? "gemini-2.0-flash" : modelName;
                    string gUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{geminiModel}:generateContent?key={apiKey}";
                    var gBody = new { contents = new[] { new { parts = new[] { new { text = "ping" } } } } };
                    using (var gContent = new StringContent(JsonSerializer.Serialize(gBody), Encoding.UTF8, "application/json"))
                    {
                        var gRes = await HttpClient.PostAsync(gUrl, gContent, cts.Token);
                        if (gRes.IsSuccessStatusCode) return (true, $"Connected successfully to Google Gemini ({geminiModel})!");
                        string err = await gRes.Content.ReadAsStringAsync(cts.Token);
                        return (false, $"Gemini Error ({gRes.StatusCode}): {err}");
                    }

                case "OpenAI":
                    if (string.IsNullOrWhiteSpace(apiKey)) return (false, "API Key is required for OpenAI.");
                    string oModel = string.IsNullOrWhiteSpace(modelName) ? "gpt-4o-mini" : modelName;
                    using (var oReq = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions"))
                    {
                        oReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                        var oBody = new { model = oModel, messages = new[] { new { role = "user", content = "ping" } }, max_tokens = 5 };
                        oReq.Content = new StringContent(JsonSerializer.Serialize(oBody), Encoding.UTF8, "application/json");
                        var oRes = await HttpClient.SendAsync(oReq, cts.Token);
                        if (oRes.IsSuccessStatusCode) return (true, $"Connected successfully to OpenAI ({oModel})!");
                        string err = await oRes.Content.ReadAsStringAsync(cts.Token);
                        return (false, $"OpenAI Error ({oRes.StatusCode}): {err}");
                    }

                case "Groq":
                    if (string.IsNullOrWhiteSpace(apiKey)) return (false, "API Key is required for Groq.");
                    string grModel = string.IsNullOrWhiteSpace(modelName) ? "llama-3.3-70b-versatile" : modelName;
                    using (var grReq = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions"))
                    {
                        grReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                        var grBody = new { model = grModel, messages = new[] { new { role = "user", content = "ping" } }, max_tokens = 5 };
                        grReq.Content = new StringContent(JsonSerializer.Serialize(grBody), Encoding.UTF8, "application/json");
                        var grRes = await HttpClient.SendAsync(grReq, cts.Token);
                        if (grRes.IsSuccessStatusCode) return (true, $"Connected successfully to Groq ({grModel})!");
                        string err = await grRes.Content.ReadAsStringAsync(cts.Token);
                        return (false, $"Groq Error ({grRes.StatusCode}): {err}");
                    }

                case "OpenRouter":
                    if (string.IsNullOrWhiteSpace(apiKey)) return (false, "API Key is required for OpenRouter.");
                    string orModel = string.IsNullOrWhiteSpace(modelName) ? "meta-llama/llama-3.3-70b-instruct" : modelName;
                    using (var orReq = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions"))
                    {
                        orReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                        orReq.Headers.Add("HTTP-Referer", "https://github.com/Beso1227/Deltempo");
                        orReq.Headers.Add("X-Title", "Deltempo");
                        var orBody = new { model = orModel, messages = new[] { new { role = "user", content = "ping" } }, max_tokens = 5 };
                        orReq.Content = new StringContent(JsonSerializer.Serialize(orBody), Encoding.UTF8, "application/json");
                        var orRes = await HttpClient.SendAsync(orReq, cts.Token);
                        if (orRes.IsSuccessStatusCode) return (true, $"Connected successfully to OpenRouter ({orModel})!");
                        string err = await orRes.Content.ReadAsStringAsync(cts.Token);
                        return (false, $"OpenRouter Error ({orRes.StatusCode}): {err}");
                    }

                case "Ollama":
                    string oEnd = (endpoint ?? "http://localhost:11434").TrimEnd('/');
                    var oResTest = await HttpClient.GetAsync($"{oEnd}/api/tags", cts.Token);
                    if (oResTest.IsSuccessStatusCode) return (true, $"Connected successfully to local Ollama instance at {oEnd}!");
                    return (false, $"Ollama responded with HTTP {oResTest.StatusCode}");

                case "BuiltIn":
                default:
                    return (true, "Built-in Knowledge Engine is ready (100% free, no API key required).");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Connection failed: {ex.Message}");
        }
    }

    #endregion

    #region Cache Persistence

    private static void LoadCache()
    {
        try
        {
            Dictionary<string, OnlineSafetyReport>? loaded = null;
            if (File.Exists(CacheFile))
            {
                string json = File.ReadAllText(CacheFile);
                loaded = JsonSerializer.Deserialize<Dictionary<string, OnlineSafetyReport>>(json);
            }

            if (loaded != null)
            {
                CacheLock.EnterWriteLock();
                try
                {
                    foreach (var kvp in loaded)
                    {
                        MemoryCache[kvp.Key] = kvp.Value;
                    }
                }
                finally
                {
                    CacheLock.ExitWriteLock();
                }
            }
        }
        catch { }
    }

    private static void SaveCache()
    {
        try
        {
            string dir = Path.GetDirectoryName(CacheFile)!;
            Directory.CreateDirectory(dir);

            CacheLock.EnterReadLock();
            string json;
            try
            {
                json = JsonSerializer.Serialize(MemoryCache, new JsonSerializerOptions { WriteIndented = false });
            }
            finally
            {
                CacheLock.ExitReadLock();
            }

            CacheLock.EnterWriteLock();
            try
            {
                File.WriteAllText(CacheFile, json);
            }
            finally
            {
                CacheLock.ExitWriteLock();
            }
        }
        catch { }
    }

    #endregion
}
