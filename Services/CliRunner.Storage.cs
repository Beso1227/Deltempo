using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using WinTempCleaner.Core.Cleaning;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

// CLI command handlers: large file discovery, inspection, recycling and deletion.
public static partial class CliRunner
{

    // ─── LARGE FILES (BIG FILES) COMMAND ───────────────────────────────

    private static async Task<int> HandleLargeFilesAsync(string[] args)
    {
        bool isJson = HasFlag(args, "--json", "-j");
        string subCmd = args.Length > 1 && !args[1].StartsWith("-") ? args[1].ToLowerInvariant() : "scan";

        // Subcommand: inspect <file>
        if (subCmd == "inspect" || subCmd == "info")
        {
            if (args.Length < 3)
            {
                Console.WriteLine("  ⚠️ Usage: deltempo large inspect <file_path>");
                return 1;
            }
            string targetFile = args[2].Trim('"', '\'');
            return await HandleInspectLargeFileAsync(targetFile, isJson);
        }

        // Subcommand: delete / rm <file>
        if (subCmd == "delete" || subCmd == "rm")
        {
            if (args.Length < 3)
            {
                Console.WriteLine("  ⚠️ Usage: deltempo large delete <file_path>");
                return 1;
            }
            string targetFile = args[2].Trim('"', '\'');
            return HandleDeleteLargeFile(targetFile, HasFlag(args, "--yes", "-y"));
        }

        // Subcommand: clean / purge
        bool isCleanMode = subCmd == "clean" || subCmd == "purge" || HasFlag(args, "--clean");

        // Scope / path parsing
        string scope = "ALL";
        if (args.Length > 1 && !args[1].StartsWith("-") && subCmd != "scan" && subCmd != "find" && subCmd != "clean" && subCmd != "purge")
        {
            scope = args[1];
        }
        else if (args.Length > 2 && !args[2].StartsWith("-"))
        {
            scope = args[2];
        }

        string? scopeOpt = GetOptionValue(args, "--scope", "--path", "--drive");
        if (!string.IsNullOrEmpty(scopeOpt))
        {
            scope = scopeOpt;
        }

        // Options
        long minBytes = ParseBytes(GetOptionValue(args, "--min", "-m"), 50L * 1024 * 1024);
        int topLimit = int.TryParse(GetOptionValue(args, "--top", "-n"), out int n) ? n : 35;
        string? typeFilter = GetOptionValue(args, "--type");
        string? extFilter = GetOptionValue(args, "--ext");
        bool safeOnly = HasFlag(args, "--safe", "--safe-only", "--ai-safe");
        bool protectedOnly = HasFlag(args, "--protected");
        bool dryRun = HasFlag(args, "--dry-run", "-d");
        bool yesPrompt = HasFlag(args, "--yes", "-y");
        bool cleanAll = HasFlag(args, "--all");

        if (!isJson)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  [Deltempo] Large File Hunter (>{TargetFolderInfo.FormatBytes(minBytes)}) on '{scope}'...\n");
            Console.ResetColor();
        }

        var scanResult = await LargeFileHunterService.ScanLargeFilesAsync(minBytes, scope);
        var files = scanResult.Files;

        // Apply filters
        if (!string.IsNullOrWhiteSpace(extFilter))
        {
            if (!extFilter.StartsWith(".")) extFilter = "." + extFilter;
            files = files.Where(f => f.FileName.EndsWith(extFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(typeFilter))
        {
            files = files.Where(f => f.Category.Contains(typeFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (safeOnly)
        {
            files = files.Where(f => f.IsAiSafe).ToList();
        }
        else if (protectedOnly)
        {
            files = files.Where(f => !f.IsAiSafe).ToList();
        }

        // Sorting
        string sort = GetOptionValue(args, "--sort") ?? "size";
        files = sort.ToLowerInvariant() switch
        {
            "date" or "time" => files.OrderByDescending(f => f.LastModified).ToList(),
            "name" => files.OrderBy(f => f.FileName).ToList(),
            _ => files.OrderByDescending(f => f.SizeBytes).ToList()
        };

        if (isJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                files = files,
                totalDiscovered = scanResult.TotalDiscovered,
                displayLimit = scanResult.DisplayLimit,
                wasTruncated = scanResult.WasTruncated,
                totalBytesScanned = scanResult.TotalBytesScanned,
                directoriesScanned = scanResult.DirectoriesScanned,
                inaccessibleDirectories = scanResult.InaccessibleDirectories.Count,
                scanCompleted = scanResult.ScanCompleted
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        if (files.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  ✓ No large files found matching the given criteria.");
            Console.ResetColor();
            return 0;
        }

        Console.WriteLine("  ┌──────────────┬──────────────────────────────┬─────────────────────┬──────────────────────────────────────┐");
        Console.WriteLine($"  │ {"SIZE",-12} │ {"AI SAFETY VERDICT",-28} │ {"CATEGORY",-19} │ {"FILE NAME",-36} │");
        Console.WriteLine("  ├──────────────┼──────────────────────────────┼─────────────────────┼──────────────────────────────────────┤");

        foreach (var f in files.Take(topLimit))
        {
            string name = f.FileName.Length > 36 ? f.FileName.Substring(0, 33) + "..." : f.FileName;
            string verdict = f.AiVerdict.Length > 28 ? f.AiVerdict.Substring(0, 25) + "..." : f.AiVerdict;

            Console.Write($"  │ {f.FormattedSize,-12} │ ");
            if (f.IsAiSafe)
                Console.ForegroundColor = ConsoleColor.Green;
            else
                Console.ForegroundColor = ConsoleColor.Red;

            Console.Write($"{verdict,-28}");
            Console.ResetColor();
            Console.WriteLine($" │ {f.Category,-19} │ {name,-36} │");
        }
        Console.WriteLine("  └──────────────┴──────────────────────────────┴─────────────────────┴──────────────────────────────────────┘");

        long totalBytes = files.Sum(x => x.SizeBytes);
        var safeFiles = files.Where(x => x.IsAiSafe).ToList();
        long safeBytes = safeFiles.Sum(x => x.SizeBytes);

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        string truncationNote = scanResult.WasTruncated ? $" (showing top {files.Count} of {scanResult.TotalDiscovered})" : "";
        Console.WriteLine($"  Discovered {scanResult.TotalDiscovered} large files ({TargetFolderInfo.FormatBytes(totalBytes)}){truncationNote}.");
        Console.WriteLine($"  Scanned {scanResult.DirectoriesScanned:N0} directories ({TargetFolderInfo.FormatBytes(scanResult.TotalBytesScanned)} total).");
        if (scanResult.InaccessibleDirectories.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  Warning: {scanResult.InaccessibleDirectories.Count} directories were inaccessible.");
        }
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  Safety-classified {safeFiles.Count} files as safe to clean ({TargetFolderInfo.FormatBytes(safeBytes)}): Stale installers, dumps, temp.");
        Console.ResetColor();

        // If Clean / Purge action requested
        if (isCleanMode)
        {
            var toRecycle = safeOnly ? safeFiles : (cleanAll ? files : safeFiles);
            if (toRecycle.Count == 0)
            {
                Console.WriteLine("  • No matching files selected for recycling.");
                return 0;
            }

            if (dryRun)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n  [DRY RUN] Would recycle {toRecycle.Count} files ({TargetFolderInfo.FormatBytes(toRecycle.Sum(x => x.SizeBytes))}) to Windows Recycle Bin.");
                foreach (var item in toRecycle.Take(15))
                {
                    Console.WriteLine($"    • Would move: {item.FileName} ({item.FormattedSize})");
                }
                Console.ResetColor();
                return 0;
            }

            if (!yesPrompt)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"\n  ⚠️ Move {toRecycle.Count} files ({TargetFolderInfo.FormatBytes(toRecycle.Sum(x => x.SizeBytes))}) to Windows Recycle Bin? (y/N): ");
                Console.ResetColor();
                var key = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(key) || (!key.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) && !key.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine("  • Recycling cancelled by user.");
                    return 0;
                }
            }

            Console.WriteLine($"\n  • Recycling {toRecycle.Count} files ({TargetFolderInfo.FormatBytes(toRecycle.Sum(x => x.SizeBytes))}) to Windows Recycle Bin...");
            var (succ, fail, freed) = LargeFileHunterService.BatchMoveToRecycleBin(toRecycle);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✓ Successfully recycled {succ} files ({TargetFolderInfo.FormatBytes(freed)} freed) with undo capability.");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"  💡 Tip: Run 'deltempo large clean --safe-only' to safely recycle {safeFiles.Count} safe files ({TargetFolderInfo.FormatBytes(safeBytes)}).");
            Console.ResetColor();
        }

        return 0;
    }

    private static async Task<int> HandleInspectLargeFileAsync(string filePath, bool isJson)
    {
        if (!File.Exists(filePath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ❌ File not found: '{filePath}'");
            Console.ResetColor();
            return 1;
        }

        var fi = new FileInfo(filePath);
        var (category, _) = LargeFileHunterService.ClassifyFileCategory(fi.Extension);
        var safety = FileSafetyEngine.Analyze(fi.FullName, fileName: fi.Name, category: category, sizeBytes: fi.Length, lastModified: fi.LastWriteTime);

        OnlineSafetyReport? aiReport = null;
        try
        {
            aiReport = await OnlineFileIntelligenceService.AnalyzeFileAsync(fi.FullName);
        }
        catch { }

        if (isJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                path = fi.FullName,
                name = fi.Name,
                sizeBytes = fi.Length,
                formattedSize = TargetFolderInfo.FormatBytes(fi.Length),
                created = fi.CreationTime,
                lastModified = fi.LastWriteTime,
                category = category,
                deterministicSafety = new
                {
                    safety.Tier,
                    safety.SafetyScore,
                    safety.Verdict,
                    safety.VerdictShort,
                    safety.Explanation,
                    safety.MatchedRule,
                    safety.Origin,
                    safety.Impact
                },
                onlineAiSafety = aiReport == null ? null : new
                {
                    aiReport.VerdictDisplay,
                    aiReport.SafetyScore,
                    aiReport.Origin,
                    aiReport.WhatIsIt,
                    aiReport.ImpactIfDeleted,
                    aiReport.Recommendation,
                    aiReport.ProviderUsed
                }
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  🔍 [File Safety Inspection] {fi.Name}\n");
        Console.ResetColor();

        Console.WriteLine($"  • Full Path:       {fi.FullName}");
        Console.WriteLine($"  • File Size:       {TargetFolderInfo.FormatBytes(fi.Length)} ({fi.Length:N0} bytes)");
        Console.WriteLine($"  • Category:        {category}");
        Console.WriteLine($"  • Last Modified:   {fi.LastWriteTime:yyyy-MM-dd HH:mm:ss}");

        if (aiReport != null)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"\n  🤖 [AI & Online Intelligence ({aiReport.ProviderUsed})]");
            Console.ResetColor();
            Console.WriteLine($"  • What is it:      {aiReport.WhatIsIt}");
            Console.WriteLine($"  • Parent Origin:   {aiReport.Origin}");
            Console.Write("  • AI Verdict:      ");
            Console.ForegroundColor = aiReport.Verdict == OnlineSafetyVerdict.SafeToDelete ? ConsoleColor.Green : (aiReport.Verdict == OnlineSafetyVerdict.CriticalDoNotDelete ? ConsoleColor.Red : ConsoleColor.Yellow);
            Console.WriteLine($"{aiReport.VerdictDisplay} (Score: {aiReport.SafetyScore}/100)");
            Console.ResetColor();
            Console.WriteLine($"  • Deletion Impact: {aiReport.ImpactIfDeleted}");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  • Recommendation:  {aiReport.Recommendation}");
            Console.ResetColor();
        }

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"\n  🛡️ [Deterministic Rule: {safety.MatchedRule}]");
        Console.WriteLine($"  • Rule Verdict:    {safety.Verdict} (Score: {safety.SafetyScore}/100)");
        Console.WriteLine($"  • System Impact:   {safety.Impact}");
        Console.WriteLine($"  • Rationale:       {safety.Explanation}");
        Console.ResetColor();

        return 0;
    }

    private static int HandleDeleteLargeFile(string filePath, bool yesPrompt)
    {
        if (!File.Exists(filePath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ❌ File not found: '{filePath}'");
            Console.ResetColor();
            return 1;
        }

        var fi = new FileInfo(filePath);
        if (!yesPrompt)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"  ⚠️ Move '{fi.Name}' ({TargetFolderInfo.FormatBytes(fi.Length)}) to Recycle Bin? (y/N): ");
            Console.ResetColor();
            var key = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(key) || (!key.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) && !key.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine("  • Deletion cancelled.");
                return 0;
            }
        }

        bool success = LargeFileHunterService.MoveToRecycleBin(fi.FullName);
        if (success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✓ Successfully moved '{fi.Name}' to Recycle Bin with undo capability.");
            Console.ResetColor();
            return 0;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ❌ Failed to move '{fi.Name}' to Recycle Bin.");
            Console.ResetColor();
            return 1;
        }
    }
}
