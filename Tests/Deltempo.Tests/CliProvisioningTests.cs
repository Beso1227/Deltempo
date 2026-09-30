using System.IO;
using WinTempCleaner.Core.Update;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Regression coverage for the "open the app once, then use the CLI" contract.
/// </summary>
/// <remarks>
/// The guiding invariant is that no artifact Deltempo writes may point at the GUI executable:
/// it is a WinExe requiring elevation, so routing a terminal command through it raises UAC and
/// detaches the process from the caller's console, producing no output and no exit code.
/// </remarks>
public class CliProvisioningTests : IDisposable
{
    private readonly string _sandbox = Path.Combine(
        Path.GetTempPath(), "Deltempo_CliProvision_" + Guid.NewGuid().ToString("N"));

    private const string CliPath = @"C:\Tools\Deltempo\bin\deltempo_cli.exe";

    public CliProvisioningTests() => Directory.CreateDirectory(_sandbox);

    public void Dispose()
    {
        try { if (Directory.Exists(_sandbox)) Directory.Delete(_sandbox, recursive: true); } catch { }
    }

    // ── The core bug: every generated artifact must target the console binary ──

    [Fact]
    public void CmdShim_TargetsConsoleBinaryAndPropagatesExitCode()
    {
        string content = CliProvisioningService.BuildCmdContent(CliPath);

        Assert.Contains("\"" + CliPath + "\" %*", content);

        // `start` swallows the child's exit code, which silently breaks every scripted command.
        Assert.DoesNotContain("start ", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exit /b %ERRORLEVEL%", content);
    }

    [Fact]
    public void Ps1Shim_PropagatesNativeExitCode()
    {
        string content = CliProvisioningService.BuildPs1Content(CliPath);

        Assert.Contains(CliPath, content);
        Assert.Contains("exit $LASTEXITCODE", content);
    }

    [Fact]
    public void NoGeneratedArtifact_EverReferencesTheGuiExecutable()
    {
        // Regression guard: this is the exact defect that made `deltempo` unusable for users.
        foreach (string content in new[]
        {
            CliProvisioningService.BuildCmdContent(CliPath),
            CliProvisioningService.BuildPs1Content(CliPath),
            CliProvisioningService.BuildProfileBlock(CliPath)
        })
        {
            Assert.DoesNotContain("Deltempo.exe", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── Profile block: idempotent, self-healing, non-destructive ──

    [Fact]
    public void ProfileBlock_IsIdempotent()
    {
        string once = CliProvisioningService.UpsertManagedBlock(string.Empty, CliPath);
        string twice = CliProvisioningService.UpsertManagedBlock(once, CliPath);

        Assert.Equal(once, twice);
        Assert.Equal(1, CountOccurrences(twice, "function deltempo"));
    }

    [Fact]
    public void ProfileBlock_RepairsStalePathFromOlderInstall()
    {
        // The legacy one-line install wrote this exact snippet pointing at a missing binary, and
        // the old repair regex could not replace it, so the broken command survived forever.
        string legacy = "Set-Alias ll Get-ChildItem\r\n" +
                        "\r\n# Deltempo Synchronous CLI\r\n" +
                        "function deltempo { & \"D:\\Old\\Path\\deltempo_cli.exe\" @args }\r\n";

        string updated = CliProvisioningService.UpsertManagedBlock(legacy, CliPath);

        Assert.DoesNotContain("D:\\Old\\Path", updated);
        Assert.Contains(CliPath, updated);
        Assert.Contains("Set-Alias ll Get-ChildItem", updated);
    }

    [Fact]
    public void ProfileBlock_PreservesUserAuthoredContent()
    {
        string user = "function Get-Foo { 'bar' }\r\nImport-Module PoshGit\r\n";

        string updated = CliProvisioningService.UpsertManagedBlock(user, CliPath);

        Assert.Contains("function Get-Foo", updated);
        Assert.Contains("Import-Module PoshGit", updated);
        Assert.Contains("function deltempo", updated);
    }

    [Fact]
    public void ProfileBlock_DoesNotTouchForeignFunctionNamedDeltempo()
    {
        // A user-defined function must survive; only Deltempo's own marked block is managed.
        string user = "function deltempo { 'my own tool' }\r\n";

        string updated = CliProvisioningService.UpsertManagedBlock(user, CliPath);

        Assert.Contains("my own tool", updated);
        Assert.Contains(CliPath, updated);
    }

    [Fact]
    public void RemoveManagedBlock_StripsOnlyTheManagedBlock()
    {
        string withBlock = CliProvisioningService.UpsertManagedBlock("Import-Module PoshGit\r\n", CliPath);

        bool removed = CliProvisioningService.RemoveManagedBlock(withBlock, out string updated);

        Assert.True(removed);
        Assert.DoesNotContain("function deltempo", updated);
        Assert.Contains("Import-Module PoshGit", updated);
    }

    [Fact]
    public void RemoveManagedBlock_ReportsNoChangeWhenAbsent()
    {
        bool removed = CliProvisioningService.RemoveManagedBlock("Import-Module PoshGit\r\n", out string updated);

        Assert.False(removed);
        Assert.Equal("Import-Module PoshGit\r\n", updated);
    }

    [Fact]
    public void ProfileTargetsBinary_DetectsMatchingAndStaleTargets()
    {
        string current = CliProvisioningService.UpsertManagedBlock(string.Empty, CliPath);

        Assert.True(CliProvisioningService.ProfileTargetsBinary(current, CliPath));
        Assert.False(CliProvisioningService.ProfileTargetsBinary(current, @"C:\Elsewhere\deltempo_cli.exe"));
    }

    // ── Binary validation ──

    [Fact]
    public void IsUsableCliBinary_RejectsMissingAndUndersizedFiles()
    {
        Assert.False(CliProvisioningService.IsUsableCliBinary(null));
        Assert.False(CliProvisioningService.IsUsableCliBinary(""));
        Assert.False(CliProvisioningService.IsUsableCliBinary(Path.Combine(_sandbox, "missing.exe")));

        string stub = Path.Combine(_sandbox, "stub.exe");
        File.WriteAllText(stub, "not a real binary");
        Assert.False(CliProvisioningService.IsUsableCliBinary(stub));
    }

    [Fact]
    public void IsUsableCliBinary_AcceptsFrameworkDependentApphostWithSiblingDll()
    {
        // A framework-dependent build ships a small apphost plus its managed dll. Rejecting it
        // would break provisioning for anyone not using the self-contained single-file publish.
        string dir = Path.Combine(_sandbox, "fd");
        Directory.CreateDirectory(dir);
        string exe = Path.Combine(dir, "deltempo_cli.exe");
        string dll = Path.ChangeExtension(exe, ".dll");

        File.WriteAllBytes(exe, new byte[200 * 1024]);
        Assert.False(CliProvisioningService.IsUsableCliBinary(exe));   // no managed dll yet

        File.WriteAllBytes(dll, new byte[1024]);
        Assert.True(CliProvisioningService.IsUsableCliBinary(exe));
    }

    [Fact]
    public void IsUsableCliBinary_AcceptsLargeSelfContainedSingleFile()
    {
        string dir = Path.Combine(_sandbox, "sc");
        Directory.CreateDirectory(dir);
        string exe = Path.Combine(dir, "deltempo_cli.exe");

        File.WriteAllBytes(exe, new byte[25 * 1024 * 1024]);

        Assert.True(CliProvisioningService.IsUsableCliBinary(exe));
    }

    [Fact]
    public void ToolDirectory_IsUserWritableAndMatchesInstallerCache()
    {
        // Must stay aligned with docs/win, which caches the binary in this exact directory.
        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Deltempo", "bin"),
            CliProvisioningService.ToolDirectory);
    }

    [Fact]
    public void FindLocalCliBinary_IgnoresPlaceholderFiles()
    {
        string decoy = Path.Combine(_sandbox, "deltempo_cli.exe");
        File.WriteAllText(decoy, "stub");

        string? found = CliProvisioningService.FindLocalCliBinary();

        // A placeholder must never be promoted to the command target.
        Assert.NotEqual(decoy, found);
    }

    // ── Download trust boundary ──

    [Fact]
    public void CliDownloadUrl_MustPassTheSsrfValidator()
    {
        Assert.True(UpdateSecurityValidator.IsValidDownloadUrl(
            "https://github.com/Beso1227/Deltempo/releases/download/v3.0.0/deltempo_cli.exe", out _));

        // Any host or path outside the official release distribution is rejected.
        Assert.False(UpdateSecurityValidator.IsValidDownloadUrl(
            "https://evil.example.com/Beso1227/Deltempo/releases/download/v3.0.0/deltempo_cli.exe", out _));
        Assert.False(UpdateSecurityValidator.IsValidDownloadUrl(
            "https://github.com/attacker/Deltempo/releases/download/v3.0.0/deltempo_cli.exe", out _));
    }

    [Fact]
    public void ChecksumManifest_ParsesTheCliDigest()
    {
        const string manifest = "40cb6d2d662d21441ab91fca8c796a6fbe4b708260a7719c34c3f3e9d95bedfd  Deltempo.exe\n" +
                                "f87e8f38cac29cb448a7117b497f6dc3f163b3efafd482504d762ec698dea49c  deltempo_cli.exe\n";

        Assert.Equal(
            "f87e8f38cac29cb448a7117b497f6dc3f163b3efafd482504d762ec698dea49c",
            UpdateService.ParseSha256FromChecksums(manifest, "deltempo_cli.exe"));
    }

    // ── Registration status ──

    [Fact]
    public void RegistrationStatus_RequiresTheConsoleBinaryToBeConsideredComplete()
    {
        var withoutBinary = new CliRegistrationService.RegistrationStatus(
            IsCliBinaryAvailable: false, IsRegisteredInUserPath: true, IsRegisteredInAppPaths: true,
            IsRegisteredInPowerShellProfile: true, HasWrapperScripts: true, CliBinaryPath: null);

        var withBinary = withoutBinary with { IsCliBinaryAvailable = true, CliBinaryPath = CliPath };

        Assert.False(withoutBinary.IsFullyRegistered);
        Assert.True(withBinary.IsFullyRegistered);
    }

    [Fact]
    public void RegisterStatusCommand_ReportsWithoutMutatingTheMachine()
    {
        var status = CliRegistrationService.GetRegistrationStatus();

        // Read-only by contract; only the binary's existence is observable here.
        Assert.Equal(CliProvisioningService.IsUsableCliBinary(status.CliBinaryPath), status.IsCliBinaryAvailable);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }
}
