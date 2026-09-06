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
        string category,
        bool safeMode24Hours = true,
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

            // Guard against starting inside a reparse point or link
            if (PathSecurity.IsReparsePointOrLink(canonicalDir)) continue;

            try
            {
                var dirInfo = new DirectoryInfo(canonicalDir);

                foreach (var file in dirInfo.EnumerateFiles("*", enumOptions))
                {
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        // 1. Skip if file itself is a reparse point
                        if (PathSecurity.IsReparsePointOrLink(file)) continue;

                        // 2. Multi-signal safety analysis
                        var safetyResult = FileSafetyEngine.Analyze(
                            file.FullName,
                            category,
                            allowedRoot: canonicalDir,
                            apply24HourThreshold: safeMode24Hours);

                        var intendedAction = DetermineAction(safetyResult.Tier, sendToRecycleBin);

                        plan.Actions.Add(new PlannedFileAction
                        {
                            FilePath = file.FullName,
                            FileName = file.Name,
                            SizeBytes = file.Length,
                            Category = category,
                            SafetyTier = safetyResult.Tier,
                            Reason = safetyResult.Explanation,
                            Action = intendedAction,
                            LastModified = file.LastWriteTimeUtc
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

    private static IntendedCleanupAction DetermineAction(SafetyRiskTier tier, bool sendToRecycleBin) => tier switch
    {
        SafetyRiskTier.Safe => sendToRecycleBin
            ? IntendedCleanupAction.MoveToRecycleBin
            : IntendedCleanupAction.DeletePermanently,

        SafetyRiskTier.LowRisk => sendToRecycleBin
            ? IntendedCleanupAction.MoveToRecycleBin
            : IntendedCleanupAction.DeletePermanently,

        SafetyRiskTier.ReviewRequired => IntendedCleanupAction.SkipReviewRequired,

        _ => IntendedCleanupAction.SkipProtected // Protected or Unknown -> Always Skip
    };
}
