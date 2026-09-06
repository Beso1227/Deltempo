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
            apply24HourShield: false);

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
                apply24HourShield: false);

            var result = await CleanupExecutor.ExecutePlanAsync(plan, _sandboxDir);

            // Verify safe handling without throwing uncaught exceptions
            Assert.Equal(0, result.DeletedCount);
            Assert.Equal(1, result.FailedCount);
            Assert.NotEmpty(result.ErrorMessages);
        }
    }

    [Fact]
    public void RevalidateBeforeDeletion_SizeChanged_ReturnsFalse()
    {
        string file = Path.Combine(_sandboxDir, "size_change_test.tmp");
        File.WriteAllText(file, "original content");

        bool valid = CleanupExecutor.RevalidateBeforeDeletion(file, _sandboxDir, 999, out string reason);
        Assert.False(valid, "Size mismatch must reject deletion to prevent TOCTOU race.");

        File.Delete(file);
    }

    [Fact]
    public void RevalidateBeforeDeletion_TimestampChanged_ReturnsFalse()
    {
        string file = Path.Combine(_sandboxDir, "timestamp_test.tmp");
        File.WriteAllText(file, "timestamp content");

        bool valid = CleanupExecutor.RevalidateBeforeDeletion(file, _sandboxDir, 0, DateTime.UtcNow.AddHours(-10), out string reason, out FileCleanupFailureReason code);
        Assert.False(valid, "Timestamp drift beyond tolerance must reject deletion.");

        File.Delete(file);
    }

    [Fact]
    public void ExecutePlanAsync_SkipsReviewRequiredFiles()
    {
        string safeFile = Path.Combine(_sandboxDir, "safe_old.tmp");
        File.WriteAllText(safeFile, "safe data");
        File.SetLastWriteTime(safeFile, DateTime.Now - TimeSpan.FromHours(48));

        string reviewFile = Path.Combine(_sandboxDir, "review_recent.tmp");
        File.WriteAllText(reviewFile, "recent data");
        File.SetLastWriteTime(reviewFile, DateTime.Now - TimeSpan.FromMinutes(5));

        var plan = CleanupPlanner.CreatePlan(
            "mixed_scope", "Mixed Scope", new[] { _sandboxDir }, "Temp",
            apply24HourShield: true);

        var safeAction = plan.Actions.First(a => a.FileName == "safe_old.tmp");
        var reviewAction = plan.Actions.First(a => a.FileName == "review_recent.tmp");

        Assert.Equal(IntendedCleanupAction.DeletePermanently, safeAction.Action);
        Assert.Equal(IntendedCleanupAction.SkipReviewRequired, reviewAction.Action);
    }

    [Fact]
    public void ExecutePlanAsync_PureCacheScope_BypassesShieldForUnknownFiles()
    {
        string unknownFile = Path.Combine(_sandboxDir, "mystery_data.xyz");
        File.WriteAllText(unknownFile, "unknown data");
        File.SetLastWriteTime(unknownFile, DateTime.Now - TimeSpan.FromMinutes(2));

        var plan = CleanupPlanner.CreatePlan(
            "cache_scope", "Cache Scope", new[] { _sandboxDir }, "Cache",
            apply24HourShield: false);

        var action = plan.Actions.First(a => a.FileName == "mystery_data.xyz");
        Assert.Equal(IntendedCleanupAction.DeletePermanently, action.Action);
    }

    [Fact]
    public async Task ExecutePlanAsync_SafeFilesAreDeleted()
    {
        string file = Path.Combine(_sandboxDir, "deletable.tmp");
        File.WriteAllText(file, "delete me");

        var plan = CleanupPlanner.CreatePlan(
            "delete_scope", "Delete Scope", new[] { _sandboxDir }, "Temp",
            apply24HourShield: false);

        var result = await CleanupExecutor.ExecutePlanAsync(plan, _sandboxDir);

        Assert.Equal(1, result.DeletedCount);
        Assert.False(File.Exists(file), "Safe file should be deleted by executor.");
    }

    [Fact]
    public void CleanupPlan_ComputedPropertiesReflectCorrectCounts()
    {
        string safeFile = Path.Combine(_sandboxDir, "safe_plan.tmp");
        string protectedFile = Path.Combine(_sandboxDir, "protected_plan.kdbx");
        File.WriteAllText(safeFile, "safe");
        File.WriteAllText(protectedFile, "passwords");

        var plan = CleanupPlanner.CreatePlan(
            "count_scope", "Count Scope", new[] { _sandboxDir }, "Temp",
            apply24HourShield: false);

        Assert.Equal(2, plan.DiscoveredCount);
        Assert.Equal(1, plan.ProtectedCount);
        Assert.Equal(1, plan.EligibleCount);
    }

    [Fact]
    public void CleanupTransactionResult_CompletionStatus_WhenClean_ReturnsClean()
    {
        var result = new CleanupTransactionResult { DeletedCount = 5, FailedCount = 0 };
        Assert.Equal(CleanupCompletionStatus.Clean, result.CompletionStatus);
    }

    [Fact]
    public void CleanupTransactionResult_CompletionStatus_WhenFailed_ReturnsFailed()
    {
        var result = new CleanupTransactionResult { DeletedCount = 0, FailedCount = 3 };
        Assert.Equal(CleanupCompletionStatus.Failed, result.CompletionStatus);
    }

    [Fact]
    public void CleanupTransactionResult_CompletionStatus_WhenPartial_ReturnsCompletedWithWarnings()
    {
        var result = new CleanupTransactionResult { DeletedCount = 3, FailedCount = 1 };
        Assert.Equal(CleanupCompletionStatus.CompletedWithWarnings, result.CompletionStatus);
    }

    [Fact]
    public void CleanupTransactionResult_CompletionStatus_WhenCancelled_ReturnsCancelled()
    {
        var result = new CleanupTransactionResult { WasCancelled = true, DeletedCount = 0 };
        Assert.Equal(CleanupCompletionStatus.Cancelled, result.CompletionStatus);
    }

    [Fact]
    public void CleanupTransactionResult_Success_TrueWhenCleanOrWarnings()
    {
        Assert.True(new CleanupTransactionResult { DeletedCount = 1, FailedCount = 0 }.Success);
        Assert.True(new CleanupTransactionResult { DeletedCount = 1, FailedCount = 1 }.Success);
        Assert.False(new CleanupTransactionResult { DeletedCount = 0, FailedCount = 1 }.Success);
        Assert.False(new CleanupTransactionResult { WasCancelled = true, DeletedCount = 0 }.Success);
    }
}
