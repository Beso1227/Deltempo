using System.IO;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace WinTempCleaner.Tests;

public static class TestRunner
{
    public static async Task<bool> RunVerificationAsync()
    {
        Console.WriteLine("[HERO TEST] Starting automated verification of Deltempo Engine...");

        var testDir = Path.Combine(Path.GetTempPath(), "Deltempo_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);
        var subDir = Path.Combine(testDir, "NestedSubFolder");
        Directory.CreateDirectory(subDir);

        try
        {
            // 1. Create dummy old file (>24h old)
            var oldFile = Path.Combine(testDir, "old_cache.tmp");
            File.WriteAllText(oldFile, new string('A', 1024 * 50)); // 50 KB
            File.SetLastWriteTime(oldFile, DateTime.Now - TimeSpan.FromHours(48));

            // 2. Create dummy recent file (<24h old)
            var recentFile = Path.Combine(testDir, "recent_installer.tmp");
            File.WriteAllText(recentFile, new string('B', 1024 * 30)); // 30 KB
            File.SetLastWriteTime(recentFile, DateTime.Now - TimeSpan.FromHours(2));

            // 3. Create locked file
            var lockedFile = Path.Combine(testDir, "active_process.lock");
            File.WriteAllText(lockedFile, "active lock");
            using var fileLockStream = new FileStream(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var cleanerService = new CleanerService();
            var target = new TargetFolderInfo
            {
                Id = "HeroTestTarget",
                Name = "Sandbox Test Target",
                Category = "Test",
                FolderPath = testDir,
                IsSafeModeEligible = true
            };

            // Test Scan
            var logs = new List<string>();
            await cleanerService.ScanFolderAsync(target, (msg, lvl) => logs.Add($"[{lvl}] {msg}"), CancellationToken.None);

            Console.WriteLine($"[TEST] Scanned bytes: {target.SizeBytes}, files: {target.FileCount}");

            // Test Clean with Safe Mode = true (should protect recentFile)
            var (freed, filesDel, foldersDel, filesSkip) = await cleanerService.CleanFolderAsync(
                target,
                safeMode24Hours: true,
                logAction: (msg, lvl) => logs.Add($"[{lvl}] {msg}"),
                progressReport: p => { },
                ct: CancellationToken.None);

            Console.WriteLine($"[TEST] Safe Cleaned: {freed} bytes freed, {filesDel} deleted, {filesSkip} skipped.");

            bool oldDeleted = !File.Exists(oldFile);
            bool recentPreserved = File.Exists(recentFile); // Must be preserved by Safe Mode!
            bool lockedPreserved = File.Exists(lockedFile);  // Must be preserved due to lock!

            Console.WriteLine($"[TEST] Old file deleted: {oldDeleted}");
            Console.WriteLine($"[TEST] Recent file protected by Safety Shield: {recentPreserved}");
            Console.WriteLine($"[TEST] Active locked file safely skipped: {lockedPreserved}");

            bool passed = oldDeleted && recentPreserved && lockedPreserved && filesDel == 1;
            Console.WriteLine(passed ? "[HERO TEST PASS] All enhanced verification checks PASSED!" : "[HERO TEST FAIL] Checks failed.");
            return passed;
        }
        finally
        {
            try
            {
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, true);
                }
            }
            catch { }
        }
    }
}
