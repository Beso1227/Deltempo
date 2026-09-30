using System;
using System.IO;
using System.Linq;
using WinTempCleaner.Services;
using Xunit;
using Xunit.Abstractions;

namespace Deltempo.Tests;

public class OrphanDiagnosticDump
{
    private readonly ITestOutputHelper _out;
    public OrphanDiagnosticDump(ITestOutputHelper output) => _out = output;

    [Fact]
    [Trait("Category", "Diagnostic")]
    public void Dump_Actual_Orphan_Proposals()
    {
        var orphans = OrphanedAppService.ScanVerifiedOrphanedFolders();
        _out.WriteLine($"PROPOSED: {orphans.Count}");
        foreach (var o in orphans.OrderByDescending(o => o.SizeBytes))
        {
            _out.WriteLine($"{o.SizeBytes,12:N0}  {o.FolderPath}  [{o.Description}]");
        }

        // Which AppData\Local folders are NOT isActive under the REAL keyword set?
        // Those are only kept safe by the age/structural shields below the predicate.
        var kw = OrphanedAppService.GetComprehensiveActiveAppKeywords();
        _out.WriteLine($"KEYWORDS: {kw.Count}");

        string[] probe =
        {
            "Google", "PDFgear", "DaysGone", "BendGame", "OpenAI", "copilot", "hermes",
            "claude-cli-nodejs", "lean-ctx", "npm-cache", "uv", "pip", "ms-playwright",
            "Docker", "ASUS", "Temp", "CrashDumps", "node-gyp", "fontconfig", "kotlin",
            "Dart", "SquirrelTemp", "system_backup_gui", "Backup", "Devolutions",
            "chrome-devtools-mcp", "google-vscode-extension", "vscode-sqltools", "gk",
            "GitKrakenCLI", "cloud-code", "cua-driver", "csdevkit", "pypa", "PSResourceGet"
        };

        foreach (var name in probe)
        {
            bool isActive = kw.Any(a =>
                a.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                (a.Length >= 4 && name.Contains(a, StringComparison.OrdinalIgnoreCase)));

            string local = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), name);

            string age = "?";
            long bytes = -1;
            if (Directory.Exists(local))
            {
                try
                {
                    age = (DateTime.Now - new DirectoryInfo(local).LastWriteTime).TotalDays.ToString("F1") + "d";
                    bytes = Directory.EnumerateFiles(local, "*", new EnumerationOptions
                    {
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = true,
                        AttributesToSkip = FileAttributes.ReparsePoint
                    }).Take(5000).Sum(f => { try { return f.Length; } catch { return 0L; } });
                }
                catch { age = "ERR"; }
            }

            _out.WriteLine($"  {(isActive ? "ACTIVE " : "ORPHAN?")} {name,-26} age={age,-8} bytes={bytes,12:N0}");
        }

        Assert.True(true);
    }
}

