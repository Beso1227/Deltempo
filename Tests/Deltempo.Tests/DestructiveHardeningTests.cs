using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinTempCleaner.Core.Cleaning;
using WinTempCleaner.Core.Safety;
using Xunit;

namespace Deltempo.Tests;

public class DestructiveHardeningTests : IDisposable
{
    private readonly string _sandboxDir;

    public DestructiveHardeningTests()
    {
        _sandboxDir = Path.Combine(Path.GetTempPath(), "Deltempo_Hardening_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_sandboxDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_sandboxDir))
            {
                Directory.Delete(_sandboxDir, true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public void RevalidateBeforeDeletion_RejectsSizeDrift_TOCTOU()
    {
        string filePath = Path.Combine(_sandboxDir, "drift_size.tmp");
        File.WriteAllText(filePath, "Initial Content 12345");

        long plannedSize = new FileInfo(filePath).Length;

        // Simulate TOCTOU: external process writes additional content before deletion
        File.AppendAllText(filePath, " Appended unexpected data!");

        bool revalidated = CleanupExecutor.RevalidateBeforeDeletion(
            filePath,
            _sandboxDir,
            plannedSize,
            null,
            out string failureReason,
            out FileCleanupFailureReason reasonCode);

        Assert.False(revalidated);
        Assert.Equal(FileCleanupFailureReason.SizeChanged, reasonCode);
        Assert.Contains("size changed", failureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RevalidateBeforeDeletion_RejectsTimestampDrift_TOCTOU()
    {
        string filePath = Path.Combine(_sandboxDir, "drift_time.tmp");
        File.WriteAllText(filePath, "Initial Content");

        var fi = new FileInfo(filePath);
        DateTime plannedTimestamp = fi.LastWriteTimeUtc;

        // Simulate TOCTOU: external process touches file mtime into the future
        File.SetLastWriteTimeUtc(filePath, plannedTimestamp.AddMinutes(5));

        bool revalidated = CleanupExecutor.RevalidateBeforeDeletion(
            filePath,
            _sandboxDir,
            fi.Length,
            plannedTimestamp,
            out string failureReason,
            out FileCleanupFailureReason reasonCode);

        Assert.False(revalidated);
        Assert.Equal(FileCleanupFailureReason.TimestampChanged, reasonCode);
        Assert.Contains("timestamp changed", failureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RevalidateBeforeDeletion_RejectsSystemAttributeFiles()
    {
        string filePath = Path.Combine(_sandboxDir, "system_file.tmp");
        File.WriteAllText(filePath, "Sensitive System Payload");
        File.SetAttributes(filePath, FileAttributes.System);

        try
        {
            bool revalidated = CleanupExecutor.RevalidateBeforeDeletion(
                filePath,
                _sandboxDir,
                0,
                null,
                out string failureReason,
                out FileCleanupFailureReason reasonCode);

            Assert.False(revalidated);
            Assert.Equal(FileCleanupFailureReason.SystemAttributeSet, reasonCode);
            Assert.Contains("System attribute", failureReason, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.SetAttributes(filePath, FileAttributes.Normal);
        }
    }

    [Fact]
    public void RevalidateBeforeDeletion_RejectsEscapedRoot()
    {
        string isolatedSubdir = Path.Combine(_sandboxDir, "AllowedSubdir");
        Directory.CreateDirectory(isolatedSubdir);

        string outsideFile = Path.Combine(_sandboxDir, "outside.tmp");
        File.WriteAllText(outsideFile, "Outside allowed boundary");

        bool revalidated = CleanupExecutor.RevalidateBeforeDeletion(
            outsideFile,
            isolatedSubdir,
            0,
            null,
            out string failureReason,
            out FileCleanupFailureReason reasonCode);

        Assert.False(revalidated);
        Assert.Equal(FileCleanupFailureReason.PathEscapedRoot, reasonCode);
        Assert.Contains("not inside designated cleanup root", failureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecutePlanAsync_ProducesCompleteAuditRecords()
    {
        string file1 = Path.Combine(_sandboxDir, "test1.tmp");
        string file2 = Path.Combine(_sandboxDir, "test2.tmp");
        File.WriteAllText(file1, "File 1 content");
        File.WriteAllText(file2, "File 2 content");

        var plan = new CleanupPlan
        {
            ScopeId = "test-audit",
            ScopeName = "Test Audit Scope",
            Actions =
            {
                new PlannedFileAction
                {
                    FilePath = file1,
                    FileName = Path.GetFileName(file1),
                    SizeBytes = new FileInfo(file1).Length,
                    PlannedSizeBytes = new FileInfo(file1).Length,
                    SafetyTier = SafetyRiskTier.Safe,
                    MatchedRule = "TempExtensionRule",
                    Action = IntendedCleanupAction.DeletePermanently,
                    LastModified = File.GetLastWriteTimeUtc(file1)
                },
                new PlannedFileAction
                {
                    FilePath = file2,
                    FileName = Path.GetFileName(file2),
                    SizeBytes = new FileInfo(file2).Length,
                    SafetyTier = SafetyRiskTier.Protected,
                    MatchedRule = "SystemRootProtection",
                    Action = IntendedCleanupAction.SkipProtected,
                    Reason = "System protected file"
                }
            }
        };

        var result = await CleanupExecutor.ExecutePlanAsync(plan, _sandboxDir);

        Assert.Equal(2, result.AuditRecords.Count);

        var auditFile1 = result.AuditRecords.FirstOrDefault(a => a.FilePath == file1);
        Assert.NotNull(auditFile1);
        Assert.Equal(DeletionAuditStatus.Deleted, auditFile1.Status);
        Assert.Equal(SafetyRiskTier.Safe, auditFile1.RiskTier);
        Assert.Equal("TempExtensionRule", auditFile1.MatchedRule);
        Assert.Null(auditFile1.ErrorOrSkipReason);

        var auditFile2 = result.AuditRecords.FirstOrDefault(a => a.FilePath == file2);
        Assert.NotNull(auditFile2);
        Assert.Equal(DeletionAuditStatus.SkippedPolicy, auditFile2.Status);
        Assert.Equal(SafetyRiskTier.Protected, auditFile2.RiskTier);
        Assert.Equal("System protected file", auditFile2.ErrorOrSkipReason);
    }

    [Fact]
    public async Task ExecutePlanAsync_HandlesLockedFileGracefully_AndAuditsFailure()
    {
        string lockedFile = Path.Combine(_sandboxDir, "locked.tmp");
        File.WriteAllText(lockedFile, "Cannot delete me, I am locked!");

        using (var stream = File.Open(lockedFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var plan = new CleanupPlan
            {
                ScopeId = "locked-test",
                ScopeName = "Locked File Test",
                Actions =
                {
                    new PlannedFileAction
                    {
                        FilePath = lockedFile,
                        FileName = Path.GetFileName(lockedFile),
                        SizeBytes = new FileInfo(lockedFile).Length,
                        PlannedSizeBytes = new FileInfo(lockedFile).Length,
                        SafetyTier = SafetyRiskTier.Safe,
                        Action = IntendedCleanupAction.DeletePermanently,
                        LastModified = File.GetLastWriteTimeUtc(lockedFile)
                    }
                }
            };

            var result = await CleanupExecutor.ExecutePlanAsync(plan, _sandboxDir);

            Assert.Equal(1, result.FailedCount);
            Assert.NotEmpty(result.ErrorMessages);

            var audit = result.AuditRecords.FirstOrDefault(a => a.FilePath == lockedFile);
            Assert.NotNull(audit);
            Assert.Equal(DeletionAuditStatus.Failed, audit.Status);
            Assert.NotNull(audit.ErrorOrSkipReason);
        }

        // Verify the file still exists on disk
        Assert.True(File.Exists(lockedFile));
    }

    [Fact]
    public async Task ExecutePlanAsync_MidCleanupCancellation_SetsWasCancelledAndKeepsAudit()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel before execution

        string testFile = Path.Combine(_sandboxDir, "cancel_test.tmp");
        File.WriteAllText(testFile, "To be cancelled");

        var plan = new CleanupPlan
        {
            ScopeId = "cancel-test",
            ScopeName = "Cancellation Test",
            Actions =
            {
                new PlannedFileAction
                {
                    FilePath = testFile,
                    FileName = Path.GetFileName(testFile),
                    SizeBytes = new FileInfo(testFile).Length,
                    SafetyTier = SafetyRiskTier.Safe,
                    Action = IntendedCleanupAction.DeletePermanently,
                    LastModified = File.GetLastWriteTimeUtc(testFile)
                }
            }
        };

        var result = await CleanupExecutor.ExecutePlanAsync(plan, _sandboxDir, ct: cts.Token);

        Assert.True(result.WasCancelled);
        Assert.True(File.Exists(testFile)); // Should not have deleted
    }

    [Fact]
    public void PathSecurity_ReparsePoint_RecognizesReparsePointAttribute()
    {
        string targetDir = Path.Combine(_sandboxDir, "junction_target");
        string linkDir = Path.Combine(_sandboxDir, "junction_link");
        Directory.CreateDirectory(targetDir);

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{linkDir}\" \"{targetDir}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit(3000);

            if (Directory.Exists(linkDir))
            {
                bool isReparse = PathSecurity.IsReparsePointOrLink(linkDir);
                Assert.True(isReparse);

                bool revalidated = CleanupExecutor.RevalidateBeforeDeletion(
                    linkDir,
                    _sandboxDir,
                    0,
                    null,
                    out string failureReason,
                    out FileCleanupFailureReason reasonCode);

                Assert.False(revalidated);
                Assert.Equal(FileCleanupFailureReason.ReparsePointDetected, reasonCode);
            }
        }
        finally
        {
            if (Directory.Exists(linkDir))
            {
                try { Directory.Delete(linkDir); } catch { }
            }
        }
    }
}
