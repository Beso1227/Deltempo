using System.IO;
using WinTempCleaner.Core.Safety;

namespace WinTempCleaner.Core.Cleaning;

/// <summary>
/// Deterministic planning engine that scans filesystem locations and produces a CleanupPlan.
/// Both Dry-Run simulation and Live execution share this exact planner to guarantee zero discrepancy.
/// </summary>
public static class CleanupPlanner
{
    public static CleanupPlan CreatePlan(
        string scopeId,
        string scopeName,
        IEnumerable<string> directories,
        string category = "General",
        bool apply24HourShield = true,
        bool sendToRecycleBin = false,
        CancellationToken ct = default)
    {
        var plan = new CleanupPlan
        {
            ScopeId = scopeId,
            ScopeName = scopeName
        };

        var enumOptions = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (var dir in directories)
        {
            if (ct.IsCancellationRequested) break;
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) continue;

            string canonicalDir = PathSecurity.NormalizeCanonicalPath(dir);
            if (string.IsNullOrEmpty(canonicalDir)) continue;

            if (PathSecurity.IsReparsePointOrLink(canonicalDir)) continue;

            try
            {
                var dirInfo = new DirectoryInfo(canonicalDir);

                foreach (var file in dirInfo.EnumerateFiles("*", enumOptions))
                {
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        if (PathSecurity.IsReparsePointOrLink(file)) continue;

                        var safetyResult = FileSafetyEngine.Analyze(
                            filePath: file.FullName,
                            fileName: file.Name,
                            category: category,
                            sizeBytes: file.Length,
                            lastModified: file.LastWriteTimeUtc,
                            allowedRoot: canonicalDir,
                            apply24HourThreshold: apply24HourShield,
                            allowedRoots: directories);

                        var intendedAction = DetermineAction(safetyResult.Tier, sendToRecycleBin, apply24HourShield);

                        plan.Actions.Add(new PlannedFileAction
                        {
                            FilePath = file.FullName,
                            FileName = file.Name,
                            SizeBytes = file.Length,
                            Category = category,
                            SafetyTier = safetyResult.Tier,
                            Reason = safetyResult.Explanation,
                            MatchedRule = safetyResult.MatchedRule,
                            Action = intendedAction,
                            LastModified = file.LastWriteTimeUtc,
                            PlannedSizeBytes = file.Length
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"[Deltempo Planner] File skipped: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo Planner] Dir skipped: {ex.Message}");
            }
        }

        return plan;
    }

    private static IntendedCleanupAction DetermineAction(SafetyRiskTier tier, bool sendToRecycleBin, bool apply24HourShield) => tier switch
    {
        SafetyRiskTier.Safe => sendToRecycleBin
            ? IntendedCleanupAction.MoveToRecycleBin
            : IntendedCleanupAction.DeletePermanently,

        SafetyRiskTier.LowRisk => sendToRecycleBin
            ? IntendedCleanupAction.MoveToRecycleBin
            : IntendedCleanupAction.DeletePermanently,

        SafetyRiskTier.ReviewRequired => IntendedCleanupAction.SkipReviewRequired,

        // Non-negotiable invariant: PROTECTED is strictly never deletable under any circumstance
        SafetyRiskTier.Protected => IntendedCleanupAction.SkipProtected,

        // When 24-hour shield is disabled (e.g. pure cache scope or explicit --unsafe flag),
        // unclassified non-protected files in the target scope are eligible for cleaning.
        // When the shield IS active (default safe mode), UNKNOWN files are strictly protected.
        SafetyRiskTier.Unknown when !apply24HourShield => sendToRecycleBin
            ? IntendedCleanupAction.MoveToRecycleBin
            : IntendedCleanupAction.DeletePermanently,

        _ => IntendedCleanupAction.SkipProtected
    };
}
