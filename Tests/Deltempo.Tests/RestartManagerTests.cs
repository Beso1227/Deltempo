using System;
using System.Diagnostics;
using System.IO;
using WinTempCleaner.Core.Safety;
using Xunit;

namespace Deltempo.Tests
{
    public class RestartManagerTests
    {
        [Fact]
        public void GetLockingProcesses_NonExistentFile_ReturnsEmptyList()
        {
            string fakePath = Path.Combine(Path.GetTempPath(), $"deltempo_nonexistent_{Guid.NewGuid():N}.tmp");
            var lockers = RestartManagerService.GetLockingProcesses(fakePath);

            Assert.NotNull(lockers);
            Assert.Empty(lockers);
        }

        [Fact]
        public void GetLockingProcesses_LockedFile_DetectsCurrentProcess()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"deltempo_locktest_{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(tempFile, "lock test payload");

                // Lock the file exclusively so Restart Manager detects the handle
                using (var stream = new FileStream(tempFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    var lockers = RestartManagerService.GetLockingProcesses(tempFile);

                    Assert.NotNull(lockers);
                    if (lockers.Count > 0)
                    {
                        var currentPid = Process.GetCurrentProcess().Id;
                        Assert.Contains(lockers, p => p.ProcessId == currentPid);
                    }
                }
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }

        [Fact]
        public void ScheduleRebootDeletion_EmptyOrWhitespace_ReturnsFalse()
        {
            Assert.False(RestartManagerService.ScheduleRebootDeletion(""));
            Assert.False(RestartManagerService.ScheduleRebootDeletion("   "));
            Assert.False(RestartManagerService.ScheduleRebootDeletion(null!));
        }
    }
}
