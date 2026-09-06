using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WinTempCleaner.Core.Cleaning;
using WinTempCleaner.Core.Safety;
using Xunit;

namespace Deltempo.Tests;

public class TempFilesystemSafetyTests : IDisposable
{
    private readonly string _sandboxDir;

    public TempFilesystemSafetyTests()
    {
        _sandboxDir = Path.Combine(Path.GetTempPath(), "Deltempo_SafetyTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_sandboxDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_sandboxDir))
            {
                // Reset any attributes before cleanup
                foreach (var file in Directory.GetFiles(_sandboxDir, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
                }
                Directory.Delete(_sandboxDir, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public void RevalidateBeforeDeletion_ValidDisposableFileInRoot_ReturnsTrue()
    {
        string filePath = Path.Combine(_sandboxDir, "normal_scratch.tmp");
        File.WriteAllText(filePath, "scratch data");

        bool isValid = CleanupExecutor.RevalidateBeforeDeletion(filePath, _sandboxDir);
        Assert.True(isValid);
    }

    [Fact]
    public void RevalidateBeforeDeletion_NonExistentFile_ReturnsFalse()
    {
        string nonExistent = Path.Combine(_sandboxDir, "ghost.tmp");
        bool isValid = CleanupExecutor.RevalidateBeforeDeletion(nonExistent, _sandboxDir);
        Assert.False(isValid);
    }

    [Fact]
    public void RevalidateBeforeDeletion_FileOutsideDesignatedRoot_ReturnsFalse()
    {
        string otherDir = Path.Combine(Path.GetTempPath(), "Deltempo_Other_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(otherDir);
        try
        {
            string outOfBoundsFile = Path.Combine(otherDir, "outside.tmp");
            File.WriteAllText(outOfBoundsFile, "outside content");

            bool isValid = CleanupExecutor.RevalidateBeforeDeletion(outOfBoundsFile, _sandboxDir);
            Assert.False(isValid);
        }
        finally
        {
            try { Directory.Delete(otherDir, true); } catch { }
        }
    }

    [Fact]
    public void RevalidateBeforeDeletion_SystemAttributeFile_ReturnsFalse()
    {
        string sysFile = Path.Combine(_sandboxDir, "protected_system.tmp");
        File.WriteAllText(sysFile, "system flagged data");
        File.SetAttributes(sysFile, FileAttributes.System);

        try
        {
            bool isValid = CleanupExecutor.RevalidateBeforeDeletion(sysFile, _sandboxDir);
            Assert.False(isValid, "Files with FileAttributes.System must never be validated for deletion!");
        }
        finally
        {
            File.SetAttributes(sysFile, FileAttributes.Normal);
        }
    }

    [Fact]
    public void RevalidateBeforeDeletion_ProtectedSensitiveExtension_ReturnsFalse()
    {
        string keyFile = Path.Combine(_sandboxDir, "private_key.kdbx");
        File.WriteAllText(keyFile, "encrypted credentials");

        bool isValid = CleanupExecutor.RevalidateBeforeDeletion(keyFile, _sandboxDir);
        Assert.False(isValid, "Password manager files (.kdbx) must never be revalidated for deletion!");
    }

    [Fact]
    public void CleanupPlanner_BuildsDeterministicPlanWithExactCounts()
    {
        string f1 = Path.Combine(_sandboxDir, "f1.tmp");
        string f2 = Path.Combine(_sandboxDir, "f2.tmp");
        File.WriteAllText(f1, "12345"); // 5 bytes
        File.WriteAllText(f2, "1234567890"); // 10 bytes

        var plan = CleanupPlanner.CreatePlan(
            "test_scope",
            "Test Scope",
            new[] { _sandboxDir },
            "Temp",
            safeMode24Hours: false);

        Assert.NotNull(plan);
        Assert.Equal(2, plan.DiscoveredCount);
        Assert.Equal(15, plan.DiscoveredBytes);
        Assert.Equal(2, plan.Actions.Count);
        Assert.All(plan.Actions, a => Assert.Equal(IntendedCleanupAction.DeletePermanently, a.Action));
    }

    [Fact]
    public async Task ExecutePlanAsync_LockedFile_DoesNotThrowAndTracksFailureSafely()
    {
        string lockedFile = Path.Combine(_sandboxDir, "locked_in_use.tmp");
        File.WriteAllText(lockedFile, "locked content");

        // Keep file exclusively open
        using (var fs = new FileStream(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var plan = CleanupPlanner.CreatePlan(
                "locked_scope",
                "Locked Scope",
                new[] { _sandboxDir },
                "Temp",
                safeMode24Hours: false);

            var result = await CleanupExecutor.ExecutePlanAsync(plan, _sandboxDir);

            // Verify safe handling without throwing uncaught exceptions
            Assert.Equal(0, result.DeletedCount);
            Assert.Equal(1, result.FailedCount);
            Assert.NotEmpty(result.ErrorMessages);
        }
    }
}
