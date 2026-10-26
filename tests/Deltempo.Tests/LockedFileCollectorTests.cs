using System;
using System.Collections.Generic;
using WinTempCleaner.Core.Cleaning;
using WinTempCleaner.Models;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// The shared collector both shells use to build the In-Use &amp; Locked Files inspector.
///
/// The inspector's whole job is to explain a file the clean could not delete, so the
/// collector is the only place where "what the audit trail recorded" becomes "what the
/// user is told". It used to be an inline loop inside WPF's celebration click handler;
/// it now feeds the WinUI preview too, which makes a silent drift between the two shells
/// possible for the first time. These cases pin the contract instead:
///
///  - only FileLocked failures belong in the list (an access-denied failure is a
///    different problem and the inspector has no remedy for it),
///  - the locking process is folded into the audit reason by the executor and has to be
///    parsed back out, with and without a PID,
///  - a missing reason falls back to the WPF wording rather than an empty row.
///
/// They build records directly, so nothing here touches the filesystem or schedules a
/// deletion.
/// </summary>
public class LockedFileCollectorTests
{
    private static TargetFolderInfo TargetWith(params DeletionAuditRecord[] records)
    {
        var result = new CleanupTransactionResult();
        foreach (DeletionAuditRecord record in records)
        {
            result.AuditRecords.Add(record);
        }

        return new TargetFolderInfo { LastTransactionResult = result };
    }

    private static DeletionAuditRecord Locked(
        string path,
        string? reason,
        CleanupErrorCategory category = CleanupErrorCategory.FileLocked,
        long sizeBytes = 0) => new()
        {
            FilePath = path,
            FileName = System.IO.Path.GetFileName(path),
            Status = DeletionAuditStatus.Failed,
            ErrorCategory = category,
            SizeBytes = sizeBytes,
            ErrorOrSkipReason = reason,
        };

    [Fact]
    public void FromTargets_MapsOnlyFileLockedFailures()
    {
        var target = TargetWith(
            Locked(@"C:\Temp\held.tmp", "Locked. process: chrome (PID: 1234)"),
            Locked(@"C:\Temp\denied.tmp", "Access denied", CleanupErrorCategory.AccessDenied));

        List<LockedFileItem> rows = LockedFileCollector.FromTargets([target]);

        LockedFileItem row = Assert.Single(rows);
        Assert.Equal(@"C:\Temp\held.tmp", row.FilePath);
    }

    [Fact]
    public void FromTargets_ParsesProcessNameAndPid()
    {
        var target = TargetWith(
            Locked(@"C:\Temp\cache.bin", "File in use. process: chrome (PID: 4321)", sizeBytes: 2048));

        LockedFileItem row = Assert.Single(LockedFileCollector.FromTargets([target]));

        Assert.Equal("chrome", row.LockingProcessName);
        Assert.Equal(4321, row.LockingProcessId);
        Assert.Equal(2048, row.SizeBytes);
        Assert.Equal("cache.bin", row.FileName);
    }

    [Fact]
    public void FromTargets_ParsesProcessNameWithoutPid()
    {
        var target = TargetWith(Locked(@"C:\Temp\shader.pso", "Held by process: explorer"));

        LockedFileItem row = Assert.Single(LockedFileCollector.FromTargets([target]));

        Assert.Equal("explorer", row.LockingProcessName);
        Assert.Equal(0, row.LockingProcessId);
    }

    [Fact]
    public void FromTargets_NullOrEmptyTargets_ReturnsEmpty()
    {
        Assert.Empty(LockedFileCollector.FromTargets(null));
        Assert.Empty(LockedFileCollector.FromTargets(Array.Empty<TargetFolderInfo>()));
    }

    [Fact]
    public void FromTargets_TargetWithoutTransaction_ContributesNothing()
    {
        var neverScanned = new TargetFolderInfo();
        var target = TargetWith(Locked(@"C:\Temp\a.tmp", "process: chrome (PID: 7)"));

        List<LockedFileItem> rows = LockedFileCollector.FromTargets([neverScanned, null, target]);

        Assert.Single(rows);
    }

    [Fact]
    public void FromTargets_MissingReason_UsesTheDefaultWording()
    {
        var target = TargetWith(Locked(@"C:\Temp\unknown.tmp", reason: null));

        LockedFileItem row = Assert.Single(LockedFileCollector.FromTargets([target]));

        Assert.Equal(LockedFileCollector.DefaultLockReason, row.LockReason);
        Assert.Empty(row.LockingProcessName);
        Assert.Equal(0, row.LockingProcessId);
    }
}
