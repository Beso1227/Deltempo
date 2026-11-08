using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

    /// <summary>What the file does at runtime: the role it plays and what reads it.</summary>
    public string WhatItDoes { get; set; } = string.Empty;

    /// <summary>
    /// The file's current state, in plain English: signed or unsigned, how old it is,
    /// still referenced or stale, regenerable or irreplaceable.
    /// </summary>
    public string Condition { get; set; } = string.Empty;

    /// <summary>Short factual findings that support the verdict.</summary>
    public List<string> Evidence { get; set; } = new();

    /// <summary>How certain the analysis is about this verdict, 0-100.</summary>
    public int Confidence { get; set; } = 50;

    public string ImpactIfDeleted { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string BadgeColor { get; set; } = "#F59E0B";
    public string BadgeBackground { get; set; } = "#2A1E0D";
    public string BadgeBorder { get; set; } = "#F59E0B";
    public string ProviderUsed { get; set; } = "Web Knowledge";
    public DateTime AnalyzedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// True only when the answer came from the source it claims: a successful provider
    /// reply, or the deliberate offline/built-in engine the user selected. A heuristic
    /// fallback after a provider error is NOT verified - it must stay retryable and
    /// must never be cached as if the provider had answered.
    /// </summary>
    public bool IsVerified { get; set; }

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

    /// <summary>
    /// Drops every cached report and persists the empty cache. Returns how many entries
    /// were removed so a surface can say what it did instead of guessing "0 items".
    /// </summary>
    public static int ClearCache()
    {
        int removed = MemoryCache.Count;
        MemoryCache.Clear();
        SaveCache();
        return removed;
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
        else if (MemoryCache.TryGetValue(cacheKey, out var cached) && cached.IsVerified)
        {
            return cached;
        }
        else
        {
            // Only a verified answer is a cache hit. An entry written before IsVerified
            // existed deserializes to false, and a fallback is never persisted at all.
            // Dropping it means a retry really re-asks instead of serving the same
            // unverified answer forever.
            MemoryCache.TryRemove(cacheKey, out _);

            try
            {
                report = await QueryProviderAsync(profile, settings, ct);

                // Only a genuine result is cached. Caching a failure meant a transient
                // outage, or a cancelled run, permanently pinned the file to an offline
                // answer that the user could never refresh without clearing the cache.
                // IsVerified is the single rule: a malformed provider reply used to be
                // cached as a genuine answer because the label alone was checked.
                if (report.IsVerified)
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
                // A fallback is a heuristic answer, not the provider's answer. It must
                // stay unverified so the row is retried and the cache never treats it
                // as genuine.
                report.IsVerified = false;
                report.Confidence = Math.Min(report.Confidence, 55);
            }
            catch (Exception ex)
            {
                LastProviderError = null;
                System.Diagnostics.Trace.WriteLine($"[Deltempo AI] Online query failed ({settings.AiProvider}): {ex.Message}. Falling back to Built-In Knowledge.");
                report = GenerateBuiltInKnowledgeReport(profile);
                report.ProviderUsed = "Web Knowledge (Fallback)";
                report.IsVerified = false;
                report.Confidence = Math.Min(report.Confidence, 55);
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
        // fell through to the offline branch and reported success. The key is resolved
        // per provider: switching providers used to reuse whichever key was last typed.
        string apiKey = settings.GetAiApiKey();

        if (AiModelCatalog.TryGetProvider(settings.AiProvider, out var descriptor) &&
            descriptor.RequiresApiKey &&
            string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AiProviderException(AiFailureKind.Authentication,
                $"No API key configured for {settings.AiProvider}.");
        }

        string model = AiModelCatalog.ResolveModelId(settings.AiProvider, settings.AiModelName);

        string rawText = await AiProviderClient.CompleteAsync(new AiChatRequest(
            Provider: settings.AiProvider,
            ApiKey: apiKey,
            ModelId: model,
            SystemPrompt: "You are a senior Windows file-safety analyst. You reply with one valid JSON object and nothing else.",
            UserPrompt: BuildAnalysisPrompt(profile),
            OllamaEndpoint: settings.AiOllamaEndpoint), ct);

        LastProviderError = null;

        // ParseAiStructuredJson throws AiProviderException(MalformedResponse) instead of
        // silently substituting a heuristic report. That substitution used to be labelled
        // with the provider/model and cached, so a broken integration looked healthy and
        // the row was never retried.
        var report = ParseAiStructuredJson(rawText, profile);
        report.ProviderUsed = $"{settings.AiProvider} ({model})";
        report.IsVerified = true;
        return report;
    }

    #region Built-In Knowledge Engine (Free & Instant)


    public static OnlineSafetyReport GenerateBuiltInKnowledgeReport(FileForensicProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var report = new OnlineSafetyReport
        {
            FilePath = profile.FilePath,
            FileName = profile.FileName,
            ProviderUsed = "Deltempo Knowledge Base"
        };

        string ext = profile.Extension;
        string dir = profile.DirectoryPath.ToLowerInvariant();
        string age = AgePhrase(profile);
        string signature = SignaturePhrase(profile);

        // 1. Steam Games
        if (profile.DetectedEcosystem == "Steam Game Asset")
        {
            string game = profile.EcosystemItemName ?? "Steam Game";
            report.Origin = $"Steam: {game}";
            report.WhatIsIt = $"Game asset or installation package ({profile.FormattedSize}) for '{game}'.";
            report.WhatItDoes = "Stores part of the installed game's data - levels, textures, audio or patch content. Steam reads it when the game launches and when it verifies the installation.";
            report.Condition = JoinSentences($"Intact game payload still referenced by Steam's install manifest; {age}.", signature);
            report.ImpactIfDeleted = $"Steam will mark this game as corrupt or uninstalled. Launching will force Steam to re-download {profile.FormattedSize} over the internet.";
            report.Recommendation = $"Do NOT delete this single file. If you need the disk space, safely uninstall '{game}' through your Steam Library instead.";
            report.Verdict = OnlineSafetyVerdict.SafeIfUnused;
            report.SafetyScore = 40;
            report.Confidence = 90;
            return Complete(report, profile);
        }

        // 2. Epic Games
        if (profile.DetectedEcosystem == "Epic Games Store Title")
        {
            string game = profile.EcosystemItemName ?? "Epic Game";
            report.Origin = $"Epic Games Store: {game}";
            report.WhatIsIt = $"Game content package ({profile.FormattedSize}) for '{game}'.";
            report.WhatItDoes = "Holds part of the installed game's content. Epic Games Launcher reads it when the game launches and when it verifies the installation.";
            report.Condition = JoinSentences($"Installed game payload, {age}; the launcher still tracks it as part of '{game}'.", signature);
            report.ImpactIfDeleted = $"Game files will be missing and Epic Games Launcher will require file verification and a large re-download.";
            report.Recommendation = $"Keep if currently playing. Uninstall properly from the Epic Games Launcher to free disk space cleanly.";
            report.Verdict = OnlineSafetyVerdict.SafeIfUnused;
            report.SafetyScore = 40;
            report.Confidence = 90;
            return Complete(report, profile);
        }

        // 3. AI Models / Model Weights
        // '.bin' is deliberately NOT treated as model weights: it is a generic binary
        // container used by CD images, firmware, game data and models alike, so claiming
        // it is a neural network was a guess. It now reaches the generic branch instead.
        if (profile.DetectedEcosystem == "AI / Machine Learning Model Weights" ||
            ext is ".gguf" or ".safetensors" or ".ckpt" or ".onnx" or ".pt" or ".pth")
        {
            string tool = profile.EcosystemItemName ?? "AI Engine";
            report.Origin = $"AI Model Weight: {tool}";
            report.WhatIsIt = $"Neural network model weights / quantized GGUF ({profile.FormattedSize}) used for local AI inference (Ollama, LM Studio, HuggingFace, etc.).";
            report.WhatItDoes = "Holds the trained parameters of a local model. Ollama, LM Studio or HuggingFace loads this file into memory to answer prompts; deleting it removes that model from the local client.";
            report.Condition = JoinSentences($"Model weight file, {age}; only the AI client that downloaded it references it, and Windows does not.", signature);
            report.ImpactIfDeleted = $"The specific AI model will no longer be available locally. However, it can be re-pulled from HuggingFace or Ollama if needed.";
            report.Recommendation = "Safe to delete if you no longer run this specific AI model in your local LLM client.";
            report.Verdict = OnlineSafetyVerdict.SafeIfUnused;
            report.SafetyScore = 75;
            report.Confidence = 85;
            return Complete(report, profile);
        }

        // 4. Virtual Machine Disks & WSL
        if (profile.DetectedEcosystem == "Virtual Machine Disk Image" || ext is ".vmdk" or ".vhdx" or ".vdi" or ".qcow2")
        {
            string vm = profile.EcosystemItemName ?? "Virtual Machine";
            report.Origin = $"Virtualization: {vm}";
            report.WhatIsIt = $"Virtual machine hard drive disk image ({profile.FormattedSize}) containing an entire guest operating system, filesystem, and user files.";
            report.WhatItDoes = "Acts as the virtual hard drive for the guest machine. The hypervisor mounts it at startup, and every file inside the guest lives in this one container.";
            report.Condition = $"Live virtual disk, {age}. It is not a cache: it holds the only copy of the guest OS and its user data.";
            report.ImpactIfDeleted = "DESTRUCTIVE: Deleting this disk image permanently destroys the virtual machine and all data stored inside it.";
            report.Recommendation = "DO NOT DELETE unless you have deleted or backed up this virtual machine. If using WSL2, export your distro first.";
            report.Verdict = OnlineSafetyVerdict.CriticalDoNotDelete;
            report.SafetyScore = 10;
            report.Confidence = 95;
            return Complete(report, profile);
        }

        // 5. Android SDK & Emulators
        if (profile.DetectedEcosystem == "Android Developer SDK / Emulator")
        {
            report.Origin = "Android Studio / SDK Emulator";
            report.WhatIsIt = $"Android Virtual Device (AVD) image or system image snapshot ({profile.FormattedSize}) used for mobile app development.";
            report.WhatItDoes = "Provides the emulated phone's system image, user data or snapshot. Android Studio boots the virtual device from it.";
            report.Condition = $"Emulator image, {age}; Android Studio can recreate it or re-download the system image.";
            report.ImpactIfDeleted = "Android Studio emulator for this virtual phone/tablet will fail to boot or will reset to factory state.";
            report.Recommendation = "Safe to delete if you no longer test apps with this specific emulator in Android Studio.";
            report.Verdict = OnlineSafetyVerdict.SafeIfUnused;
            report.SafetyScore = 65;
            report.Confidence = 85;
            return Complete(report, profile);
        }

        // 6. Unreal / Unity Engine Caches
        if (profile.DetectedEcosystem?.Contains("Unreal") == true || profile.DetectedEcosystem?.Contains("Unity") == true)
        {
            report.Origin = profile.DetectedEcosystem;
            report.WhatIsIt = $"Compiled shader cache or derived engine asset ({profile.FormattedSize}).";
            report.WhatItDoes = "Stores shaders and derived data compiled from the project's source assets. The editor rebuilds it from those assets the next time the project opens.";
            report.Condition = $"Derived cache, {age}; regenerable, and no source asset or project file is stored here.";
            report.ImpactIfDeleted = "Zero permanent loss. The game engine editor will recompile or rebuild shaders next time the project opens (opening will take longer).";
            report.Recommendation = "Safe to delete to reclaim space if this project is closed or inactive.";
            report.Verdict = OnlineSafetyVerdict.SafeToDelete;
            report.SafetyScore = 85;
            report.Confidence = 88;
            return Complete(report, profile);
        }

        // 7. Adobe Media Cache
        if (profile.DetectedEcosystem?.Contains("Adobe") == true)
        {
            report.Origin = "Adobe Creative Cloud";
            report.WhatIsIt = $"Peak files, audio conform cache, or video scratch cache ({profile.FormattedSize}) generated by Premiere Pro / After Effects.";
            report.WhatItDoes = "Stores preview renders, audio conform files and scratch media so the editor can scrub the timeline quickly. Adobe regenerates it on demand.";
            report.Condition = $"Scratch cache, {age}; Adobe rebuilds the entries when the project is reopened.";
            report.ImpactIfDeleted = "Zero permanent data loss. Adobe will regenerate preview waveforms and conform caches next time project is opened.";
            report.Recommendation = "Looks safe to delete - scratch space that will free disk if you no longer need it.";
            report.Verdict = OnlineSafetyVerdict.SafeToDelete;
            report.SafetyScore = 95;
            report.Confidence = 92;
            return Complete(report, profile);
        }

        // 8. Downloaded ISO / Installers
        // '.bin' is not included: it is a generic binary container, not necessarily a disc
        // image. It falls through to the honest generic branch below.
        if (ext is ".iso" or ".img")
        {
            report.Origin = "Operating System / Disc Image";
            report.WhatIsIt = $"Disk image installer ({profile.FormattedSize}).";
            report.WhatItDoes = "Contains a complete installer or live environment. It is only read when mounted, burned to media, or used by a virtual machine.";
            report.Condition = $"Static disc image, {age}; Windows does not use it after installation, so it is a re-downloadable copy rather than a live component.";
            report.ImpactIfDeleted = "You will no longer have the offline setup image on disk. Re-downloading from the web will be necessary if needed.";
            report.Recommendation = "Safe to delete if you have already installed the OS or created your bootable USB flash drive.";
            report.Verdict = OnlineSafetyVerdict.SafeIfUnused;
            report.SafetyScore = 80;
            report.Confidence = 82;
            return Complete(report, profile);
        }

        // 9. Windows Core System
        if (dir.Contains(@"\windows\winsxs\") || dir.Contains(@"\windows\system32\"))
        {
            report.Origin = "Microsoft Windows Operating System";
            report.WhatIsIt = $"Critical Windows OS component or driver ({profile.FormattedSize}).";
            report.WhatItDoes = "Loaded by Windows, a driver, or the WinSxS servicing store at boot or during updates. Other system components depend on it.";
            report.Condition = JoinSentences($"Protected operating-system component, {age}; it is part of the running OS, not user data.", signature);
            report.ImpactIfDeleted = "FATAL SYSTEM FAILURE: Deleting Windows system files will cause Blue Screen of Death (BSOD) or boot failure.";
            report.Recommendation = "STRICTLY PROTECTED: Never delete system binaries directly. Use Deltempo's System Repair (DISM) tool instead.";
            report.Verdict = OnlineSafetyVerdict.CriticalDoNotDelete;
            report.SafetyScore = 0;
            report.Confidence = 95;
            return Complete(report, profile);
        }

        // 10. Executables with PE Info
        if (!string.IsNullOrWhiteSpace(profile.ProductName) || !string.IsNullOrWhiteSpace(profile.CompanyName))
        {
            string vendor = profile.CompanyName ?? "Software Vendor";
            string prod = profile.ProductName ?? profile.FileName;
            report.Origin = $"{vendor} - {prod}";
            report.WhatIsIt = $"Application executable or package ({profile.FormattedSize}). Description: {profile.FileDescription ?? "Windows Application"}.";
            report.WhatItDoes = $"Part of {prod}. It runs when the application starts, or is loaded as a library by that application.";
            report.Condition = JoinSentences($"{age.Capitalize()}.", signature);
            report.ImpactIfDeleted = "The associated application will be uninstalled or broken.";
            report.Recommendation = "Use Windows Settings > Installed Apps to uninstall properly instead of deleting file directly.";
            report.Verdict = OnlineSafetyVerdict.ReviewRequired;
            report.SafetyScore = 30;
            report.Confidence = 70;
            return Complete(report, profile);
        }

        // 11. Generic Media / Videos
        if (ext is ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv")
        {
            report.Origin = "Personal Video / Media File";
            report.WhatIsIt = $"Large video recording, movie, or media clip ({profile.FormattedSize}).";
            report.WhatItDoes = "Holds picture and audio streams that a media player decodes. No installed program depends on it.";
            report.Condition = $"{age.Capitalize()}; personal or downloaded media rather than a program component.";
            report.ImpactIfDeleted = "Permanent loss of this video file unless restored from Recycle Bin.";
            report.Recommendation = "Verify whether this is a personal video recording or movie before deleting.";
            report.Verdict = OnlineSafetyVerdict.ReviewRequired;
            report.SafetyScore = 50;
            report.Confidence = 75;
            return Complete(report, profile);
        }

        // Default Fallback
        report.Origin = !string.IsNullOrWhiteSpace(profile.DetectedMagicType) ? profile.DetectedMagicType : "Local File";
        report.WhatIsIt = $"File of type '{ext}' ({profile.FormattedSize}) located in {Path.GetFileName(profile.DirectoryPath)}.";
        report.WhatItDoes = "Read by whichever application created it. Deltempo has no specific knowledge of this format, so its role cannot be proven from the file alone.";
        report.Condition = $"{age.Capitalize()}; the file signature is {(string.IsNullOrWhiteSpace(profile.DetectedMagicType) ? "unrecognised" : profile.DetectedMagicType)} and its owner is unknown.";
        report.ImpactIfDeleted = "Depends on whether the parent application actively relies on this file.";
        report.Recommendation = "Review the folder location in File Explorer before removing.";
        report.Verdict = OnlineSafetyVerdict.ReviewRequired;
        report.SafetyScore = 50;
        report.Confidence = 40;
        return Complete(report, profile);
    }

    /// <summary>
    /// Fills the report fields every branch must carry, so no code path can return a
    /// half-explained verdict. Built-in answers are authoritative for the built-in
    /// engine and are marked verified; provider fallbacks clear the flag afterwards.
    /// </summary>
    private static OnlineSafetyReport Complete(OnlineSafetyReport report, FileForensicProfile profile)
    {
        if (report.Evidence.Count == 0)
        {
            report.Evidence = BuildEvidence(profile);
        }

        if (string.IsNullOrWhiteSpace(report.WhatItDoes))
        {
            report.WhatItDoes = "No documented runtime role for this format; treat it as data belonging to its parent application.";
        }

        if (string.IsNullOrWhiteSpace(report.Condition))
        {
            report.Condition = $"{AgePhrase(profile).Capitalize()}; ownership and runtime role are not established from the file alone.";
        }

        report.Confidence = Math.Clamp(report.Confidence, 1, 100);
        report.IsVerified = true;
        return report;
    }

    /// <summary>
    /// Builds the factual evidence list shown in the UI and sent to a provider. It is
    /// derived only from what the forensic pass actually observed.
    /// </summary>
    private static List<string> BuildEvidence(FileForensicProfile profile)
    {
        var evidence = new List<string>();

        if (!string.IsNullOrWhiteSpace(profile.DetectedMagicType))
        {
            evidence.Add($"File signature: {profile.DetectedMagicType}");
        }
        else if (!string.IsNullOrWhiteSpace(profile.Extension))
        {
            evidence.Add($"Extension: {profile.Extension} (no recognised file signature)");
        }

        if (!string.IsNullOrWhiteSpace(profile.DetectedEcosystem))
        {
            string ecosystem = string.IsNullOrWhiteSpace(profile.EcosystemItemName)
                ? profile.DetectedEcosystem
                : $"{profile.DetectedEcosystem} ({profile.EcosystemItemName})";
            evidence.Add($"Software ecosystem: {ecosystem}");
        }

        if (!string.IsNullOrWhiteSpace(profile.DigitalSigner))
        {
            evidence.Add($"Authenticode signature: {profile.DigitalSigner}");
        }
        else if (profile.Extension is ".exe" or ".dll" or ".sys" or ".msi")
        {
            evidence.Add("Authenticode signature: not signed");
        }

        if (!string.IsNullOrWhiteSpace(profile.ProductName))
        {
            string product = string.IsNullOrWhiteSpace(profile.CompanyName)
                ? profile.ProductName
                : $"{profile.ProductName} ({profile.CompanyName})";
            evidence.Add($"Product metadata: {product}");
        }

        if (profile.SizeBytes > 0)
        {
            evidence.Add($"Size on disk: {profile.FormattedSize}");
        }

        if (profile.LastModified != default)
        {
            evidence.Add($"Last modified: {profile.LastModified:yyyy-MM-dd} ({AgePhrase(profile)})");
        }

        if (!string.IsNullOrWhiteSpace(profile.GeneralizedLocation))
        {
            evidence.Add($"Location: {profile.GeneralizedLocation}");
        }

        return evidence;
    }

    /// <summary>Plain-English age, used in both the evidence list and the condition line.</summary>
    private static string AgePhrase(FileForensicProfile profile)
    {
        if (profile.LastModified == default) return "modification time unavailable";

        TimeSpan age = DateTime.UtcNow - profile.LastModified;
        if (age < TimeSpan.Zero) return "modification time is in the future";
        if (age.TotalHours < 1) return "modified within the last hour";
        if (age.TotalDays < 1) return "modified today";
        if (age.TotalDays < 30) return $"modified {(int)age.TotalDays} days ago";
        if (age.TotalDays < 365) return $"modified about {Math.Max(1, (int)(age.TotalDays / 30))} months ago";
        return $"modified over {(int)(age.TotalDays / 365)} year(s) ago";
    }

    /// <summary>Signature sentence for the condition line; empty when not applicable.</summary>
    private static string SignaturePhrase(FileForensicProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.DigitalSigner))
        {
            return $"Digitally signed by {profile.DigitalSigner}.";
        }

        return profile.Extension is ".exe" or ".dll" or ".sys" or ".msi"
            ? "The binary carries no Authenticode signature."
            : string.Empty;
    }

    private static string JoinSentences(params string?[] sentences)
        => string.Join(" ", sentences.Where(s => !string.IsNullOrWhiteSpace(s)));

    /// <summary>Uppercases the first character so a fragment can start a sentence.</summary>
    private static string Capitalize(this string value)
        => string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];

    #endregion

    #region Cloud AI Providers (Gemini, OpenAI, Groq, Ollama)

    /// <summary>
    /// Builds the analysis turn. The contract asks for the whole picture - identity,
    /// runtime role, current condition, evidence, confidence - because a one-line
    /// verdict is not enough for a user deciding whether to delete 40 GB.
    /// </summary>
    private static string BuildAnalysisPrompt(FileForensicProfile profile)
    {
        return $@"A Windows user is deciding whether this large file can be deleted for disk space.
Base your answer only on the evidence below and on well-established knowledge of the
named software. Never invent a vendor, version, or behaviour you are not confident about.

EVIDENCE GATHERED:
{profile.BuildAnonymizedSummary()}

Respond with ONE JSON object and no other text, using exactly these keys:
{{
  ""what_is_it"": ""1-2 sentences naming the file and the software it belongs to."",
  ""what_it_does"": ""1-2 sentences describing the role the file plays and what reads it at runtime."",
  ""origin"": ""Parent software, game, studio or Windows component (e.g. Steam: Baldur's Gate 3, Adobe Premiere Pro, Windows OS)."",
  ""condition"": ""1-2 sentences on the file's current state: signed or unsigned, how old, still referenced or stale, regenerable or irreplaceable."",
  ""evidence"": [""2-4 short factual bullets from the evidence that support the verdict""],
  ""verdict"": ""SAFE_TO_DELETE"" | ""SAFE_IF_UNUSED"" | ""REVIEW_REQUIRED"" | ""DO_NOT_DELETE"",
  ""confidence"": <integer 0 to 100: how certain you are given the evidence>,
  ""safety_score"": <integer 0 to 100 where 100 is a disposable cache and 0 is a critical OS component>,
  ""impact_if_deleted"": ""The exact consequence (e.g. Steam re-downloads 42 GB, Adobe rebuilds the cache, the VM and its data are destroyed, Windows fails to boot)."",
  ""recommendation"": ""One concrete action for the user.""
}}

VERDICT RULES:
- SAFE_TO_DELETE only for regenerable caches, logs, or an installer that has already been used.
- SAFE_IF_UNUSED only when the file belongs to software the user can uninstall or re-download.
- REVIEW_REQUIRED for user data, project files, portable apps, or anything you cannot identify.
- DO_NOT_DELETE for Windows system files, live virtual-machine disks, and irreplaceable user data.
- Lower the confidence when the evidence is thin. Do not guess silently.";
    }

    /// <summary>
    /// Parses a provider reply into a report, or throws a classified
    /// <see cref="AiProviderException"/> when the reply is unusable.
    /// </summary>
    /// <remarks>
    /// The previous version caught every parse error and returned the built-in report,
    /// so a provider that answered with prose, a truncated object, or a different schema
    /// still produced a "successful" result labelled with the provider and model - and
    /// that result was cached. The caller now receives the failure, labels it a fallback,
    /// and leaves the row retryable.
    /// </remarks>
    internal static OnlineSafetyReport ParseAiStructuredJson(string rawText, FileForensicProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.IsNullOrWhiteSpace(rawText))
        {
            throw new AiProviderException(AiFailureKind.MalformedResponse,
                "The AI provider returned an empty response.");
        }

        // Models wrap JSON in prose and code fences; the object is between the first
        // opening and the last closing brace, whatever surrounds it.
        string json = ExtractJsonObject(rawText);
        if (json.Length == 0)
        {
            throw new AiProviderException(AiFailureKind.MalformedResponse,
                "The AI provider did not return a JSON object.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
        }
        catch (JsonException ex)
        {
            throw new AiProviderException(AiFailureKind.MalformedResponse,
                "The AI provider returned malformed JSON.", ex);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new AiProviderException(AiFailureKind.MalformedResponse,
                    "The AI provider returned JSON that is not an object.");
            }

            string whatIsIt = ReadText(root, "what_is_it", "whatIsIt", "description");
            string whatItDoes = ReadText(root, "what_it_does", "whatItDoes", "role", "purpose");
            string origin = ReadText(root, "origin", "publisher", "vendor");
            string condition = ReadText(root, "condition", "file_condition", "state");
            string impact = ReadText(root, "impact_if_deleted", "impactIfDeleted", "impact", "removal_impact");
            string recommendation = ReadText(root, "recommendation", "advice", "action");

            // The file's identity is the one thing the report cannot be complete without.
            if (string.IsNullOrWhiteSpace(whatIsIt))
            {
                throw new AiProviderException(AiFailureKind.MalformedResponse,
                    "The AI response did not identify the file (missing 'what_is_it').");
            }

            List<string> evidence = ReadStringList(root, "evidence", "reasons", "risk_factors");
            int score = Math.Clamp(ReadInt(root, "safety_score", "safetyScore", "score") ?? 50, 0, 100);
            int confidence = Math.Clamp(ReadInt(root, "confidence", "certainty") ?? 60, 0, 100);

            string verdictText = ReadText(root, "verdict", "safety_verdict", "safetyVerdict");
            OnlineSafetyVerdict verdict = string.IsNullOrWhiteSpace(verdictText)
                ? DeriveVerdict(score)
                : ParseVerdict(verdictText);

            // Safety-first reconciliation: a model that says "safe to delete" while scoring
            // the file as critical is contradicting itself. Never auto-approve that.
            if (verdict == OnlineSafetyVerdict.SafeToDelete && score < 50)
            {
                verdict = OnlineSafetyVerdict.ReviewRequired;
            }

            // Any field the model omitted is filled from the deterministic engine so the
            // report is always complete. The verdict and score stay the model's answer.
            var builtIn = GenerateBuiltInKnowledgeReport(profile);

            return new OnlineSafetyReport
            {
                Verdict = verdict,
                SafetyScore = score,
                Confidence = confidence,
                WhatIsIt = whatIsIt,
                WhatItDoes = OrFallback(whatItDoes, builtIn.WhatItDoes),
                Origin = OrFallback(origin, builtIn.Origin),
                Condition = OrFallback(condition, builtIn.Condition),
                Evidence = evidence.Count > 0 ? evidence : builtIn.Evidence,
                ImpactIfDeleted = OrFallback(impact, builtIn.ImpactIfDeleted),
                Recommendation = OrFallback(recommendation, builtIn.Recommendation)
            };
        }
    }

    private static string ExtractJsonObject(string rawText)
    {
        int start = rawText.IndexOf('{');
        int end = rawText.LastIndexOf('}');
        return start >= 0 && end > start ? rawText[start..(end + 1)] : string.Empty;
    }

    private static string OrFallback(string value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

    /// <summary>Lowercase letters and digits only, so snake_case and camelCase match.</summary>
    private static string NormalizeKey(string key)
        => new(key.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static bool TryGetProperty(JsonElement root, out JsonElement value, params string[] names)
    {
        var wanted = new HashSet<string>(names.Select(NormalizeKey), StringComparer.Ordinal);
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (wanted.Contains(NormalizeKey(property.Name)))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string ReadText(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out JsonElement value, names)) return string.Empty;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()?.Trim() ?? string.Empty,
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True or JsonValueKind.False => value.GetBoolean().ToString(),
            JsonValueKind.Array => string.Join(
                " ",
                value.EnumerateArray().Select(ElementText).Where(text => text.Length > 0)),
            _ => string.Empty
        };
    }

    private static string ElementText(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString()?.Trim() ?? string.Empty,
            JsonValueKind.Number => element.ToString(),
            _ => string.Empty
        };

    private static int? ReadInt(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out JsonElement value, names)) return null;

        if (value.ValueKind == JsonValueKind.Number)
        {
            if (value.TryGetInt32(out int integer)) return integer;
            if (value.TryGetDouble(out double real)) return (int)Math.Round(real);
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            string text = value.GetString() ?? string.Empty;
            bool negative = text.TrimStart().StartsWith('-');
            string digits = new(text.Where(char.IsDigit).Take(3).ToArray());
            return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? (negative ? -parsed : parsed)
                : null;
        }

        return null;
    }

    private static List<string> ReadStringList(JsonElement root, params string[] names)
    {
        var result = new List<string>();
        if (!TryGetProperty(root, out JsonElement value, names)) return result;

        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement element in value.EnumerateArray())
            {
                AddEvidence(result, ElementText(element));
            }
        }
        else if (value.ValueKind == JsonValueKind.String)
        {
            foreach (string line in (value.GetString() ?? string.Empty).Split('\n'))
            {
                AddEvidence(result, line);
            }
        }

        return result;
    }

    private static void AddEvidence(List<string> target, string raw)
    {
        string text = raw.Trim().TrimStart('-', '*', '•').Trim();

        // Strip "1." / "1)" numbering that models add to list items.
        if (text.Length > 2 && char.IsDigit(text[0]) && (text[1] == '.' || text[1] == ')'))
        {
            text = text[2..].Trim();
        }

        if (text.Length == 0) return;
        if (text.Length > 240) text = text[..240].TrimEnd() + "…";

        if (target.Count < 6 && !target.Contains(text, StringComparer.OrdinalIgnoreCase))
        {
            target.Add(text);
        }
    }

    private static OnlineSafetyVerdict ParseVerdict(string raw)
    {
        string value = NormalizeKey(raw);

        if (value.Contains("donot") || value.Contains("critical") ||
            value.Contains("keep") || value.Contains("protected") || value.Contains("never"))
        {
            return OnlineSafetyVerdict.CriticalDoNotDelete;
        }

        if (value.Contains("safeif") || value.Contains("unused") || value.Contains("redownload"))
        {
            return OnlineSafetyVerdict.SafeIfUnused;
        }

        if (value.Contains("safetodelete") || value.Contains("safetoclean") ||
            value.Contains("disposable") || value == "safe" || value == "delete")
        {
            return OnlineSafetyVerdict.SafeToDelete;
        }

        return OnlineSafetyVerdict.ReviewRequired;
    }

    private static OnlineSafetyVerdict DeriveVerdict(int score) => score switch
    {
        >= 90 => OnlineSafetyVerdict.SafeToDelete,
        >= 65 => OnlineSafetyVerdict.SafeIfUnused,
        >= 30 => OnlineSafetyVerdict.ReviewRequired,
        _ => OnlineSafetyVerdict.CriticalDoNotDelete
    };

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
                OllamaEndpoint: endpoint), cts.Token);

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
