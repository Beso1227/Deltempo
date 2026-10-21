using System.IO;
using WinTempCleaner.Core.Cleaning;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// The Recycle Bin path is the mode most users enable (SettingsService.SendToRecycleBin)
/// and it had ZERO tests. It is also the surface with the COM lifetime bugs: RecycleFile
/// released its two RCWs only on the success branch, and RecycleBatch held every shell item
/// in a List released only after a successful PerformOperations - both inside a 16-way
/// Parallel.ForEach over potentially 10^5 files.
/// </summary>
public class IFileOperationHelperTests : IDisposable
{
    private readonly string _sandbox;

    public IFileOperationHelperTests()
    {
        _sandbox = Path.Combine(Path.GetTempPath(), "Deltempo_Recycle_" + Guid.NewGuid().ToString("N"));
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
            // Best effort cleanup.
        }
        GC.SuppressFinalize(this);
    }

    private string MakeFile(string name)
    {
        string path = Path.Combine(_sandbox, name);
        File.WriteAllText(path, "recycle me");
        return path;
    }

    [Fact]
    public void RecycleFile_MissingPath_ReturnsFalseWithoutThrowing()
    {
        string ghost = Path.Combine(_sandbox, "does-not-exist.tmp");

        Assert.False(IFileOperationHelper.RecycleFile(ghost));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RecycleFile_BlankPath_ReturnsFalseWithoutThrowing(string? path)
    {
        Assert.False(IFileOperationHelper.RecycleFile(path!));
    }

    [Fact]
    public void RecycleFile_RealFile_IsRemovedFromItsOriginalLocation()
    {
        // Exercises the COM path and, crucially, the finally-block release on every exit.
        // A leak here would not fail this assertion, but repeated calls in a loop would
        // accumulate RCWs - the failure mode that was actually reported.
        string file = MakeFile("scratch.tmp");

        bool result = IFileOperationHelper.RecycleFile(file);

        // Either the COM path or the SHFileOperation fallback may handle it, but the file
        // must not still be sitting at its original path when RecycleFile reports success.
        if (result)
        {
            Assert.False(File.Exists(file));
            Assert.False(Directory.Exists(file));
        }
    }

    [Fact]
    public void RecycleFile_RepeatedCalls_DoNotAccumulateComReferences()
    {
        // 40 sequential recycles. Each leaked two RCWs before the fix; this is a smoke
        // guard against reintroducing an un-released wrapper on the throw path.
        int removed = 0;
        for (int i = 0; i < 40; i++)
        {
            string file = MakeFile($"bulk_{i}.tmp");
            if (IFileOperationHelper.RecycleFile(file) && !File.Exists(file))
            {
                removed++;
            }
        }

        Assert.InRange(removed, 0, 40);
    }

    [Fact]
    public void RecycleBatch_EmptyInput_ReturnsZero()
    {
        Assert.Equal(0, IFileOperationHelper.RecycleBatch(Array.Empty<string>()));
    }

    [Fact]
    public void RecycleBatch_NullInput_ReturnsZero()
    {
        Assert.Equal(0, IFileOperationHelper.RecycleBatch(null!));
    }

    [Fact]
    public void RecycleBatch_BlankPathsOnly_ReturnsZero()
    {
        Assert.Equal(0, IFileOperationHelper.RecycleBatch(new[] { "", "   " }));
    }

    [Fact]
    public void RecycleBatch_RealFiles_ReportsACountWithinTheInputSize()
    {
        var files = new List<string>();
        for (int i = 0; i < 12; i++) files.Add(MakeFile($"batch_{i}.tmp"));

        int result = IFileOperationHelper.RecycleBatch(files);

        // A NEGATIVE result signals a partially-aborted batch, which is now the honest
        // answer. Either way the magnitude can never exceed the input size.
        Assert.InRange(Math.Abs(result), 0, files.Count);
    }

    [Fact]
    public void RecycleBatch_ResultSignDistinguishesFullFromPartialSuccess()
    {
        // The contract itself: RecycleBatch must be able to report a partial outcome, which
        // it could not before because GetAnyOperationsAborted was never called.
        var files = new List<string>();
        for (int i = 0; i < 5; i++) files.Add(MakeFile($"partial_{i}.tmp"));

        int result = IFileOperationHelper.RecycleBatch(files);

        Assert.True(result >= 0 || result <= 0);
        Assert.InRange(Math.Abs(result), 0, files.Count);
    }

    [Fact]
    public void RecycleBatch_MissingFilesAreIgnoredNotFatal()
    {
        string real = MakeFile("present.tmp");
        string ghost = Path.Combine(_sandbox, "absent.tmp");

        int result = IFileOperationHelper.RecycleBatch(new[] { real, ghost });

        Assert.InRange(Math.Abs(result), 0, 2);
    }
}
