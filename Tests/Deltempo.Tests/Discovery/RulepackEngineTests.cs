using System.IO;
using System.Text.Json;
using WinTempCleaner.Core.Discovery;
using Xunit;

namespace Deltempo.Tests.Discovery;

public class RulepackEngineTests : IDisposable
{
    private readonly string _testRulesDir;

    public RulepackEngineTests()
    {
        _testRulesDir = Path.Combine(Path.GetTempPath(), "Deltempo_RuleTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRulesDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRulesDir))
            {
                Directory.Delete(_testRulesDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public void GetBuiltInRulepacks_ReturnsValidNonEmptyRules()
    {
        var rules = RulepackEngine.GetBuiltInRulepacks();

        Assert.NotEmpty(rules);
        Assert.All(rules, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.Id));
            Assert.False(string.IsNullOrWhiteSpace(r.Name));
            Assert.NotEmpty(r.PathTemplates);
        });
    }

    [Fact]
    public void LoadUserRulepacks_ParsesValidJsonFiles()
    {
        var userRule = new RulepackDefinition
        {
            Id = "CustomAppTest",
            Name = "Custom App Cache",
            Category = "User Cache",
            PathTemplates = new List<string> { @"%TEMP%\CustomApp" }
        };

        string jsonPath = Path.Combine(_testRulesDir, "custom.json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(userRule));

        var loaded = RulepackEngine.LoadUserRulepacks(_testRulesDir);

        Assert.Single(loaded);
        Assert.Equal("CustomAppTest", loaded[0].Id);
        Assert.Equal("Custom App Cache", loaded[0].Name);
    }

    [Fact]
    public void MultiDriveDetector_GetReadyDriveRoots_ReturnsAtLeastSystemDrive()
    {
        var drives = MultiDriveDetector.GetReadyDriveRoots();

        Assert.NotEmpty(drives);
        Assert.Contains(drives, d => d.StartsWith("C:", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildTargetFolderInfos_CreatesTargetWithResolvedOverrides()
    {
        string dummyTarget = Path.Combine(_testRulesDir, "TargetDummy");
        Directory.CreateDirectory(dummyTarget);

        var rule = new RulepackDefinition
        {
            Id = "DummyRule",
            Name = "Dummy Target",
            PathTemplates = new List<string> { dummyTarget }
        };

        var targets = RulepackEngine.BuildTargetFolderInfos(new[] { rule }, isAdmin: true);

        Assert.Single(targets);
        Assert.Equal("DummyRule", targets[0].Id);
        Assert.NotNull(targets[0].ResolvedDirectoriesOverride);
        Assert.Contains(dummyTarget, targets[0].ResolvedDirectoriesOverride!);
    }

    [Fact]
    public void ExpandPathTemplates_WithWildcardSegments_FindsAllMatchingDirectories()
    {
        string dir1 = Path.Combine(_testRulesDir, "IdeA", "log");
        string dir2 = Path.Combine(_testRulesDir, "IdeB", "log");
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        string pattern = Path.Combine(_testRulesDir, "*", "log");
        var resolved = RulepackEngine.ExpandPathTemplates(new[] { pattern });

        Assert.Contains(dir1, resolved);
        Assert.Contains(dir2, resolved);
    }
}
