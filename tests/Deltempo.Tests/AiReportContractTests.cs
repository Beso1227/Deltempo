using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Guards the "Analyze with AI" report contract: a report must always explain what
/// the file is, what it does, its condition, the evidence behind the verdict, what
/// happens if it is deleted, and what the user should do - from the provider path,
/// the parser, the offline engine, and the row projection.
///
/// The bug this round fixed: a provider reply that was prose, truncated, or in a
/// different schema silently became the built-in report, was labelled with the
/// provider and model, and was cached. A broken integration therefore looked healthy
/// and the row was never retried. Parse failures now raise a classified
/// AiProviderException and fallbacks are explicitly unverified.
/// </summary>
public class AiReportContractTests
{
    private static FileForensicProfile Profile(
        string fileName,
        string dir,
        string ecosystem = "",
        string item = "",
        string? signer = null)
        => new()
        {
            FilePath = Path.Combine(dir, fileName),
            FileName = fileName,
            Extension = Path.GetExtension(fileName).ToLowerInvariant(),
            SizeBytes = 42L * 1024 * 1024 * 1024,
            LastModified = DateTime.UtcNow.AddDays(-400),
            DirectoryPath = dir,
            DetectedEcosystem = ecosystem,
            EcosystemItemName = item,
            DigitalSigner = signer
        };

    // ---------------------------------------------------------------- parser ---

    [Fact]
    public void ParseAiStructuredJson_ProseAndFenceWrapped_ReadsEveryContractField()
    {
        const string raw = """
            Sure, here is my analysis.

            ```json
            {
              "what_is_it": "A Steam game data archive for Baldur's Gate 3.",
              "what_it_does": "Steam reads it at launch and when verifying the install.",
              "origin": "Steam: Baldur's Gate 3",
              "condition": "Intact payload, signed by Larian, modified two years ago.",
              "evidence": ["File signature: PAK archive", "Location: Steam library", "Size: 42 GB"],
              "verdict": "SAFE_IF_UNUSED",
              "confidence": 91,
              "safety_score": 40,
              "impact_if_deleted": "Steam re-downloads 42 GB.",
              "recommendation": "Uninstall the game through Steam instead."
            }
            ```

            Let me know if you need anything else.
            """;

        var report = OnlineFileIntelligenceService.ParseAiStructuredJson(
            raw, Profile("patch.pak", @"D:\Steam\steamapps\common\BG3", "Steam Game Asset", "Baldur's Gate 3"));

        Assert.Equal(OnlineSafetyVerdict.SafeIfUnused, report.Verdict);
        Assert.Equal(40, report.SafetyScore);
        Assert.Equal(91, report.Confidence);
        Assert.Contains("Baldur's Gate 3", report.WhatIsIt);
        Assert.Contains("Steam reads it", report.WhatItDoes);
        Assert.Contains("Intact payload", report.Condition);
        Assert.Equal(3, report.Evidence.Count);
        Assert.Contains("PAK archive", report.Evidence[0]);
        Assert.Equal("Steam re-downloads 42 GB.", report.ImpactIfDeleted);
        Assert.Equal("Uninstall the game through Steam instead.", report.Recommendation);
    }

    [Fact]
    public void ParseAiStructuredJson_AcceptsStringScoreAndCamelCaseKeys()
    {
        const string raw = """
            {"whatIsIt":"A local model file.","whatItDoes":"Loaded by Ollama.","verdict":"safe to delete",
             "safetyScore":"87","confidence":"80%","evidence":"- weights file\n- re-downloadable"}
            """;

        var report = OnlineFileIntelligenceService.ParseAiStructuredJson(
            raw, Profile("model.gguf", @"C:\Users\t\.ollama\models", "AI / Machine Learning Model Weights", "llama"));

        Assert.Equal(OnlineSafetyVerdict.SafeToDelete, report.Verdict);
        Assert.Equal(87, report.SafetyScore);
        Assert.Equal(80, report.Confidence);
        Assert.Equal(2, report.Evidence.Count);
        Assert.Equal("weights file", report.Evidence[0]);
        Assert.Equal("re-downloadable", report.Evidence[1]);
    }

    [Fact]
    public void ParseAiStructuredJson_ProseOnly_ThrowsMalformedResponse()
    {
        var ex = Assert.Throws<AiProviderException>(() =>
            OnlineFileIntelligenceService.ParseAiStructuredJson(
                "I think this is a game file and it is probably fine to delete.",
                Profile("game.pak", @"D:\Games")));

        Assert.Equal(AiFailureKind.MalformedResponse, ex.Kind);
    }

    [Fact]
    public void ParseAiStructuredJson_EmptyReply_ThrowsMalformedResponse()
    {
        var ex = Assert.Throws<AiProviderException>(() =>
            OnlineFileIntelligenceService.ParseAiStructuredJson(
                "   ", Profile("game.pak", @"D:\Games")));

        Assert.Equal(AiFailureKind.MalformedResponse, ex.Kind);
    }

    [Fact]
    public void ParseAiStructuredJson_TruncatedJson_ThrowsMalformedResponse()
    {
        var ex = Assert.Throws<AiProviderException>(() =>
            OnlineFileIntelligenceService.ParseAiStructuredJson(
                """{"what_is_it":"A game file","verdict":"SAFE""",
                Profile("game.pak", @"D:\Games")));

        Assert.Equal(AiFailureKind.MalformedResponse, ex.Kind);
    }

    [Fact]
    public void ParseAiStructuredJson_WithoutFileIdentity_ThrowsMalformedResponse()
    {
        // A verdict with no identity is not a report: the user cannot tell what they
        // are being asked to delete.
        var ex = Assert.Throws<AiProviderException>(() =>
            OnlineFileIntelligenceService.ParseAiStructuredJson(
                """{"verdict":"SAFE_TO_DELETE","safety_score":95}""",
                Profile("game.pak", @"D:\Games")));

        Assert.Equal(AiFailureKind.MalformedResponse, ex.Kind);
    }

    [Fact]
    public void ParseAiStructuredJson_OmitsOptionalFields_FillsThemFromTheBuiltInEngine()
    {
        const string raw = """{"what_is_it":"A Steam game archive.","verdict":"SAFE_IF_UNUSED","safety_score":40}""";

        var report = OnlineFileIntelligenceService.ParseAiStructuredJson(
            raw, Profile("patch.pak", @"D:\Steam\steamapps\common\BG3", "Steam Game Asset", "Baldur's Gate 3"));

        Assert.False(string.IsNullOrWhiteSpace(report.WhatItDoes), "role must never be blank");
        Assert.False(string.IsNullOrWhiteSpace(report.Condition), "condition must never be blank");
        Assert.False(string.IsNullOrWhiteSpace(report.ImpactIfDeleted), "impact must never be blank");
        Assert.False(string.IsNullOrWhiteSpace(report.Recommendation), "recommendation must never be blank");
        Assert.NotEmpty(report.Evidence);
    }

    [Fact]
    public void ParseAiStructuredJson_SafeVerdictWithCriticalScore_IsDowngraded()
    {
        // Safety-first reconciliation: a model that says "safe to delete" while scoring
        // the file as critical is contradicting itself, so it never auto-approves.
        const string raw = """
            {"what_is_it":"A virtual machine disk.","verdict":"SAFE_TO_DELETE","safety_score":10}
            """;

        var report = OnlineFileIntelligenceService.ParseAiStructuredJson(
            raw, Profile("disk.vhdx", @"D:\VMs\Dev", "Virtual Machine Disk Image", "DevVM"));

        Assert.Equal(OnlineSafetyVerdict.ReviewRequired, report.Verdict);
        Assert.Equal(10, report.SafetyScore);
    }

    // ------------------------------------------------------- built-in engine ---

    [Theory]
    [InlineData("game.pak", @"C:\Program Files (x86)\Steam\steamapps\common\Hades II", "Steam Game Asset", "Hades II")]
    [InlineData("chunk.ucas", @"D:\Epic\EOSDK\Fortnite", "Epic Games Store Title", "Fortnite")]
    [InlineData("model.gguf", @"C:\Users\t\.ollama\models", "AI / Machine Learning Model Weights", "llama")]
    [InlineData("disk.vhdx", @"D:\VMs\Dev", "Virtual Machine Disk Image", "DevVM")]
    [InlineData("avd.img", @"C:\Users\t\.android\avd", "Android Developer SDK / Emulator", "")]
    [InlineData("shader.cache", @"D:\Projects\UE5\DerivedDataCache", "Unreal Engine Shader Cache", "")]
    [InlineData("peak.pek", @"C:\Users\t\AppData\Roaming\Adobe\Common", "Adobe Premiere Cache", "")]
    [InlineData("win11.iso", @"D:\ISOs", "", "")]
    [InlineData("ntdll.dll", @"C:\Windows\System32\", "", "")]
    [InlineData("setup.exe", @"D:\Downloads", "", "")]
    [InlineData("holiday.mp4", @"D:\Videos", "", "")]
    [InlineData("mystery.xyz", @"D:\Random", "", "")]
    public void BuiltInReport_AlwaysAnswersEveryQuestion(string file, string dir, string eco, string item)
    {
        var report = OnlineFileIntelligenceService.GenerateBuiltInKnowledgeReport(
            Profile(file, dir, eco, item));

        Assert.False(string.IsNullOrWhiteSpace(report.WhatIsIt), "what it is");
        Assert.False(string.IsNullOrWhiteSpace(report.WhatItDoes), "what it does");
        Assert.False(string.IsNullOrWhiteSpace(report.Condition), "condition");
        Assert.False(string.IsNullOrWhiteSpace(report.Origin), "origin");
        Assert.False(string.IsNullOrWhiteSpace(report.ImpactIfDeleted), "deletion impact");
        Assert.False(string.IsNullOrWhiteSpace(report.Recommendation), "recommendation");
        Assert.NotEmpty(report.Evidence);
        Assert.InRange(report.Confidence, 1, 100);
        Assert.InRange(report.SafetyScore, 0, 100);
        Assert.True(report.IsVerified, "a built-in answer is the deliberate offline answer, not a fallback");
    }

    [Fact]
    public void BuiltInReport_UsesSignatureAndAgeInTheCondition()
    {
        var report = OnlineFileIntelligenceService.GenerateBuiltInKnowledgeReport(
            Profile("ntdll.dll", @"C:\Windows\System32\", signer: "Microsoft Windows"));

        Assert.Contains("Microsoft Windows", report.Condition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("year", report.Condition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(report.Evidence, e => e.Contains("Authenticode signature: Microsoft Windows", StringComparison.Ordinal));
    }

    [Fact]
    public void BuiltInReport_UnsignedBinary_SaysSo()
    {
        var report = OnlineFileIntelligenceService.GenerateBuiltInKnowledgeReport(
            Profile("setup.exe", @"D:\Downloads"));

        Assert.Contains(report.Evidence, e => e.Contains("not signed", StringComparison.OrdinalIgnoreCase));
    }

    // ----------------------------------------------------------- '.bin' fix ---

    [Theory]
    [InlineData(@"D:\ISOs\install.bin")]
    [InlineData(@"D:\Games\data\level.bin")]
    [InlineData(@"C:\ProgramData\Firmware\board.bin")]
    public void BinFile_OutsideAKnownModelFolder_IsNotClaimedAsAnOsImageOrAiWeights(string path)
    {
        var profile = FileForensicsService.AnalyzeFile(path);

        Assert.NotEqual("Operating System / Disc Image", profile.DetectedEcosystem);
        Assert.NotEqual("AI / Machine Learning Model Weights", profile.DetectedEcosystem);

        var report = OnlineFileIntelligenceService.GenerateBuiltInKnowledgeReport(profile);
        Assert.Equal(OnlineSafetyVerdict.ReviewRequired, report.Verdict);
        Assert.DoesNotContain("Neural network", report.WhatIsIt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Disk image installer", report.WhatIsIt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BinFile_InsideAModelFolder_IsModelWeightsAndStillNotADiscImage()
    {
        // Directory context is evidence: a .bin inside .ollama\models really is a model
        // blob. What it must never be is an OS disc image, which is what the extension
        // alone used to claim.
        var profile = FileForensicsService.AnalyzeFile(@"C:\Users\t\.ollama\models\blob.bin");

        Assert.Equal("AI / Machine Learning Model Weights", profile.DetectedEcosystem);
        Assert.NotEqual("Operating System / Disc Image", profile.DetectedEcosystem);

        var report = OnlineFileIntelligenceService.GenerateBuiltInKnowledgeReport(profile);
        Assert.Equal(OnlineSafetyVerdict.SafeIfUnused, report.Verdict);
        Assert.Contains("model", report.WhatIsIt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClassifyFileCategory_BinIsHonest()
    {
        var (category, icon) = LargeFileHunterService.ClassifyFileCategory(".bin");

        Assert.Equal("Binary / Data", category);
        Assert.Equal("\uE8A5", icon);
    }

    // --------------------------------------------------------- row projection ---

    [Fact]
    public void ApplyOnlineReport_VerifiedReport_MarksTheRowVerifiedAndComplete()
    {
        var item = new LargeFileInfo { FilePath = @"C:\Games\bigfile.pak", FileName = "bigfile.pak" };

        var report = new OnlineSafetyReport
        {
            Verdict = OnlineSafetyVerdict.SafeIfUnused,
            SafetyScore = 40,
            Confidence = 90,
            Origin = "Steam: Hades II",
            WhatIsIt = "Game payload.",
            WhatItDoes = "Read by Steam at launch.",
            Condition = "Intact, modified two years ago.",
            Evidence = new List<string> { "File signature: PAK", "Location: Steam library" },
            ImpactIfDeleted = "Steam re-downloads 40 GB.",
            Recommendation = "Uninstall through Steam.",
            ProviderUsed = "Ollama (llama3.2)",
            IsVerified = true
        };
        report.ApplyBadgeColors();

        item.ApplyOnlineReport(report);

        Assert.True(item.IsAiOnlineVerified);
        Assert.True(item.IsAiAnalyzed);
        Assert.False(item.IsAiAnalyzing);
        Assert.Equal("Game payload.", item.AiExplanation);
        Assert.Equal("Read by Steam at launch.", item.AiWhatItDoes);
        Assert.Equal("Intact, modified two years ago.", item.AiCondition);
        Assert.Equal(2, item.AiEvidence.Count);
        Assert.Equal("90% confidence", item.AiConfidenceLabel);
        Assert.Equal("Steam re-downloads 40 GB.", item.AiImpact);
        Assert.Equal("Uninstall through Steam.", item.AiRecommendation);
    }

    [Fact]
    public void ApplyOnlineReport_Fallback_IsShownButStaysUnverified()
    {
        // The user still sees the explanation, but the row must remain retryable and
        // the cache must never treat the fallback as the provider's answer.
        var item = new LargeFileInfo { FilePath = @"C:\Games\bigfile.pak", FileName = "bigfile.pak" };

        var report = new OnlineSafetyReport
        {
            Verdict = OnlineSafetyVerdict.ReviewRequired,
            SafetyScore = 50,
            Confidence = 55,
            Origin = "Local File",
            WhatIsIt = "Unknown container.",
            WhatItDoes = "Read by its creator.",
            Condition = "Unknown owner.",
            Evidence = new List<string> { "Extension: .pak" },
            ImpactIfDeleted = "Unknown.",
            Recommendation = "Inspect the folder first.",
            ProviderUsed = "Web Knowledge (Fallback - MalformedResponse)",
            IsVerified = false
        };
        report.ApplyBadgeColors();

        item.ApplyOnlineReport(report);

        Assert.False(item.IsAiOnlineVerified, "a fallback is not a verified answer");
        Assert.True(item.IsAiAnalyzed, "the fallback must still be explained on the row");
        Assert.False(item.IsAiAnalyzing);
        Assert.Equal("Unknown container.", item.AiExplanation);
        Assert.Equal("Read by its creator.", item.AiWhatItDoes);
    }

    [Fact]
    public void ApplyOnlineReport_NotifiesEveryNewProperty()
    {
        var item = new LargeFileInfo { FilePath = @"C:\Games\bigfile.pak", FileName = "bigfile.pak" };
        var changed = new List<string>();
        item.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        item.ApplyOnlineReport(new OnlineSafetyReport
        {
            Verdict = OnlineSafetyVerdict.ReviewRequired,
            WhatIsIt = "x",
            WhatItDoes = "y",
            Condition = "z",
            Evidence = new List<string> { "e" },
            Confidence = 70
        });

        Assert.Contains(nameof(LargeFileInfo.AiWhatItDoes), changed);
        Assert.Contains(nameof(LargeFileInfo.AiCondition), changed);
        Assert.Contains(nameof(LargeFileInfo.AiEvidence), changed);
        Assert.Contains(nameof(LargeFileInfo.AiConfidenceLabel), changed);
        Assert.Contains(nameof(LargeFileInfo.IsAiAnalyzed), changed);
    }

    // --------------------------------------------------------- UI parity ------

    [Fact]
    public void BothApps_RenderTheFullReportCapsule()
    {
        string wpf = ReadRepoFile("src", "Deltempo.App.Wpf", "MainWindow.xaml");
        string winui = ReadRepoFile("src", "Deltempo.App.WinUI", "Views", "LargeFilesWorkspaceView.xaml");

        // WPF
        Assert.Contains("IsAiAnalyzed", wpf, StringComparison.Ordinal);
        Assert.Contains("AiWhatItDoes", wpf, StringComparison.Ordinal);
        Assert.Contains("AiCondition", wpf, StringComparison.Ordinal);
        Assert.Contains("AiEvidence", wpf, StringComparison.Ordinal);
        Assert.Contains("AiConfidenceLabel", wpf, StringComparison.Ordinal);
        Assert.Contains("AiImpact", wpf, StringComparison.Ordinal);
        Assert.Contains("If deleted:", wpf, StringComparison.Ordinal);

        // WinUI binds the same LargeFileInfo properties under their non-Ai aliases.
        Assert.Contains("IsAiAnalyzed", winui, StringComparison.Ordinal);
        Assert.Contains("WhatItDoes", winui, StringComparison.Ordinal);
        Assert.Contains("AiCondition", winui, StringComparison.Ordinal);
        Assert.Contains("AiEvidence", winui, StringComparison.Ordinal);
        Assert.Contains("AiConfidenceLabel", winui, StringComparison.Ordinal);
        Assert.Contains("If deleted:", winui, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 12; i++)
        {
            string candidate = Path.Combine(new[] { dir }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }

        throw new InvalidOperationException($"could not locate {Path.Combine(parts)} under the repository root");
    }
}
