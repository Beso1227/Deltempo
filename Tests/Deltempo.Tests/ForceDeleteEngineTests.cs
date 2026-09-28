using System.IO;
using WinTempCleaner.Core.Cleaning;
using WinTempCleaner.Core.Safety;
using Xunit;

namespace Deltempo.Tests;

public class ForceDeleteEngineTests : IDisposable
{
    private readonly string _sandbox;

    public ForceDeleteEngineTests()
    {
        _sandbox = Path.Combine(Path.GetTempPath(), "Deltempo_Force_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_sandbox);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_sandbox))
            {
                Directory.Delete(_sandbox, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public void Gate_ProtectsDriveRoot()
    {
        var decision = ForceDeleteGate.Evaluate(@"C:\");
        Assert.Equal(ForceDeleteTier.AbsoluteBlock, decision.Tier);
    }

    [Fact]
    public void Gate_ProtectsSystem32()
    {
        string sys32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "kernel32.dll");
        var decision = ForceDeleteGate.Evaluate(sys32);
        Assert.Equal(ForceDeleteTier.AbsoluteBlock, decision.Tier);
    }

    [Fact]
    public void Gate_AllowsTempFile()
    {
        string tempFile = Path.Combine(_sandbox, "test.tmp");
        File.WriteAllText(tempFile, "hello");
        var decision = ForceDeleteGate.Evaluate(tempFile);
        Assert.Equal(ForceDeleteTier.Allowed, decision.Tier);
    }

    [Fact]
    public void Gate_ProtectsProgramFilesRoot()
    {
        // Regression: the root escaped the StartsWith(root + "\\") check and evaluated
        // as Allowed, which would have let a user queue the whole Program Files tree.
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var decision = ForceDeleteGate.Evaluate(programFiles);
        Assert.Equal(ForceDeleteTier.AbsoluteBlock, decision.Tier);
    }

    [Fact]
    public void Gate_ProtectsProgramDataRoot()
    {
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var decision = ForceDeleteGate.Evaluate(programData);
        Assert.Equal(ForceDeleteTier.AbsoluteBlock, decision.Tier);
    }

    [Fact]
    public void Gate_DoesNotBlockUserFolderThatMerelySharesSystemNames()
    {
        // Regression: substring matching treated any "...\windows\system32" path as
        // system-critical, permanently blocking legitimate user directories.
        string decoy = Path.Combine(_sandbox, "windows", "system32");
        Directory.CreateDirectory(decoy);

        var decision = ForceDeleteGate.Evaluate(decoy);

        Assert.Equal(ForceDeleteTier.Allowed, decision.Tier);
    }

    [Fact]
    public void Gate_StillBlocksRealWindowsSubdirectories()
    {
        // Counterpart to the decoy test: anchoring must not weaken the real protection.
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        foreach (string relative in new[] { "System32", "WinSxS", @"System32\config" })
        {
            var decision = ForceDeleteGate.Evaluate(Path.Combine(winDir, relative));
            Assert.Equal(ForceDeleteTier.AbsoluteBlock, decision.Tier);
        }
    }

    [Fact]
    public async Task ForceDelete_DryRun_DoesNotDeleteAndCountsDirectoriesSeparately()
    {
        string dir = Path.Combine(_sandbox, "dryrun_dir");
        Directory.CreateDirectory(dir);
        string file = Path.Combine(_sandbox, "dryrun_file.tmp");
        File.WriteAllText(file, "x");

        var res = await ForceDeleteEngine.ForceDeleteAsync(
            new[] { file, dir },
            new ForceDeleteOptions { DryRun = true });

        Assert.Equal(1, res.FilesDeleted);
        Assert.Equal(1, res.DirectoriesDeleted);
        Assert.True(File.Exists(file), "Dry run must not delete.");
        Assert.True(Directory.Exists(dir), "Dry run must not delete.");
    }

    [Fact]
    public async Task ForceDelete_ShieldedTargetIsNeverDeleted()
    {
        // Even with every aggressive option enabled, Tier A paths stay put.
        var options = new ForceDeleteOptions
        {
            StripReadOnlySystemHidden = true,
            TakeOwnershipAndResetAcl = true,
            TerminateLockingProcesses = true,
            ScheduleRebootIfLocked = false,
            ExplicitOverrideConfirmed = true
        };

        var res = await ForceDeleteEngine.ForceDeleteAsync(new[] { @"C:\" }, options);

        Assert.Equal(0, res.FilesDeleted);
        Assert.Equal(0, res.DirectoriesDeleted);
        Assert.True(Directory.Exists(@"C:\"));
    }

    [Fact]
    public async Task ForceDelete_DeletesReadOnlyFile()
    {
        string roFile = Path.Combine(_sandbox, "readonly.tmp");
        File.WriteAllText(roFile, "stubborn content");
        File.SetAttributes(roFile, FileAttributes.ReadOnly);

        var options = new ForceDeleteOptions { StripReadOnlySystemHidden = true };
        var res = await ForceDeleteEngine.ForceDeleteAsync(new[] { roFile }, options);

        Assert.Equal(1, res.FilesDeleted);
        Assert.False(File.Exists(roFile));
    }

    [Fact]
    public async Task ForceDelete_DeletesNestedDirectoryWithReadOnlyFiles()
    {
        string subDir = Path.Combine(_sandbox, "sub_tree");
        Directory.CreateDirectory(subDir);
        string f1 = Path.Combine(subDir, "f1.dat");
        File.WriteAllText(f1, "f1 data");
        File.SetAttributes(f1, FileAttributes.ReadOnly);

        string deepDir = Path.Combine(subDir, "deep");
        Directory.CreateDirectory(deepDir);
        string f2 = Path.Combine(deepDir, "f2.dat");
        File.WriteAllText(f2, "f2 data");

        var options = new ForceDeleteOptions { StripReadOnlySystemHidden = true };
        var res = await ForceDeleteEngine.ForceDeleteAsync(new[] { subDir }, options);

        Assert.True(res.DirectoriesDeleted >= 1);
        Assert.False(Directory.Exists(subDir));
    }

    [Fact]
    public async Task ForceDelete_ShieldsSystemCriticalWithoutTouching()
    {
        string sys32File = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "calc.exe");
        var options = new ForceDeleteOptions { ExplicitOverrideConfirmed = true };
        var res = await ForceDeleteEngine.ForceDeleteAsync(new[] { sys32File }, options);

        Assert.Equal(1, res.ShieldedCount);
        Assert.Equal(0, res.FilesDeleted);
    }

    [Fact]
    public async Task ForceDelete_DryRunDoesNotMutate()
    {
        string dummy = Path.Combine(_sandbox, "dry_run.txt");
        File.WriteAllText(dummy, "stay here");

        var options = new ForceDeleteOptions { DryRun = true, ExplicitOverrideConfirmed = true };
        var res = await ForceDeleteEngine.ForceDeleteAsync(new[] { dummy }, options);

        Assert.Equal(1, res.FilesDeleted); // Simulated
        Assert.True(File.Exists(dummy)); // Still exists
    }

    [Fact]
    public void Profile_IdentifiesReadOnlyAndIssues()
    {
        string roFile = Path.Combine(_sandbox, "profile_test.tmp");
        File.WriteAllText(roFile, "data");
        File.SetAttributes(roFile, FileAttributes.ReadOnly);

        var p = ForceDeleteEngine.Profile(roFile);
        Assert.True(p.Exists);
        Assert.True(p.HasReadOnlyOrSystemAttributes);
        Assert.Contains("Read-Only", p.SummaryIssues);
    }
}
