using System.IO;
using WinTempCleaner.Core.Safety;
using Xunit;

namespace Deltempo.Tests.Safety;

public class QuarantineManagerTests : IDisposable
{
    private readonly string _testDir;

    public QuarantineManagerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "Deltempo_QuarTest_" + Guid.NewGuid().ToString("N"));
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
    }

    [Fact]
    public async Task CreateAndRestoreQuarantineSnapshot_RestoresFilesAccurately()
    {
        // Arrange
        string testFile1 = Path.Combine(_testDir, "test1.log");
        string testFile2 = Path.Combine(_testDir, "test2.tmp");
        string content1 = "Log file contents 12345";
        string content2 = "Temporary cache payload abcde";

        File.WriteAllText(testFile1, content1);
        File.WriteAllText(testFile2, content2);

        // Act - Snapshot
        var manifest = await QuarantineManager.CreateQuarantineSnapshotAsync(
            "ScopeTest",
            "Scope Name Test",
            new[] { testFile1, testFile2 });

        Assert.NotNull(manifest);
        Assert.Equal(2, manifest.FileCount);

        // Simulate deletion
        File.Delete(testFile1);
        File.Delete(testFile2);
        Assert.False(File.Exists(testFile1));
        Assert.False(File.Exists(testFile2));

        // Act - Restore
        var (success, restoredCount, restoredBytes, error) = await QuarantineManager.RestoreQuarantineSessionAsync(manifest.SessionId);

        // Assert
        Assert.True(success, error);
        Assert.Equal(2, restoredCount);
        Assert.True(File.Exists(testFile1));
        Assert.True(File.Exists(testFile2));
        Assert.Equal(content1, File.ReadAllText(testFile1));
        Assert.Equal(content2, File.ReadAllText(testFile2));
    }

    [Fact]
    public void GetQuarantineSessions_ReturnsEnumeratedSessions()
    {
        var sessions = QuarantineManager.GetQuarantineSessions();
        Assert.NotNull(sessions);
    }
}
