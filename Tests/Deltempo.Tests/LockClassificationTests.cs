using System.IO;
using WinTempCleaner.Core.Safety;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Locked-vs-denied classification. Both branches here previously reported "in use" to the
/// user AND armed a reboot deletion, which is a promise Windows may not keep and which
/// these tests were written to make the code honest about.
/// </summary>
public class LockClassificationTests : IDisposable
{
    private readonly string _sandbox;

    public LockClassificationTests()
    {
        _sandbox = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Deltempo_LockTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_sandbox);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_sandbox)) Directory.Delete(_sandbox, recursive: true);
        }
        catch
        {
            // Best effort.
        }
        GC.SuppressFinalize(this);
    }

    private string MakeFile(string name)
    {
        string path = Path.Combine(_sandbox, name);
        File.WriteAllText(path, "content");
        return path;
    }

    [Fact]
    public void IsFileLocked_UnheldFile_ReportsNotLocked()
    {
        string file = MakeFile("free.tmp");

        Assert.False(RestartManagerService.IsFileLocked(file));
    }

    [Fact]
    public void IsFileLocked_ActuallyHeldFile_ReportsLocked()
    {
        string file = MakeFile("held.tmp");

        using var hold = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.True(RestartManagerService.IsFileLocked(file));
    }

    [Fact]
    public void IsFileLocked_MissingFile_ReportsNotLocked()
    {
        Assert.False(RestartManagerService.IsFileLocked(Path.Combine(_sandbox, "ghost.tmp")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsFileLocked_BlankPath_ReportsNotLocked(string? path)
    {
        Assert.False(RestartManagerService.IsFileLocked(path!));
    }

    /// <summary>
    /// IsFileLocked used to return true for EVERY IOException, so a path too long, an
    /// invalid device name or a transient device error was reported as "locked" - which fed
    /// the executor's schedule-for-reboot branch and armed MoveFileEx against a path we had
    /// never confirmed was busy. Only a real sharing/lock violation counts.
    /// </summary>
    [Fact]
    public void IsFileLocked_DirectoryInsteadOfFile_ReportsNotLocked()
    {
        // Opening a directory as a FileStream throws an IOException that is NOT a lock
        // violation. Reporting it as locked is exactly the bug.
        Assert.False(RestartManagerService.IsFileLocked(_sandbox));
    }

    [Fact]
    public void GetLockingProcesses_UnheldFile_ReturnsEmpty()
    {
        string file = MakeFile("unheld.tmp");

        Assert.Empty(RestartManagerService.GetLockingProcesses(file));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GetLockingProcesses_BlankPath_ReturnsEmptyWithoutThrowing(string? path)
    {
        Assert.Empty(RestartManagerService.GetLockingProcesses(path!));
    }

    [Fact]
    public void GetLockingProcesses_MissingFile_ReturnsEmpty()
    {
        Assert.Empty(RestartManagerService.GetLockingProcesses(Path.Combine(_sandbox, "nope.tmp")));
    }

    /// <summary>
    /// ScheduleRebootDeletion is the most dangerous primitive in the pipeline: Windows
    /// executes the queued delete at Session Manager time, when a different file may occupy
    /// the path. It now enforces its own guards instead of trusting every caller, so these
    /// tests pin that it refuses what it must.
    /// </summary>
    [Fact]
    public void ScheduleRebootDeletion_ProtectedPath_IsRefused()
    {
        string protectedPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Package Cache");

        // Must return false rather than arming a delete against MSI repair state. The
        // directory need not exist for the guard to refuse it - the policy check runs
        // before any filesystem access that would require it.
        Assert.False(RestartManagerService.ScheduleRebootDeletion(protectedPath));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ScheduleRebootDeletion_BlankPath_ReturnsFalse(string? path)
    {
        Assert.False(RestartManagerService.ScheduleRebootDeletion(path!));
    }

    [Fact]
    public void ScheduleRebootDeletion_MissingFile_ReturnsFalse()
    {
        Assert.False(RestartManagerService.ScheduleRebootDeletion(
            Path.Combine(_sandbox, "never-existed.tmp")));
    }

    [Fact]
    public void ScheduleRebootDeletion_TraversalPath_IsRefused()
    {
        // NormalizeCanonicalPath rejects traversal segments; the guard must run before the
        // MoveFileEx is armed, not after.
        string traversal = Path.Combine(_sandbox, "..", "..", "Windows", "System32", "config");

        Assert.False(RestartManagerService.ScheduleRebootDeletion(traversal));
    }

    [Fact]
    public void IsProtectedSystemProcess_ProtectsTheShell()
    {
        Assert.True(RestartManagerService.IsProtectedSystemProcess("explorer"));
        Assert.False(RestartManagerService.IsProtectedSystemProcess("some_unrelated_app"));
        Assert.False(RestartManagerService.IsProtectedSystemProcess(""));
    }
}
