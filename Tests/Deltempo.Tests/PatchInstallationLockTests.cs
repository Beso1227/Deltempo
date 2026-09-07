using System;
using System.Threading.Tasks;
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
    public async Task TryAcquire_WhenAlreadyHeld_RejectsSecondRequester()
    {
        string mutexName = $"Local\\Deltempo_Test_{Guid.NewGuid():N}";

        using var firstLock = PatchInstallationLock.TryAcquire(mutexName, TimeSpan.FromSeconds(2));
        Assert.True(firstLock.HasLock);

        // Attempt second acquisition on background thread with zero timeout
        bool secondAcquired = await Task.Run(() =>
        {
            using var secondLock = PatchInstallationLock.TryAcquire(mutexName, TimeSpan.Zero);
            return secondLock.HasLock;
        });

        Assert.False(secondAcquired, "Second requester must not acquire the update lock while held!");
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
}
