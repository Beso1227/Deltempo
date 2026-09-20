using System.IO;
using WinTempCleaner.Core.Scanning;
using Xunit;

namespace Deltempo.Tests.Scanning;

public class NativeFileScannerTests : IDisposable
{
    private readonly string _testRoot;

    public NativeFileScannerTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "Deltempo_ScanTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public void ScanDirectory_FindsAllFilesAcrossSubdirectories()
    {
        // Arrange
        string subDir1 = Path.Combine(_testRoot, "Sub1");
        string subDir2 = Path.Combine(_testRoot, "Sub2", "Nested");
        Directory.CreateDirectory(subDir1);
        Directory.CreateDirectory(subDir2);

        File.WriteAllText(Path.Combine(_testRoot, "root_file.txt"), "Root content");
        File.WriteAllText(Path.Combine(subDir1, "sub1_file.log"), "Sub1 content");
        File.WriteAllText(Path.Combine(subDir2, "nested_file.tmp"), "Nested content with some more bytes");

        // Act
        var discovered = new List<DiscoveredFileItem>();
        NativeFileScanner.ScanDirectory(_testRoot, item => discovered.Add(item), recurse: true);

        // Assert
        Assert.Equal(3, discovered.Count);
        Assert.Contains(discovered, d => d.FileName == "root_file.txt");
        Assert.Contains(discovered, d => d.FileName == "sub1_file.log");
        Assert.Contains(discovered, d => d.FileName == "nested_file.tmp");

        var nested = discovered.First(d => d.FileName == "nested_file.tmp");
        Assert.True(nested.SizeBytes > 0);
        Assert.True(File.Exists(nested.FullPath));
    }

    [Fact]
    public async Task EnumerateFilesAsync_ChannelsAllFilesSuccessfully()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            File.WriteAllText(Path.Combine(_testRoot, $"chan_file_{i}.tmp"), $"Data {i}");
        }

        // Act
        var results = new List<DiscoveredFileItem>();
        await foreach (var item in NativeFileScanner.EnumerateFilesAsync(_testRoot))
        {
            results.Add(item);
        }

        // Assert
        Assert.Equal(10, results.Count);
    }

    [Fact]
    public void ScanDirectoryStats_CalculatesAggregatesCorrectly()
    {
        // Arrange
        string file1 = Path.Combine(_testRoot, "small.dat");
        string file2 = Path.Combine(_testRoot, "large.dat");
        File.WriteAllBytes(file1, new byte[100]);
        File.WriteAllBytes(file2, new byte[1000]);

        // Act
        var (totalBytes, fileCount, topFiles) = NativeFileScanner.ScanDirectoryStats(_testRoot);

        // Assert
        Assert.Equal(2, fileCount);
        Assert.Equal(1100, totalBytes);
        Assert.Equal(2, topFiles.Count);
        Assert.Equal("large.dat", topFiles[0].FileName);
        Assert.Equal(1000, topFiles[0].SizeBytes);
    }

    [Fact]
    public void ScanDirectory_NonExistentDirectory_DoesNotThrow()
    {
        string badPath = Path.Combine(_testRoot, "NonExistentPath_xyz123");
        var items = new List<DiscoveredFileItem>();

        var ex = Record.Exception(() => NativeFileScanner.ScanDirectory(badPath, i => items.Add(i)));

        Assert.Null(ex);
        Assert.Empty(items);
    }
}
