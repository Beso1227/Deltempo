using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WinTempCleaner.Core.Services;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public static partial class CliRunner
{
    private static async Task<int> HandleDuplicatesAsync(string[] args)
    {
        bool isJson = HasFlag(args, "--json", "-j");
        bool isDelete = HasFlag(args, "--delete", "-d");
        bool isDryRun = HasFlag(args, "--dry-run");
        bool autoConfirm = HasFlag(args, "--yes", "-y");

        string targetPath = Directory.GetCurrentDirectory();
        if (args.Length > 1 && !args[1].StartsWith("-"))
        {
            targetPath = Path.GetFullPath(args[1].Trim('"', '\''));
        }

        string? minSizeStr = GetOptionValue(args, "--min-size", "--min", "-m");
        long minSizeBytes = 1024 * 1024; // 1 MB default
        if (!string.IsNullOrEmpty(minSizeStr))
        {
            minSizeBytes = CleanerService.ParseSizeStringToBytes(minSizeStr);
        }

        string strategyStr = GetOptionValue(args, "--strategy", "-s") ?? "oldest";
        var strategy = strategyStr.ToLowerInvariant() switch
        {
            "newest" => DuplicateSelectionStrategy.KeepNewest,
            "shortest" => DuplicateSelectionStrategy.KeepShortestPath,
            "longest" => DuplicateSelectionStrategy.KeepLongestPath,
            _ => DuplicateSelectionStrategy.KeepOldest
        };

        if (!Directory.Exists(targetPath))
        {
            if (isJson)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { error = $"Directory not found: {targetPath}" }));
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ❌ Target directory not found: '{targetPath}'");
                Console.ResetColor();
            }
            return 1;
        }

        if (!isJson)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  🔍 Scanning for duplicate files in '{targetPath}' (min size: {TargetFolderInfo.FormatBytes(minSizeBytes)})...");
            Console.ResetColor();
        }

        var result = await DuplicateFileService.FindDuplicatesAsync(new[] { targetPath }, minSizeBytes);
        DuplicateFileService.ApplySelectionStrategy(result.Groups, strategy);

        if (isJson)
        {
            var jsonPayload = new
            {
                targetPath,
                filesScanned = result.TotalScannedFiles,
                duplicateGroupsCount = result.Groups.Count,
                duplicateFilesCount = result.TotalDuplicateFilesCount,
                totalWastedBytes = result.TotalWastedBytes,
                formattedWasted = TargetFolderInfo.FormatBytes(result.TotalWastedBytes),
                elapsedMs = result.Elapsed.TotalMilliseconds,
                groups = result.Groups.Select(g => new
                {
                    sizeBytes = g.SizeBytes,
                    formattedSize = TargetFolderInfo.FormatBytes(g.SizeBytes),
                    sha256 = g.Sha256Hash,
                    wastedBytes = g.TotalWastedBytes,
                    files = g.Files.Select(f => new
                    {
                        path = f.FilePath,
                        name = f.FileName,
                        lastModified = f.LastModified,
                        isSelectedForDeletion = f.IsSelectedForDeletion,
                        isProtected = f.IsProtected
                    })
                })
            };

            Console.WriteLine(JsonSerializer.Serialize(jsonPayload, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        Console.WriteLine($"  ✓ Scanned {result.TotalScannedFiles:N0} files in {result.Elapsed.TotalSeconds:F2}s.");

        if (result.Groups.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  ✓ No duplicate files found. Disk is clean!");
            Console.ResetColor();
            return 0;
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  Found {result.Groups.Count:N0} duplicate groups ({result.TotalDuplicateFilesCount:N0} redundant files, {TargetFolderInfo.FormatBytes(result.TotalWastedBytes)} wasted space):\n");
        Console.ResetColor();

        int groupIndex = 1;
        foreach (var group in result.Groups.Take(25))
        {
            Console.WriteLine($"  [{groupIndex++}] Size: {TargetFolderInfo.FormatBytes(group.SizeBytes)} | Hash: {group.Sha256Hash[..12]}... (Wasted: {TargetFolderInfo.FormatBytes(group.TotalWastedBytes)})");
            foreach (var file in group.Files)
            {
                string status = file.IsSelectedForDeletion ? "❌ [DELETE]" : "✅ [KEEP]";
                Console.WriteLine($"      {status} {file.FilePath} ({file.LastModified:yyyy-MM-dd HH:mm})");
            }
            Console.WriteLine();
        }

        if (result.Groups.Count > 25)
        {
            Console.WriteLine($"  ... and {result.Groups.Count - 25} more duplicate groups.\n");
        }

        if (!isDelete)
        {
            Console.WriteLine("  💡 Run with '--delete' to move redundant copies to the Windows Recycle Bin.");
            Console.WriteLine($"  💡 Strategy used: {strategy} (use '--strategy newest' or '--strategy shortest' to customize).");
            return 0;
        }

        var filesToDelete = result.Groups
            .SelectMany(g => g.Files)
            .Where(f => f.IsSelectedForDeletion && !f.IsProtected)
            .ToList();

        if (filesToDelete.Count == 0)
        {
            Console.WriteLine("  No non-protected files selected for deletion.");
            return 0;
        }

        if (isDryRun)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  [DRY RUN] Would recycle {filesToDelete.Count:N0} redundant duplicate files ({TargetFolderInfo.FormatBytes(filesToDelete.Sum(f => f.SizeBytes))}).");
            Console.ResetColor();
            return 0;
        }

        if (!autoConfirm)
        {
            Console.Write($"  ⚠️ Move {filesToDelete.Count:N0} duplicate files ({TargetFolderInfo.FormatBytes(filesToDelete.Sum(f => f.SizeBytes))}) to Windows Recycle Bin? (y/N): ");
            var key = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(key) || !key.Trim().Equals("y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("  Operation cancelled by user.");
                return 0;
            }
        }

        int recycled = 0;
        long bytesFreed = 0;

        foreach (var file in filesToDelete)
        {
            try
            {
                if (LargeFileHunterService.MoveToRecycleBin(file.FilePath))
                {
                    recycled++;
                    bytesFreed += file.SizeBytes;
                }
            }
            catch { }
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✓ Successfully recycled {recycled:N0} duplicate files ({TargetFolderInfo.FormatBytes(bytesFreed)} freed).");
        Console.ResetColor();

        return 0;
    }
}
