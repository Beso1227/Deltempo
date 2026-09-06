namespace WinTempCleaner.Core.Safety;

/// <summary>
/// Detailed evaluation record produced by FileSafetyEngine.
/// Contains the deterministic verdict, matched protection rule, rationale, and badge formatting.
/// </summary>
public class SafetyAnalysisResult
{
    public SafetyRiskTier Tier { get; set; } = SafetyRiskTier.Unknown;

    /// <summary>
    /// Safety score from 0 (Strictly Protected) to 100 (Verified Disposable Cache).
    /// </summary>
    public int SafetyScore { get; set; }

    /// <summary>
    /// Human-readable verdict description.
    /// </summary>
    public string Verdict { get; set; } = string.Empty;

    /// <summary>
    /// Short badge verdict text: "PROTECTED", "VERIFIED CACHE", "LOW RISK", "REVIEW REQUIRED", "UNKNOWN".
    /// </summary>
    public string VerdictShort { get; set; } = "UNKNOWN";

    /// <summary>
    /// Hex color for UI badge foreground.
    /// </summary>
    public string BadgeColor { get; set; } = "#9CA3AF";

    /// <summary>
    /// Hex color for UI badge background.
    /// </summary>
    public string BadgeBackground { get; set; } = "#1F2937";

    /// <summary>
    /// Hex color for UI badge border.
    /// </summary>
    public string BadgeBorder { get; set; } = "#4B5563";

    /// <summary>
    /// Categorical origin of the file (e.g. "Windows System Core", "Browser Authentication", "NVIDIA Shader Cache").
    /// </summary>
    public string Origin { get; set; } = string.Empty;

    /// <summary>
    /// Concrete impact statement explaining what happens if this file is deleted.
    /// </summary>
    public string Impact { get; set; } = string.Empty;

    /// <summary>
    /// Technical explanation detailing why this safety tier was assigned.
    /// </summary>
    public string Explanation { get; set; } = string.Empty;

    /// <summary>
    /// Name of the specific protection rule or pattern that was matched.
    /// </summary>
    public string MatchedRule { get; set; } = string.Empty;

    /// <summary>
    /// Whether this file is authorized for automated routine cleanup without individual review.
    /// True ONLY for Tier Safe and Tier LowRisk.
    /// </summary>
    public bool IsSafeToClean => Tier is SafetyRiskTier.Safe or SafetyRiskTier.LowRisk;
}
