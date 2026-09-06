using WinTempCleaner.Core.Safety;

namespace WinTempCleaner.Services;

public enum AiSafetyTier
{
    SafeToClean,
    HighRiskKeep
}

/// <summary>
/// Backward-compatible adapter for AiAnalysisResult.
/// Maps underlying deterministic verdicts from FileSafetyEngine to existing UI bindings.
/// </summary>
public class AiAnalysisResult
{
    public int SafetyScore { get; set; }
    public AiSafetyTier Tier { get; set; }
    public string Verdict { get; set; } = string.Empty;
    public string VerdictShort { get; set; } = "SAFE";
    public string BadgeColor { get; set; } = "#10B981";
    public string BadgeBackground { get; set; } = "#122A1E";
    public string BadgeBorder { get; set; } = "#10B981";
    public string Origin { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public bool IsSafeToAutoClean { get; set; }
}

/// <summary>
/// Backward-compatible facade delegating directly to the deterministic FileSafetyEngine.
/// Replaces heuristic "AI" claims with verifiable multi-signal safety analysis.
/// </summary>
public static class AiFileSafetyService
{
    public static AiAnalysisResult AnalyzeFile(string filePath, string fileName, string category, long sizeBytes, DateTime lastModified)
    {
        var result = FileSafetyEngine.Analyze(
            filePath,
            fileName: fileName,
            category: category,
            sizeBytes: sizeBytes,
            lastModified: lastModified);

        bool isSafe = result.Tier is SafetyRiskTier.Safe or SafetyRiskTier.LowRisk;

        return new AiAnalysisResult
        {
            SafetyScore = result.SafetyScore,
            Tier = isSafe ? AiSafetyTier.SafeToClean : AiSafetyTier.HighRiskKeep,
            Verdict = result.Verdict,
            VerdictShort = result.VerdictShort,
            BadgeColor = result.BadgeColor,
            BadgeBackground = result.BadgeBackground,
            BadgeBorder = result.BadgeBorder,
            Origin = result.Origin,
            Impact = result.Impact,
            Explanation = result.Explanation,
            IsSafeToAutoClean = isSafe
        };
    }
}
