using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Core.Services;
using Xunit;

namespace Deltempo.Tests;

public class DuplicateFileServiceTests : IDisposable
{
    private readonly string _testDir;

    public DuplicateFileServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "Deltempo_DupTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task FindDuplicatesAsync_UniqueFiles_ReturnsEmptyList()
    {
        File.WriteAllText(Path.Combine(_testDir, "file1.txt"), "Unique Content Alpha");
        File.WriteAllText(Path.Combine(_testDir, "file2.txt"), "Unique Content Beta");
        File.WriteAllText(Path.Combine(_testDir, "file3.txt"), "Unique Content Gamma");

        var result = await DuplicateFileService.FindDuplicatesAsync([_testDir], minFileSizeBytes: 1);

        Assert.Empty(result.Groups);
        Assert.Equal(0, result.TotalDuplicateFilesCount);
        Assert.Equal(0, result.TotalWastedBytes);
    }

    [Fact]
    public async Task FindDuplicatesAsync_IdenticalFiles_ReturnsDuplicateGroup()
    {
        byte[] content = Encoding.UTF8.GetBytes("This is identical duplicated payload content across files.");
        string path1 = Path.Combine(_testDir, "original.bin");
        string path2 = Path.Combine(_testDir, "copy1.bin");
        string path3 = Path.Combine(_testDir, "copy2.bin");

        File.WriteAllBytes(path1, content);
        File.WriteAllBytes(path2, content);
        File.WriteAllBytes(path3, content);

        var result = await DuplicateFileService.FindDuplicatesAsync([_testDir], minFileSizeBytes: 1);

        Assert.Single(result.Groups);
        var group = result.Groups[0];
        Assert.Equal(3, group.Files.Count);
        Assert.Equal(content.Length, group.SizeBytes);
        Assert.Equal(content.Length * 2, group.TotalWastedBytes);
        Assert.Equal(2, result.TotalDuplicateFilesCount);
    }

    [Fact]
    public async Task FindDuplicatesAsync_SameSizeDifferentContent_PrunesCorrectly()
    {
        byte[] data1 = new byte[100];
        byte[] data2 = new byte[100];
        Array.Fill(data1, (byte)'A');
        Array.Fill(data2, (byte)'B');

        File.WriteAllBytes(Path.Combine(_testDir, "diff1.dat"), data1);
        File.WriteAllBytes(Path.Combine(_testDir, "diff2.dat"), data2);

        var result = await DuplicateFileService.FindDuplicatesAsync([_testDir], minFileSizeBytes: 1);

        Assert.Empty(result.Groups);
    }

    [Fact]
    public async Task FindDuplicatesAsync_SameHeaderDifferentBody_PrunedAtStageThree()
    {
        byte[] head = new byte[4096];
        Array.Fill(head, (byte)0x42);

        byte[] fileA = new byte[5000];
        byte[] fileB = new byte[5000];

        Buffer.BlockCopy(head, 0, fileA, 0, 4096);
        Buffer.BlockCopy(head, 0, fileB, 0, 4096);

        fileA[4500] = 0xAA;
        fileB[4500] = 0xBB;

        File.WriteAllBytes(Path.Combine(_testDir, "stage3_a.dat"), fileA);
        File.WriteAllBytes(Path.Combine(_testDir, "stage3_b.dat"), fileB);

        var result = await DuplicateFileService.FindDuplicatesAsync([_testDir], minFileSizeBytes: 1);

        Assert.Empty(result.Groups);
    }

    [Fact]
    public void ApplySelectionStrategy_KeepOldest_SelectsNewerDuplicatesForDeletion()
    {
        string p1 = Path.Combine(_testDir, "old.txt");
        string p2 = Path.Combine(_testDir, "new.txt");

        File.WriteAllText(p1, "Data");
        File.WriteAllText(p2, "Data");

        var group = new DuplicateGroup
        {
            Files =
            {
                new DuplicateFileItem { FilePath = p1, LastModified = new DateTime(2020, 1, 1) },
                new DuplicateFileItem { FilePath = p2, LastModified = new DateTime(2025, 1, 1) }
            }
        };

        var groups = new List<DuplicateGroup> { group };
        DuplicateFileService.ApplySelectionStrategy(groups, DuplicateSelectionStrategy.KeepOldest);

        Assert.False(group.Files[0].IsSelectedForDeletion); // Oldest kept
        Assert.True(group.Files[1].IsSelectedForDeletion);  // Newer selected
    }

    [Fact]
    public void ApplySelectionStrategy_KeepNewest_SelectsOlderDuplicatesForDeletion()
    {
        var group = new DuplicateGroup
        {
            Files =
            {
                new DuplicateFileItem { FilePath = "C:\\file_old.txt", LastModified = new DateTime(2020, 1, 1) },
                new DuplicateFileItem { FilePath = "C:\\file_new.txt", LastModified = new DateTime(2026, 1, 1) }
            }
        };

        var groups = new List<DuplicateGroup> { group };
        DuplicateFileService.ApplySelectionStrategy(groups, DuplicateSelectionStrategy.KeepNewest);

        Assert.True(group.Files[0].IsSelectedForDeletion);  // Old selected
        Assert.False(group.Files[1].IsSelectedForDeletion); // Newest kept
    }

    [Fact]
    public void ApplySelectionStrategy_KeepShortestPath_KeepsShorterPath()
    {
        var group = new DuplicateGroup
        {
            Files =
            {
                new DuplicateFileItem { FilePath = "C:\\Tools\\app.exe" },
                new DuplicateFileItem { FilePath = "C:\\Tools\\Deeply\\Nested\\Directory\\Backup\\app.exe" }
            }
        };

        var groups = new List<DuplicateGroup> { group };
        DuplicateFileService.ApplySelectionStrategy(groups, DuplicateSelectionStrategy.KeepShortestPath);

        Assert.False(group.Files[0].IsSelectedForDeletion); // Shortest kept
        Assert.True(group.Files[1].IsSelectedForDeletion);  // Longer selected
    }

    [Fact]
    public void ApplySelectionStrategy_ProtectedFile_NeverSelectedForDeletion()
    {
        var group = new DuplicateGroup
        {
            Files =
            {
                new DuplicateFileItem { FilePath = "C:\\User\\copy.txt", LastModified = new DateTime(2020, 1, 1) },
                new DuplicateFileItem { FilePath = "C:\\Windows\\System32\\file.dll", LastModified = new DateTime(2025, 1, 1), IsProtected = true }
            }
        };

        var groups = new List<DuplicateGroup> { group };
        // Even if KeepOldest would select the newer file (file.dll), it must NOT be selected because IsProtected == true
        DuplicateFileService.ApplySelectionStrategy(groups, DuplicateSelectionStrategy.KeepOldest);

        Assert.False(group.Files[0].IsSelectedForDeletion); // Oldest kept
        Assert.False(group.Files[1].IsSelectedForDeletion); // Protected file NEVER selected
    }
}
