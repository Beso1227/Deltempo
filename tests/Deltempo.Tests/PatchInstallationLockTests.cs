using System;
using WinTempCleaner.Core.Update;
using Xunit;

namespace Deltempo.Tests;

public class PatchInstallationLockTests
{
    [Fact]
    public void TryAcquire_WhenFree_AcquiresSuccessfully()
    {
        string mutexName = $"Local\\Deltempo_Test_{Guid.NewGuid():N}";
        using var lockObj = PatchInstallationLock.TryAcquire(mutexName, TimeSpan.FromSeconds(2));
        Assert.True(lockObj.HasLock);
    }

    [Fact]
    public void Dispose_ReleasesLockForSubsequentRequesters()
    {
        string mutexName = $"Local\\Deltempo_Test_{Guid.NewGuid():N}";

        using (var lock1 = PatchInstallationLock.TryAcquire(mutexName, TimeSpan.FromSeconds(2)))
        {
            Assert.True(lock1.HasLock);
        } // Disposed here

        using (var lock2 = PatchInstallationLock.TryAcquire(mutexName, TimeSpan.FromSeconds(2)))
        {
            Assert.True(lock2.HasLock, "Lock should be re-acquirable after disposal.");
        }
    }

    [Fact]
    public void TryAcquire_WithDefaultName_AcquiresSuccessfully()
    {
        using var lockObj = PatchInstallationLock.TryAcquire(TimeSpan.FromSeconds(2));
        Assert.True(lockObj.HasLock);
    }
}
