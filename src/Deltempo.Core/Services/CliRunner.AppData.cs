using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace WinTempCleaner.Services;

// CLI command handlers for the read-only AppData footprint report.
//
// This command has NO delete path by construction: it renders a report and returns. There is
// deliberately no --clean, --yes, or delete subcommand here, because a report that can be
// turned into a deletion from the command line stops being a report.
public static partial class CliRunner
{
    private static async Task<int> HandleAppDataFootprintAsync(string[] args)
    {
        bool isJson = HasFlag(args, "--json", "-j");
        int topN = 20;
        if (HasFlag(args, "--top"))
        {
            int idx = Array.FindIndex(args, a => a.Equals("--top", StringComparison.OrdinalIgnoreCase));
            if (idx >= 0 && idx + 1 < args.Length &&
                int.TryParse(args[idx + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) &&
                parsed > 0)
            {
                topN = Math.Min(parsed, 500);
            }
        }

        var entries = AppDataFootprintService.Analyze();

        if (isJson)
        {
            var payload = new
            {
                reportOnly = true,
                deletesNothing = true,
                totalBytes = entries.Sum(e => e.SizeBytes),
                folderCount = entries.Count,
                protectedStateBytes = entries.Where(e => e.IsProtectedState).Sum(e => e.SizeBytes),
                folders = entries.Take(topN).Select(e => new
                {
                    name = e.Name,
                    root = e.RootLabel,
                    path = e.Path,
                    sizeBytes = e.SizeBytes,
                    lastWriteUtc = e.LastWriteTimeUtc,
                    isProtectedState = e.IsProtectedState,
                    coveredByExistingScope = e.CoveredByExistingScope
                })
            };

            Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
            return 0;
        }

        Console.WriteLine();
        Console.WriteLine("  AppData footprint report - READ ONLY, nothing is deleted");
        Console.WriteLine("  -------------------------------------------------------");

        if (entries.Count == 0)
        {
            Console.WriteLine("  No AppData trees could be read.");
            return 1;
        }

        long total = entries.Sum(e => e.SizeBytes);
        foreach (var entry in entries.Take(topN))
        {
            string flags = entry.IsProtectedState ? " [protected state - never cleaned]" :
                           entry.CoveredByExistingScope ? " [covered by an existing scope]" : "";
            Console.WriteLine(
                $"  {FormatBytes(entry.SizeBytes),10}  {entry.RootLabel,-9} {entry.Name}{flags}");
        }

        if (entries.Count > topN)
        {
            Console.WriteLine($"  ... and {entries.Count - topN} more folders (use --top N or --json)");
        }

        Console.WriteLine();
        Console.WriteLine($"  Total measured: {FormatBytes(total)} across {entries.Count} folders.");
        foreach (var group in entries.GroupBy(e => e.RootLabel).OrderByDescending(g => g.Sum(e => e.SizeBytes)))
        {
            Console.WriteLine($"    {group.Key,-9} {FormatBytes(group.Sum(e => e.SizeBytes)),10}");
        }
        Console.WriteLine("  Folders marked [covered by an existing scope] can be cleaned by the");
        Console.WriteLine("  matching Deltempo scope. Nothing is selected or deleted by this command.");
        Console.WriteLine();

        await Task.CompletedTask;
        return 0;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes} B" : $"{value:0.##} {units[unit]}";
    }
}
