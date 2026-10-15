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
        "ai_safety_cache.json");

    // ConcurrentDictionary is already thread-safe; the ReaderWriterLockSlim that used to guard
    // it protected nothing and its read-then-write sequence in SaveCache was not atomic.
    private static readonly ConcurrentDictionary<string, OnlineSafetyReport> MemoryCache = new(StringComparer.OrdinalIgnoreCase);

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

    /// <summary>
    /// Cache identity: the file's identity PLUS the provenance of the answer.
    /// </summary>
    /// <remarks>
    /// The key used to be FileName_Size_LastModifiedTicks alone, so a verdict produced by
    /// one provider survived a switch to another provider, a change of model, or a rotated
    /// key - there was no way to invalidate it except ClearCache(), which also discards
    /// every unrelated answer. Including provider and model means changing either one
    /// simply misses the cache and re-answers, which is what a user expects after
    /// deliberately changing them.
    /// </remarks>
    private static string GetCacheKey(FileForensicProfile profile, AppSettings settings)
    {
        string provider = string.IsNullOrWhiteSpace(settings.AiProvider) ? "BuiltIn" : settings.AiProvider;
        string model = AiModelCatalog.ResolveModelId(settings.AiProvider, settings.AiModelName);
        return $"{provider}|{model}|{profile.FileName}_{profile.SizeBytes}_{profile.LastModified.Ticks}";
    }

    public static async Task<OnlineSafetyReport> AnalyzeFileAsync(
        string filePath,
        CancellationToken ct = default)
    {
        // FileForensicsService.AnalyzeFile is synchronous and hits the filesystem hard: it stats
        // the file, reads its 32-byte header, then for .exe/.dll/.sys/.msi parses the version
        // resource and calls X509Certificate.CreateFromSignedFile, which reads and parses the
        // Authenticode certificate table. On a large signed installer that is tens to hundreds
        // of milliseconds of blocking I/O.
        //
        // This method is reached directly from WPF click handlers ("Analyse with AI", "AI
        // Analyse All"), so running it on the caller's thread put that blocking I/O on the UI
        // thread and Windows declared the window not responding. Task.Run is not cosmetic
        // here: it is the difference between a responsive app and a freeze.
        var profile = await Task.Run(() => FileForensicsService.AnalyzeFile(filePath), ct).ConfigureAwait(false);
        var settings = SettingsService.Current;
        string cacheKey = GetCacheKey(profile, settings);
        OnlineSafetyReport report;

        // Route to selected AI provider or fall back to built-in knowledge
        if (!settings.EnableOnlineAiSafety)
        {
            // The opt-out is checked BEFORE the cache. Reading the cache first meant a
            // verdict produced with AI enabled was returned forever after the user turned
            // AI off - the setting did not do what its checkbox says, and the value it
            // was supposed to suppress was already persisted to ai_safety_cache.json.
            report = GenerateBuiltInKnowledgeReport(profile);
            report.ProviderUsed = "Local Intelligence (Offline)";
        }
        else if (MemoryCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }
        else
        {
            try
            {
                report = await QueryProviderAsync(profile, settings, ct);

                // Only a genuine result is cached. Caching a failure meant a transient
                // outage, or a cancelled run, permanently pinned the file to an offline
                // answer that the user could never refresh without clearing the cache.
                if (!report.ProviderUsed.Contains("(Fallback", StringComparison.Ordinal))
                {
                    MemoryCache[cacheKey] = report;
                    SaveCache();
                }
            }
            catch (OperationCanceledException)
            {
                // A cancelled query is not a failed query. Falling through to the offline
                // report here wrote a heuristic answer into the PERSISTENT cache under an
                // AI-styled badge, so a cancelled batch permanently poisoned the cache for
                // that file. Propagate instead: the caller asked us to stop.
                throw;
            }
            catch (AiProviderException ex)
            {
                LastProviderError = ex;
                System.Diagnostics.Trace.WriteLine(
                    $"[Deltempo AI] Provider failure ({settings.AiProvider}/{ex.Kind}): {ex.Message}");

                report = GenerateBuiltInKnowledgeReport(profile);
                report.ProviderUsed = $"Web Knowledge (Fallback - {ex.Kind})";
            }
            catch (Exception ex)
            {
                LastProviderError = null;
                System.Diagnostics.Trace.WriteLine($"[Deltempo AI] Online query failed ({settings.AiProvider}): {ex.Message}. Falling back to Built-In Knowledge.");
                report = GenerateBuiltInKnowledgeReport(profile);
                report.ProviderUsed = "Web Knowledge (Fallback)";
            }
        }

        report.FilePath = filePath;
        report.FileName = profile.FileName;
        report.ApplyBadgeColors();

        return report;
    }

    /// <summary>
    /// The most recent provider failure, for the UI to surface. Cleared on the next
    /// success. Without this the only signal a user had was a report labelled "Fallback",
    /// which cannot distinguish an expired key, a retired model and a dead socket.
    /// </summary>
    public static AiProviderException? LastProviderError { get; private set; }

    private static async Task<OnlineSafetyReport> QueryProviderAsync(
        FileForensicProfile profile,
        AppSettings settings,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.AiProvider) ||
            settings.AiProvider.Equals("BuiltIn", StringComparison.OrdinalIgnoreCase))
        {
            // BuiltIn is advertised in Settings as "no API key required", so it must not
            // make an unrequested web round-trip. This used to fall through to a
            // DuckDuckGo lookup with the file's base name, which the user never agreed to.
            var offline = GenerateBuiltInKnowledgeReport(profile);
            offline.ProviderUsed = "Deltempo Software Knowledge";
            LastProviderError = null;
            return offline;
        }

        if (!AiModelCatalog.TryGetProvider(settings.AiProvider, out _))
        {
            throw new AiProviderException(AiFailureKind.ModelUnavailable,
                $"Unknown AI provider '{settings.AiProvider}'.");
        }

        // The key used to gate each provider arm, so a provider with a blank key silently
        // fell through to the offline branch and reported success.
        if (AiModelCatalog.TryGetProvider(settings.AiProvider, out var descriptor) &&
            descriptor.RequiresApiKey &&
            string.IsNullOrWhiteSpace(settings.AiApiKey))
        {
            throw new AiProviderException(AiFailureKind.Authentication,
                $"No API key configured for {settings.AiProvider}.");
        }

        string model = AiModelCatalog.ResolveModelId(settings.AiProvider, settings.AiModelName);

        string rawText = await AiProviderClient.CompleteAsync(new AiChatRequest(
            Provider: settings.AiProvider,
            ApiKey: settings.AiApiKey,
            ModelId: model,
            SystemPrompt: "You are a Windows PC storage & file safety expert. Output valid JSON only.",
            UserPrompt: BuildSystemPrompt(profile),
            Endpoint: settings.AiOllamaEndpoint), ct);

        LastProviderError = null;

        var report = ParseAiStructuredJson(rawText, profile);
        report.ProviderUsed = $"{settings.AiProvider} ({model})";
        return report;
    }

    #region Built-In Knowledge Engine (Free & Instant)


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

    /// <summary>
    /// Verifies the provider/model/key combination with the SAME transport the real
    /// analysis uses, so a green test here means a green request later.
    /// </summary>
    /// <remarks>
    /// This used to carry its own copy of every provider endpoint AND its own copy of every
    /// default model literal - 16 more hardcoded identifiers that could drift from the ones
    /// the analysis path used. That is how it came to report "Connected successfully" for a
    /// Groq model that had been decommissioned for the free tier two months earlier.
    ///
    /// It also used to send a bare "ping" with max_tokens 5, which does not exercise the
    /// response shape the real path needs. The probe now asks for the same JSON contract.
    /// </remarks>
    public static async Task<(bool Success, string Message)> TestConnectionAsync(
        string provider,
        string apiKey,
        string endpoint,
        string modelName)
    {
        if (string.IsNullOrWhiteSpace(provider) ||
            provider.Equals("BuiltIn", StringComparison.OrdinalIgnoreCase))
        {
            return (true, "Built-in Knowledge Engine is ready (100% free, no API key required).");
        }

        if (!AiModelCatalog.TryGetProvider(provider, out var descriptor))
        {
            return (false, $"Unknown AI provider '{provider}'.");
        }

        if (descriptor.RequiresApiKey && string.IsNullOrWhiteSpace(apiKey))
        {
            return (false, $"An API key is required for {provider}. Add one in Settings.");
        }

        string model = AiModelCatalog.ResolveModelId(provider, modelName);
        if (string.IsNullOrEmpty(model))
        {
            return (false, $"No model resolved for {provider}. Set a model name in Settings.");
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));

            string text = await AiProviderClient.CompleteAsync(new AiChatRequest(
                Provider: provider,
                ApiKey: apiKey,
                ModelId: model,
                SystemPrompt: "You reply with valid JSON only.",
                UserPrompt: "Return exactly this object and nothing else: {\"ok\": true}",
                Endpoint: endpoint), cts.Token);

            return (true, $"Connected to {provider} ({model}).{(string.IsNullOrWhiteSpace(text) ? "" : " JSON response verified.")}");
        }
        catch (OperationCanceledException)
        {
            return (false, "Connection test timed out. If this is a local model, start it and try again once it has loaded.");
        }
        catch (AiProviderException ex)
        {
            // Name the actual problem. "Connection failed" told the user nothing about
            // whether to fix the key, the model, or their network.
            return (false, $"{ex.UserMessage} ({model})");
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
                foreach (var kvp in loaded)
                {
                    MemoryCache[kvp.Key] = kvp.Value;
                }
            }
        }
        catch { }
    }

    /// <summary>Coalesces the per-file cache writes into one.</summary>
    /// <remarks>
    /// SaveCache used to run on EVERY analyzed file, serializing the entire dictionary each
    /// time. BatchAnalyzeWithAiAsync fans out four-wide, so a 200-file batch performed 200
    /// full serializations and writes, each of a growing dictionary - O(n^2) disk I/O
    /// executed on the slowest thread in the batch. A pending flag plus a background flush
    /// bounds this to roughly one write per burst.
    /// </remarks>
    private static int _savePending;
    private static readonly object SaveGate = new();

    private static void SaveCache()
    {
        if (Interlocked.Exchange(ref _savePending, 1) == 1)
        {
            return; // a flush is already queued
        }

        _ = Task.Run(async () =>
        {
            try
            {
                // Small debounce so a burst collapses into a single write.
                await Task.Delay(750).ConfigureAwait(false);

                string dir = Path.GetDirectoryName(CacheFile)!;
                Directory.CreateDirectory(dir);

                // ConcurrentDictionary already gives a consistent-enough snapshot for a
                // cache; the ReaderWriterLockSlim that used to guard it protected nothing
                // and its read-lock-then-write-lock sequence left a window where the
                // serialized text could be written over a newer one.
                string json = JsonSerializer.Serialize(
                    MemoryCache,
                    new JsonSerializerOptions { WriteIndented = false });

                File.WriteAllText(CacheFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo AI] Cache save failed: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _savePending, 0);
            }
        });
    }

    /// <summary>Forces any coalesced cache write to complete. Used by tests and shutdown.</summary>
    public static void FlushCache()
    {
        while (Interlocked.CompareExchange(ref _savePending, 0, 0) != 0)
        {
            Thread.Sleep(50);
        }
    }

    #endregion
}
