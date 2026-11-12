using System.Diagnostics;
using System.IO;
using WinTempCleaner.Models;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Pins the permanent-removal path for orphaned-app residual folders.
///
/// This path previously called LargeFileHunterService.MoveToRecycleBin UNCONDITIONALLY,
/// so residuals landed in the Recycle Bin even with the Send-to-Recycle-Bin setting
/// OFF - the "I disabled it but files still go to the bin" report. CleanFolderAsync now
/// reads the setting and picks recycle vs. permanent removal; these tests pin the
/// permanent branch (the one whose safety behaviour must be airtight).
/// </summary>
public class OrphanResidualDeletionTests
{
    private static readonly Action<string, LogLevel> SilentLog = static (_, _) => { };

    private static string NewAppDataSandbox()
    {
        // LOCALAPPDATA guarantees the "\appdata\" scope the protected-state denylist is
        // evaluated in; %TEMP% can be redirected away from AppData.
        string sandbox = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Deltempo_OrphanResidualTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);
        return sandbox;
    }

    private static void CreateJunction(string junctionPath, string targetDir)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c mklink /J \"{junctionPath}\" \"{targetDir}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi)!;
        proc.StandardOutput.ReadToEnd();
        proc.StandardError.ReadToEnd();
        proc.WaitForExit(15000);

        Assert.True(Directory.Exists(junctionPath),
            $"mklink /J failed to create a junction at '{junctionPath}' in this environment.");
    }

    [Fact]
    public void RemovesAVettedFolderAndItsContentsPermanently()
    {
        string sandbox = NewAppDataSandbox();
        try
        {
            string residual = Path.Combine(sandbox, "residual");
            string nested = Path.Combine(residual, "AC", "TokenBrokerCache");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(nested, "cache.dat"), "data");
            File.WriteAllText(Path.Combine(residual, "rootfile.tmp"), "data");

            bool removed = CleanerService.DeleteOrphanResidualPermanently(residual, SilentLog);

            Assert.True(removed);
            Assert.False(Directory.Exists(residual));
        }
        finally
        {
            try { Directory.Delete(sandbox, recursive: true); } catch { }
        }
    }

    [Fact]
    public void DeletesNestedJunctionsButNeverTheirTargets()
    {
        string sandbox = NewAppDataSandbox();
        try
        {
            // The junction points OUTSIDE the residual folder being deleted. Permanent
            // removal must destroy the link, never the data behind it. Runtime behaviour
            // differs (probe findings): .NET 11 Directory.Delete(recursive) removes a
            // nested junction itself, .NET 10 throws UnauthorizedAccessException on it,
            // .NET Framework FOLLOWS it. The helper therefore detaches every nested link
            // explicitly before the recursive delete - this test keeps that guarantee
            // observable across future runtime changes.
            string targetDir = Path.Combine(sandbox, "link-target");
            string precious = Path.Combine(targetDir, "precious.txt");
            Directory.CreateDirectory(targetDir);
            File.WriteAllText(precious, "do-not-lose");

            string residual = Path.Combine(sandbox, "residual");
            Directory.CreateDirectory(residual);
            File.WriteAllText(Path.Combine(residual, "own-file.tmp"), "data");
            CreateJunction(Path.Combine(residual, "link"), targetDir);

            var logs = new List<string>();
            bool removed = CleanerService.DeleteOrphanResidualPermanently(residual, (m, _) => logs.Add(m));

            Assert.True(removed, "helper refused: " + string.Join(" | ", logs));
            Assert.False(Directory.Exists(residual), "the residual folder itself must be gone");
            Assert.True(File.Exists(precious), "the junction TARGET's content must survive");
        }
        finally
        {
            try { Directory.Delete(sandbox, recursive: true); } catch { }
        }
    }

    [Fact]
    public void RefusesAFolderContainingProtectedAppDataState()
    {
        string sandbox = NewAppDataSandbox();
        try
        {
            string residual = Path.Combine(sandbox, "residual");
            string onedrive = Path.Combine(residual, "Microsoft", "OneDrive");
            Directory.CreateDirectory(onedrive);
            File.WriteAllText(Path.Combine(onedrive, "settings.dat"), "sync state");

            bool removed = CleanerService.DeleteOrphanResidualPermanently(residual, SilentLog);

            Assert.False(removed, "a residual containing durable OneDrive state must be refused");
            Assert.True(Directory.Exists(residual), "the refused folder must be left intact");
        }
        finally
        {
            try { Directory.Delete(sandbox, recursive: true); } catch { }
        }
    }

    [Fact]
    public void RefusesAJunctionRoot()
    {
        string sandbox = NewAppDataSandbox();
        try
        {
            string targetDir = Path.Combine(sandbox, "link-target");
            Directory.CreateDirectory(targetDir);
            string junctionRoot = Path.Combine(sandbox, "junction-root");
            CreateJunction(junctionRoot, targetDir);

            bool removed = CleanerService.DeleteOrphanResidualPermanently(junctionRoot, SilentLog);

            Assert.False(removed, "a junction root must never be deleted as a folder");
            Assert.True(Directory.Exists(junctionRoot), "the junction must be left intact");
        }
        finally
        {
            try { Directory.Delete(sandbox, recursive: true); } catch { }
        }
    }

    [Fact]
    public void ReportsAMissingFolderAsFailure()
    {
        string sandbox = NewAppDataSandbox();
        try
        {
            string missing = Path.Combine(sandbox, "does-not-exist");

            bool removed = CleanerService.DeleteOrphanResidualPermanently(missing, SilentLog);

            Assert.False(removed);
            Assert.False(Directory.Exists(missing));
        }
        finally
        {
            try { Directory.Delete(sandbox, recursive: true); } catch { }
        }
    }
}
