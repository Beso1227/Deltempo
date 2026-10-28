using System;
using System.Collections.Generic;
using System.IO;
using WinTempCleaner.Core.Cleaning;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;
using Xunit;

namespace Deltempo.Tests;

public class CleanupExecutorEdgeTests
{
    private static string NewSandbox() =>
        Path.Combine(Path.GetTempPath(), "Deltempo_CleanupEdge_" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RevalidateBeforeDeletion_FailsClosed_OnNullOrWhitespacePath(string? filePath)
    {
        bool ok = CleanupExecutor.RevalidateBeforeDeletion(filePath!, @"C:\Somewhere", out string reason);

        Assert.False(ok);
        // The convenience overloads refuse a null/whitespace path immediately and do
        // not surface a reason; only the fail-closed boolean is asserted here.
        Assert.True(string.IsNullOrEmpty(reason), $"unexpected reason: '{reason}'");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void RevalidateRootsOverload_FailsClosed_OnNullOrWhitespacePath(string? filePath)
    {
        IEnumerable<string> roots = ["C:\\Somewhere"];

        bool ok = CleanupExecutor.RevalidateBeforeDeletion(filePath!, roots, out string reason);

        Assert.False(ok);
        Assert.True(string.IsNullOrEmpty(reason), $"unexpected reason: '{reason}'");
    }

    [Fact]
    public void RevalidateBeforeDeletion_FailsClosed_WhenCanonicalizationFails()
    {
        // A "..." path segment strips to an empty component during Win32 dot/space
        // normalization, so PathSecurity refuses to return ANY canonical form. That
        // is the deterministic stand-in for "canonicalization impossible" - URLs
        // actually resolve against the CWD instead of throwing.
        Assert.Equal(string.Empty, PathSecurity.NormalizeCanonicalPath(@"C:\safe\...\file.txt"));

        bool ok = CleanupExecutor.RevalidateBeforeDeletion(
            @"C:\safe\...\file.txt",
            ((IEnumerable<string>?)[@"C:\Somewhere"]),
            (long?)null, null,
            out _, out string reason, out FileCleanupFailureReason code);

        Assert.False(ok);
        Assert.Equal("Could not verify canonical path.", reason);
        Assert.Equal(FileCleanupFailureReason.FileMissing, code);
    }

    [Fact]
    public void RevalidateBeforeDeletion_FailsClosed_WhenNoRootIsDesignated()
    {
        // A non-null but empty roots list is an incoherent policy: nothing is
        // explicitly designated, so nothing may be deleted.
        bool ok = CleanupExecutor.RevalidateBeforeDeletion(
            @"C:\SomeFile.txt",
            ((IEnumerable<string>?)["", ""]),
            (long?)null, null,
            out _, out string reason, out FileCleanupFailureReason code);

        Assert.False(ok);
        Assert.Equal("No cleanup root designated; deletion skipped.", reason);
        Assert.Equal(FileCleanupFailureReason.PathEscapedRoot, code);
    }

    [Fact]
    public void RevalidateBeforeDeletion_FailsClosed_WhenPathEscapesRoot()
    {
        string sandbox = NewSandbox();
        try
        {
            Directory.CreateDirectory(sandbox);
            string inside = Path.Combine(sandbox, "inside.txt");
            File.WriteAllText(inside, "x");

            // Sibling directory: same parent, different root (the same shape a
            // traversal escape always produces).
            string outsideRoot = NewSandbox();

            bool ok = CleanupExecutor.RevalidateBeforeDeletion(
                inside,
                ((IEnumerable<string>?)["", outsideRoot]),
                (long?)null, null,
                out _, out string reason, out FileCleanupFailureReason code);

            Assert.False(ok);
            Assert.Equal("Path is not inside designated cleanup root.", reason);
            Assert.Equal(FileCleanupFailureReason.PathEscapedRoot, code);
        }
        finally
        {
            try { Directory.Delete(sandbox, recursive: true); } catch { }
        }
    }

    [Fact]
    public void RevalidateBeforeDeletion_ReportsMissingTarget_WhenFileVanished()
    {
        string sandbox = NewSandbox();
        try
        {
            Directory.CreateDirectory(sandbox);
            string ghost = Path.Combine(sandbox, "ghost.txt");

            bool ok = CleanupExecutor.RevalidateBeforeDeletion(
                ghost,
                ((IEnumerable<string>?)[sandbox]),
                (long?)null, null,
                out _, out string reason, out FileCleanupFailureReason code);

            Assert.False(ok);
            Assert.Equal("Target no longer exists on disk.", reason);
            Assert.Equal(FileCleanupFailureReason.FileMissing, code);
        }
        finally
        {
            try { Directory.Delete(sandbox, recursive: true); } catch { }
        }
    }

    [Fact]
    public void RevalidateBeforeDeletion_Succeeds_ForAPlainFileInsideItsRoot()
    {
        string sandbox = NewSandbox();
        try
        {
            Directory.CreateDirectory(sandbox);
            string file = Path.Combine(sandbox, "plain.bin");
            File.WriteAllBytes(file, new byte[64]);

            bool ok = CleanupExecutor.RevalidateBeforeDeletion(
                file,
                ((IEnumerable<string>?)["", sandbox]),
                (long?)null, null,
                out string validatedPath, out string reason, out _);

            Assert.True(ok, reason);
            // The caller must delete the canonical form, not the raw input.
            Assert.Equal(PathSecurity.NormalizeCanonicalPath(file), validatedPath, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(sandbox, recursive: true); } catch { }
        }
    }

    [Fact]
    public void RevalidateBeforeDeletion_DetectsSizeDrift_WithWrongExpectedSize()
    {
        string sandbox = NewSandbox();
        try
        {
            Directory.CreateDirectory(sandbox);
            string file = Path.Combine(sandbox, "drift.bin");
            File.WriteAllBytes(file, new byte[100]);

            bool ok = CleanupExecutor.RevalidateBeforeDeletion(
                file,
                ((IEnumerable<string>?)["", sandbox]),
                (long?)50, null,
                out _, out string reason, out FileCleanupFailureReason code);

            Assert.False(ok);
            Assert.Contains("File size changed", reason, StringComparison.Ordinal);
            Assert.Equal(FileCleanupFailureReason.SizeChanged, code);
        }
        finally
        {
            try { Directory.Delete(sandbox, recursive: true); } catch { }
        }
    }

    [Fact]
    public void RevalidateBeforeDeletion_RejectsDriftedTimestamp_BeyondTolerance()
    {
        string sandbox = NewSandbox();
        try
        {
            Directory.CreateDirectory(sandbox);
            string file = Path.Combine(sandbox, "timed.bin");
            File.WriteAllBytes(file, new byte[100]);
            var nowUtc = DateTime.UtcNow.AddHours(-1);
            File.SetLastWriteTimeUtc(file, nowUtc);

            // Timestamp planned 10 minutes before the actual write: drift > 5s tolerance.
            bool okStaleTs = CleanupExecutor.RevalidateBeforeDeletion(
                file,
                ((IEnumerable<string>?)["", sandbox]),
                (long?)100, nowUtc.AddMinutes(-10),
                out _, out string tsReason, out FileCleanupFailureReason tsCode);

            Assert.False(okStaleTs);
            Assert.Contains("File timestamp changed", tsReason, StringComparison.Ordinal);
            Assert.Equal(FileCleanupFailureReason.TimestampChanged, tsCode);
        }
        finally
        {
            try { Directory.Delete(sandbox, recursive: true); } catch { }
        }
    }

    [Fact]
    public void DeletePermanently_RemovesAPlainFile()
    {
        string sandbox = NewSandbox();
        try
        {
            Directory.CreateDirectory(sandbox);
            string file = Path.Combine(sandbox, "gone.bin");
            File.WriteAllBytes(file, new byte[16]);

            bool deleted = CleanupExecutor.DeletePermanently(file);

            Assert.True(deleted);
            Assert.False(File.Exists(file));
        }
        finally
        {
            try { Directory.Delete(sandbox, recursive: true); } catch { }
        }
    }

    [Fact]
    public void DeletePermanently_TreatsAMissingTargetAsAlreadyDeleted()
    {
        string sandbox = NewSandbox();
        try
        {
            Directory.CreateDirectory(sandbox);
            string ghost = Path.Combine(sandbox, "ghost.bin");

            bool deleted = CleanupExecutor.DeletePermanently(
                ghost, out int win32Error, out string? failureDetail);

            // Idempotent by contract: a target that is already gone counts as
            // success (error 2 is swallowed), so the caller sees True either way.
            Assert.True(deleted);
            Assert.False(File.Exists(ghost));
        }
        finally
        {
            try { Directory.Delete(sandbox, recursive: true); } catch { }
        }
    }
}
