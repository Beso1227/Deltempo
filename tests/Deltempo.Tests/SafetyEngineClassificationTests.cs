using System.IO;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Rule 9b of FileSafetyEngine - "non-executable files in designated temporary and cache
/// directories" - is the broadest grant of Safe in the engine, so it is where an
/// over-broad extension list does the most damage. It previously marked any recognized
/// extension inside ANY authorized root as Safe(90), including .json/.db/.xml/.ini and the
/// EMPTY extension, which made every extensionless file under a cleanup root deletable.
///
/// The key invariant these tests pin: a DATA FORMAT is only ever disposable because of
/// where it is, never because of what it is, and an extensionless file is never disposable
/// on the strength of an authorized root alone.
/// </summary>
[Collection("SettingsService")]
public class SafetyEngineClassificationTests : IDisposable
{
    private readonly string _sandbox;
    private readonly string _cacheRoot;

    public SafetyEngineClassificationTests()
    {
        // Deliberately NOT under %TEMP%. A sandbox in Path.GetTempPath() matches the
        // unambiguous-temp-shape branch on its mere location, which would make every test
        // here pass for the wrong reason and hide the extension logic under test.
        _sandbox = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Deltempo_SafetyTest_" + Guid.NewGuid().ToString("N"));
        _cacheRoot = Path.Combine(_sandbox, "AppCache");
        Directory.CreateDirectory(_cacheRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_sandbox)) Directory.Delete(_sandbox, recursive: true);
        }
        catch
        {
            // Best effort.
        }
        GC.SuppressFinalize(this);
    }

    private string MakeFile(string name, int bytes = 64)
    {
        string path = Path.Combine(_cacheRoot, name);
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    private static SafetyAnalysisResult Analyze(string path, string root, bool shield = false) =>
        FileSafetyEngine.Analyze(
            filePath: path,
            allowedRoot: root,
            apply24HourThreshold: shield,
            allowedRoots: new[] { root });

    /// <summary>
    /// The regression this split exists for: an extensionless file inside an authorized
    /// cleanup root. The old extension set contained "", and the old location test was a
    /// bare Contains("cache"), so ANY extensionless file under a cache-shaped root - or
    /// under a directory merely NAMED like one - was Safe(90).
    /// </summary>
    [Fact]
    public void ExtensionlessFile_InsideAuthorizedRoot_IsNotSafe()
    {
        string file = MakeFile("LICENSE");
        file = Path.Combine(_cacheRoot, "LICENSE");
        File.WriteAllText(file, "no extension, proves nothing");

        var result = Analyze(file, _cacheRoot);

        Assert.NotEqual(SafetyRiskTier.Safe, result.Tier);
    }

    [Fact]
    public void ExtensionlessFile_InsideRealTempDirectory_IsStillCleanable()
    {
        // Restricting extensionless files must not make genuine temp directories
        // uncleanable - that would silently regress a legitimate cleanup path.
        string tempDir = Path.Combine(_sandbox, "Temp");
        Directory.CreateDirectory(tempDir);
        string file = Path.Combine(tempDir, "scratch");
        File.WriteAllText(file, "temp fragment");

        var result = FileSafetyEngine.Analyze(
            filePath: file,
            allowedRoot: _sandbox,
            apply24HourThreshold: false,
            allowedRoots: new[] { _sandbox });

        // AppData\Local\Temp is an unambiguous temp shape; a temp-dir-shaped path under the
        // sandbox is the closest equivalent available in a test.
        Assert.NotEqual(SafetyRiskTier.Protected, result.Tier);
    }

    [Theory]
    [InlineData("config.json")]
    [InlineData("state.db")]
    [InlineData("settings.xml")]
    [InlineData("app.ini")]
    [InlineData("data.sqlite")]
    public void DataFormats_AreClassifiedByTierNotByExtensionAlone(string fileName)
    {
        string file = Path.Combine(_cacheRoot, fileName);
        File.WriteAllText(file, "{}");

        var result = Analyze(file, _cacheRoot);

        // Inside a curated cache root a data format is legitimately disposable; the point
        // is that the classification came from the DIRECTORY, and must remain consistent
        // rather than varying by extension spelling.
        Assert.Equal(SafetyRiskTier.Safe, result.Tier);
        Assert.Equal("DesignatedCacheFileRule", result.MatchedRule);
    }

    [Fact]
    public void DataFormat_OutsideAnyAuthorizedRoot_IsRefusedBeforeExtensionMatters()
    {
        string outsideDir = Path.Combine(_sandbox, "Documents");
        Directory.CreateDirectory(outsideDir);
        string file = Path.Combine(outsideDir, "config.json");
        File.WriteAllText(file, "{}");

        var result = FileSafetyEngine.Analyze(
            filePath: file,
            allowedRoot: _cacheRoot,
            apply24HourThreshold: false,
            allowedRoots: new[] { _cacheRoot });

        Assert.Equal(SafetyRiskTier.Protected, result.Tier);
        Assert.Equal("BoundaryContainmentRule", result.MatchedRule);
    }

    /// <summary>
    /// A .tmp file is scratch wherever it lands, so an authorized root is enough. This is
    /// the counterpart to the extensionless case: the split must not become so strict that
    /// real scratch files stop being cleaned.
    /// </summary>
    [Fact]
    public void ScratchFormat_InsideAuthorizedRoot_IsSafe()
    {
        string file = MakeFile("partial.tmp");

        var result = Analyze(file, _cacheRoot);

        Assert.Equal(SafetyRiskTier.Safe, result.Tier);
        // Rule 9 (scratch in a temp-shaped path) is evaluated before rule 9b; both are
        // legitimate Safe verdicts, so assert the tier rather than which rule fired.
        Assert.Contains(result.MatchedRule, new[]
        {
            "TemporaryScratchRule",
            "DesignatedCacheFileRule"
        });
    }

    [Fact]
    public void DriveRootVendorFolder_IsNoLongerSafe100()
    {
        // DriverExtractorCacheRule rated every file under C:\NVIDIA\, C:\AMD\, C:\Intel\,
        // C:\Realtek\ as Safe(100) "VERIFIED CACHE". Those roots are where an installer
        // unpacks payload it has not consumed yet, so that classification was false during
        // an installation.
        string vendorRoot = @"C:\NVIDIA\DisplayDriver\unpacked_payload";
        var result = FileSafetyEngine.Analyze(
            filePath: Path.Combine(vendorRoot, "setup.cfg"),
            allowedRoot: @"C:\NVIDIA",
            apply24HourThreshold: false,
            allowedRoots: new[] { @"C:\NVIDIA" });

        Assert.NotEqual("DriverExtractorCacheRule", result.MatchedRule);
    }

    /// <summary>
    /// Non-disposable state is refused by ProtectionPolicy independently of any resolver
    /// or rulepack that might nominate it as a cleanup root.
    /// </summary>
    [Theory]
    [InlineData(@"C:\ProgramData\Package Cache\{GUID}\package.msi")]
    [InlineData(@"C:\Windows\Installer\$PatchCache$\Cached\file.msi")]
    public void MsiSourceCaches_AreProtectedEvenIfTargetedAsACleanupRoot(string path)
    {
        Assert.True(
            ProtectionPolicy.IsProtected(path, out string reason),
            $"'{path}' should be refused as non-disposable state. Reason: {reason}");
    }

    [Fact]
    public void NonDisposableStateDirectory_IsProtectedAsADirectoryAndAsADescendant()
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Package Cache");

        // The directory ITSELF must be refused, not only files beneath it - a pure
        // prefix test would leave the directory root unprotected while protecting its
        // contents, which is the classic trailing-separator marker bug.
        Assert.True(ProtectionPolicy.IsProtected(root, out _));
        Assert.True(ProtectionPolicy.IsProtected(Path.Combine(root, "SomeGuid", "a.msi"), out _));
    }
}
