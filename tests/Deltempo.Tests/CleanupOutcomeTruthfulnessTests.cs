using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WinTempCleaner.Core.Cleaning;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;
using WinTempCleaner.Services.Providers.CacheResolvers;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Covers the cleanup run's user-facing truthfulness: the files that stay behind are
/// attributed to the application holding them, the protection engine refuses the
/// state it must refuse, and the Defender material a cleaner cannot delete is
/// protected rather than attempted.
/// </summary>
public class CleanupOutcomeTruthfulnessTests
{
    private static string ProgramData => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    // ------------------------------------------------------------ protection

    [Theory]
    [InlineData(@"Microsoft\Windows Defender\Scans\History")]
    [InlineData(@"Microsoft\Windows Defender\Scans\History\Results\Resource")]
    [InlineData(@"Microsoft\Windows Defender\Scans\History\Results\Quick")]
    [InlineData(@"Microsoft\Windows Defender\Scans\History\Store")]
    [InlineData(@"Microsoft\Windows Defender\Definition Updates\Backup")]
    [InlineData(@"Microsoft\Windows Defender\Support")]
    [InlineData(@"Microsoft\Windows Defender\Quarantine")]
    public void DefenderState_IsProtected_NotCleanupMaterial(string relative)
    {
        string path = Path.Combine(ProgramData, relative);

        // The platform ACLs these to SYSTEM, so even an elevated cleaner cannot
        // delete them. Offering them and then failing was the bug: one run reported
        // 177 "failed" files, every one of them a Defender scan-history file the app
        // had tried to take ownership of.
        Assert.True(ProtectionPolicy.IsProtected(path, out string reason),
            $"'{path}' must be protected. Reason: {reason}");
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [Fact]
    public void DefenderResolver_ProposesOnlyDisposableMaterial()
    {
        List<string> dirs = SystemCacheResolver.ResolveDefenderDirectories();

        Assert.NotEmpty(dirs);
        foreach (string dir in dirs)
        {
            // Scan history, signature backups, the support bundle and the quarantine
            // are protected state - proposing them cost a scan of thousands of
            // undeletable files and produced a wall of failures.
            Assert.DoesNotContain(dirs, d => d.Contains("Scans\\History", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(dirs, d => d.Contains("Definition Updates", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(dirs, d => d.Contains("Quarantine", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(dirs, d => d.Contains("Defender\\Support", StringComparison.OrdinalIgnoreCase));
        }
    }

    // ------------------------------------------------------- in-use attribution

    [Fact]
    public void InUseFiles_AreAttributedToTheApplicationHoldingThem()
    {
        var target = new TargetFolderInfo
        {
            Id = "truth_scope",
            Name = "Truth Scope",
            FolderPath = Path.GetTempPath(),
            HasAccess = true,
        };

        target.LastTransactionResult = new CleanupTransactionResult
        {
            ScopeId = "truth_scope",
            AuditRecords =
            {
                new DeletionAuditRecord
                {
                    FilePath = @"C:\Temp\a.tmp",
                    FileName = "a.tmp",
                    Status = DeletionAuditStatus.Failed,
                    ErrorCategory = CleanupErrorCategory.FileLocked,
                    ErrorOrSkipReason = "In use by: chrome (PID 4321)",
                    LockingProcessName = "chrome",
                    LockingProcessId = 4321,
                    SizeBytes = 100,
                },
                new DeletionAuditRecord
                {
                    FilePath = @"C:\Temp\b.tmp",
                    FileName = "b.tmp",
                    Status = DeletionAuditStatus.Failed,
                    ErrorCategory = CleanupErrorCategory.FileLocked,
                    ErrorOrSkipReason = "In use by: chrome (PID 4321)",
                    LockingProcessName = "chrome",
                    LockingProcessId = 4321,
                    SizeBytes = 200,
                },
                new DeletionAuditRecord
                {
                    FilePath = @"C:\Temp\c.tmp",
                    FileName = "c.tmp",
                    Status = DeletionAuditStatus.Failed,
                    ErrorCategory = CleanupErrorCategory.FileLocked,
                    ErrorOrSkipReason = "In use by: Discord (PID 999)",
                    LockingProcessName = "Discord",
                    LockingProcessId = 999,
                    SizeBytes = 50,
                },
            },
        };

        List<LockedFileItem> rows = LockedFileCollector.FromTargets([target]);

        Assert.Equal(3, rows.Count);

        // The application name must survive to the screen. The collector used to
        // re-parse the reason string with a regex that expected
        // "process: name (PID: 1)" while the executor writes
        // "In use by: name (PID 1)" - so every row fell back to a generic reason and
        // the user was never told WHICH application was holding the file.
        Assert.All(rows, r => Assert.False(string.IsNullOrWhiteSpace(r.LockingProcessName)));
        Assert.Contains(rows, r => r.LockingProcessName == "chrome" && r.LockingProcessId == 4321);
        Assert.Contains(rows, r => r.LockingProcessName == "Discord" && r.LockingProcessId == 999);

        List<InUseApp> apps = LockedFileCollector.AggregateByApplication(rows);

        Assert.Equal(2, apps.Count);
        Assert.Equal("chrome", apps[0].ProcessName);
        Assert.Equal(2, apps[0].FileCount);
        Assert.Equal(300, apps[0].Bytes);
        Assert.Equal("Discord", apps[1].ProcessName);
        Assert.Equal(1, apps[1].FileCount);

        // The line the popup shows has to name the application and the count.
        Assert.Contains("chrome", apps[0].DisplayText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2", apps[0].DisplayText, StringComparison.Ordinal);
    }

    [Fact]
    public void InUseFiles_WithAnUnknownHolder_AreStillCounted()
    {
        var target = new TargetFolderInfo { Id = "s", Name = "S", FolderPath = Path.GetTempPath() };
        target.LastTransactionResult = new CleanupTransactionResult
        {
            AuditRecords =
            {
                new DeletionAuditRecord
                {
                    FilePath = @"C:\Temp\a.tmp",
                    FileName = "a.tmp",
                    Status = DeletionAuditStatus.Failed,
                    ErrorCategory = CleanupErrorCategory.FileLocked,
                    ErrorOrSkipReason = "In use: active handle or loaded module held by running application",
                },
            },
        };

        List<InUseApp> apps = LockedFileCollector.AggregateByApplication(
            LockedFileCollector.FromTargets([target]));

        // An unnamed holder must not vanish: the count the user sees has to match the
        // number of files that stayed behind.
        Assert.Single(apps);
        Assert.Equal(1, apps[0].FileCount);
    }

    [Fact]
    public void LegacyReasonFormat_IsStillParsed()
    {
        // Records written before the holder was modelled on the record itself carry
        // only the reason string. The parser must still understand both shapes.
        var target = new TargetFolderInfo { Id = "s", Name = "S", FolderPath = Path.GetTempPath() };
        target.LastTransactionResult = new CleanupTransactionResult
        {
            AuditRecords =
            {
                new DeletionAuditRecord
                {
                    FilePath = @"C:\Temp\a.tmp",
                    FileName = "a.tmp",
                    Status = DeletionAuditStatus.Failed,
                    ErrorCategory = CleanupErrorCategory.FileLocked,
                    ErrorOrSkipReason = "Locked by vscode (PID: 777): file in use",
                },
            },
        };

        List<LockedFileItem> rows = LockedFileCollector.FromTargets([target]);

        Assert.Single(rows);
        Assert.Equal("vscode", rows[0].LockingProcessName);
        Assert.Equal(777, rows[0].LockingProcessId);
    }
}
