using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WinTempCleaner.Core.Scanning;
using WinTempCleaner.Models;
using Xunit;

namespace Deltempo.Tests
{
    public class TopFilesCollectorTests
    {
        [Fact]
        public void TopFilesCollector_BoundedCapacity_NeverExceedsLimit()
        {
            var collector = new TopFilesCollector(capacity: 10);

            for (int i = 1; i <= 50; i++)
            {
                collector.TryAdd(new JunkFileItem
                {
                    FilePath = $@"C:\temp\file_{i}.dat",
                    FileName = $"file_{i}.dat",
                    SizeBytes = i * 1024,
                    LastModified = DateTime.UtcNow
                });
            }

            var top = collector.ToDescendingList();

            Assert.Equal(10, top.Count);
            // Must contain top 10 largest (41 through 50)
            Assert.Equal(50 * 1024, top[0].SizeBytes);
            Assert.Equal(41 * 1024, top[9].SizeBytes);
        }

        [Fact]
        public void TopFilesCollector_SortsDescending()
        {
            var collector = new TopFilesCollector(capacity: 5);

            long[] sizes = { 100, 500, 200, 900, 300, 150, 800 };
            foreach (var size in sizes)
            {
                collector.TryAdd(new JunkFileItem
                {
                    FilePath = $@"C:\temp\file_{size}.dat",
                    FileName = $"file_{size}.dat",
                    SizeBytes = size,
                    LastModified = DateTime.UtcNow
                });
            }

            var top = collector.ToDescendingList();

            Assert.Equal(5, top.Count);
            long previous = long.MaxValue;
            foreach (var file in top)
            {
                Assert.True(file.SizeBytes <= previous);
                previous = file.SizeBytes;
            }

            Assert.Equal(900, top[0].SizeBytes);
            Assert.Equal(800, top[1].SizeBytes);
            Assert.Equal(500, top[2].SizeBytes);
            Assert.Equal(300, top[3].SizeBytes);
            Assert.Equal(200, top[4].SizeBytes);
        }

        [Fact]
        public void TopFilesCollector_ConcurrentAdditions_AreThreadSafe()
        {
            var collector = new TopFilesCollector(capacity: 30);

            Parallel.For(0, 1000, i =>
            {
                collector.TryAdd(new JunkFileItem
                {
                    FilePath = $@"C:\temp\parallel_{i}.dat",
                    FileName = $"parallel_{i}.dat",
                    SizeBytes = i * 10,
                    LastModified = DateTime.UtcNow
                });
            });

            var top = collector.ToDescendingList();

            Assert.Equal(30, top.Count);
            Assert.Equal(999 * 10, top[0].SizeBytes);
            for (int i = 0; i < top.Count - 1; i++)
            {
                Assert.True(top[i].SizeBytes >= top[i + 1].SizeBytes);
            }
        }
    }
}
