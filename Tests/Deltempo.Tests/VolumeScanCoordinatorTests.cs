using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinTempCleaner.Core.Scanning;
using Xunit;

namespace Deltempo.Tests
{
    public class VolumeScanCoordinatorTests
    {
        [Fact]
        public void ResolveVolumeKey_StandardDrives_ReturnsDriveRoot()
        {
            Assert.Equal("C:", VolumeScanCoordinator.ResolveVolumeKey(@"C:\Windows\Temp"));
            Assert.Equal("D:", VolumeScanCoordinator.ResolveVolumeKey(@"D:\Games\Cache\file.tmp"));
            Assert.Equal("C:", VolumeScanCoordinator.ResolveVolumeKey(@"c:\users\test\appdata"));
        }

        [Fact]
        public void ResolveVolumeKey_InvalidOrEmpty_ReturnsGeneralDefault()
        {
            Assert.Equal("DEFAULT", VolumeScanCoordinator.ResolveVolumeKey(""));
            Assert.Equal("DEFAULT", VolumeScanCoordinator.ResolveVolumeKey("   "));
            Assert.Equal("DEFAULT", VolumeScanCoordinator.ResolveVolumeKey(null));
        }

        [Fact]
        public void ResolveVolumeKey_SpecialTargets_ReturnsVirtualShell()
        {
            Assert.Equal("VIRTUAL_SHELL", VolumeScanCoordinator.ResolveVolumeKey("RecycleBin"));
            Assert.Equal("VIRTUAL_SHELL", VolumeScanCoordinator.ResolveVolumeKey("VSS_ShadowCopies"));
        }

        [Fact]
        public async Task ExecutePartitionedAsync_RunsAllItemsAcrossVolumes()
        {
            var testItems = new List<string>
            {
                @"C:\Test1",
                @"C:\Test2",
                @"D:\Test1",
                @"D:\Test2",
                @"E:\Test1"
            };

            var processed = new ConcurrentBag<string>();
            var concurrentPerVolume = new ConcurrentDictionary<string, int>();
            int maxObservedPerVolume = 0;

            await VolumeScanCoordinator.ExecutePartitionedAsync(
                testItems,
                path => path,
                async (item, ct) =>
                {
                    string vol = VolumeScanCoordinator.ResolveVolumeKey(item);
                    int current = concurrentPerVolume.AddOrUpdate(vol, 1, (_, c) => c + 1);
                    Interlocked.Exchange(ref maxObservedPerVolume, Math.Max(maxObservedPerVolume, current));

                    await Task.Delay(20, ct);

                    concurrentPerVolume.AddOrUpdate(vol, 0, (_, c) => c - 1);
                    processed.Add(item);
                },
                maxParallelismPerVolume: 2,
                ct: CancellationToken.None
            );

            Assert.Equal(5, processed.Count);
            foreach (var item in testItems)
            {
                Assert.Contains(item, processed);
            }
            Assert.True(maxObservedPerVolume <= 2);
        }

        [Fact]
        public async Task ExecutePartitionedAsync_RespectsCancellation()
        {
            var testItems = Enumerable.Range(1, 20).Select(i => $@"C:\folder{i}").ToList();
            using var cts = new CancellationTokenSource();

            var task = VolumeScanCoordinator.ExecutePartitionedAsync(
                testItems,
                path => path,
                async (item, ct) =>
                {
                    cts.Cancel();
                    await Task.Delay(100, ct);
                },
                maxParallelismPerVolume: 1,
                ct: cts.Token
            );

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        }
    }
}
