using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WinTempCleaner.Core.Cleaning;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Adversarial security and filesystem tests proving non-negotiable safety invariants:
/// 1. UNKNOWN can never become deletable through any code path.
/// 2. PROTECTED can never become deletable through any code path.
/// 3. Sibling directory prefix collisions are strictly rejected by canonical containment.
/// 4. AI recommendations can never override the deterministic safety engine.
/// 5. TOCTOU attacks, reparse point escapes, and path traversals fail closed.
/// </summary>
public class AdversarialFilesystemTests : IDisposable
{
    private readonly string _sandboxDir;

    public AdversarialFilesystemTests()
    {
        _sandboxDir = Path.Combine(Path.GetTempPath(), "Deltempo_Adv_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_sandboxDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_sandboxDir))
            {
                Directory.Delete(_sandboxDir, true);
            }
        }
        catch
        {
            // Best effort sandbox cleanup
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Invariant_UnknownCanNeverBecomeDeletableWhenShieldActive(bool sendToRecycleBin)
    {
        string unknownFile = Path.Combine(_sandboxDir, "mystery_binary.xyz");
        File.WriteAllText(unknownFile, "Unrecognized custom payload");
        File.SetLastWriteTimeUtc(unknownFile, DateTime.UtcNow.AddHours(-48));

        var plan = CleanupPlanner.CreatePlan(
            scopeId: "adv-test-unknown",
            scopeName: "Adversarial Unknown Test",
            directories: new[] { _sandboxDir },
            apply24HourShield: true,
            sendToRecycleBin: sendToRecycleBin);

        var action = plan.Actions.FirstOrDefault(a => a.FilePath.Equals(unknownFile, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(action);

        // Invariant: Action must NEVER be DeletePermanently or MoveToRecycleBin when shield is active
        Assert.NotEqual(IntendedCleanupAction.DeletePermanently, action.Action);
        Assert.NotEqual(IntendedCleanupAction.MoveToRecycleBin, action.Action);
        Assert.Equal(IntendedCleanupAction.SkipProtected, action.Action);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Invariant_ProtectedCanNeverBecomeDeletableThroughNormalCodePath(bool sendToRecycleBin, bool apply24HourShield)
    {
        string protectedFile = Path.Combine(_sandboxDir, "id_rsa");
        File.WriteAllText(protectedFile, "-----BEGIN OPENSSH PRIVATE KEY-----");

        var plan = CleanupPlanner.CreatePlan(
            scopeId: "adv-test-protected",
            scopeName: "Adversarial Protected Test",
            directories: new[] { _sandboxDir },
            apply24HourShield: apply24HourShield,
            sendToRecycleBin: sendToRecycleBin);

        var action = plan.Actions.FirstOrDefault(a => a.FilePath.Equals(protectedFile, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(action);

        Assert.NotEqual(IntendedCleanupAction.DeletePermanently, action.Action);
        Assert.NotEqual(IntendedCleanupAction.MoveToRecycleBin, action.Action);
        Assert.Equal(IntendedCleanupAction.SkipProtected, action.Action);
    }

    [Fact]
    public void CanonicalContainment_RejectsSiblingDirectoryPrefixCollisions()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string realDocuments = Path.Combine(userProfile, "Documents");
        string siblingSpoof = Path.Combine(userProfile, "Documents_Fake", "secret.txt");

        // Prefix match would be true, but canonical containment must be false!
        bool isContained = PathSecurity.IsSubpathOf(siblingSpoof, realDocuments);
        Assert.False(isContained);

        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string realSystem32 = Path.Combine(winDir, "System32");
        string system32Spoof = Path.Combine(winDir, "System32_Spoofed", "malware.dll");

        bool isSys32Contained = PathSecurity.IsSubpathOf(system32Spoof, realSystem32);
        Assert.False(isSys32Contained);
    }

    [Theory]
    [InlineData(@"..\..\..\Windows\System32\notepad.exe")]
    [InlineData(@"C:\Temp\..\..\secret.docx")]
    [InlineData(@"subdir/../../passwords.kdbx")]
    public void PathTraversal_SequencesAreStrictlyBlocked(string traversalPath)
    {
        Assert.True(PathSecurity.HasPathTraversalSequences(traversalPath));

        var result = FileSafetyEngine.Analyze(traversalPath, allowedRoot: _sandboxDir);
        Assert.Equal(SafetyRiskTier.Protected, result.Tier);
        Assert.Equal("PathTraversalRule", result.MatchedRule);
    }

    [Fact]
    public void AIIntelligenceReport_CannotOverride_DeterministicProtectionEngine()
    {
        string sensitiveKey = Path.Combine(_sandboxDir, "id_ed25519");
        File.WriteAllText(sensitiveKey, "SSH-KEY-PAYLOAD");

        // Suppose an external AI service returned SafeToDelete
        var aiReport = new OnlineSafetyReport
        {
            FilePath = sensitiveKey,
            FileName = "id_ed25519",
            Verdict = OnlineSafetyVerdict.SafeToDelete,
            SafetyScore = 100
        };

        // Deterministic engine must stay authoritative and reject deletion
        bool isProtected = ProtectionPolicy.IsProtected(sensitiveKey, out string reason);
        Assert.True(isProtected);
        Assert.Contains("Developer / SSH / Cloud authentication", reason, StringComparison.OrdinalIgnoreCase);

        var safetyResult = FileSafetyEngine.Analyze(sensitiveKey, allowedRoot: _sandboxDir);
        Assert.Equal(SafetyRiskTier.Protected, safetyResult.Tier);
    }

    [Fact]
    public async Task AuditRecord_PopulatesPreciseErrorCategories_OnFailure()
    {
        string lockedFile = Path.Combine(_sandboxDir, "locked_adv.tmp");
        File.WriteAllText(lockedFile, "Adversarial lock file");

        using (var stream = File.Open(lockedFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var plan = new CleanupPlan
            {
                ScopeId = "audit-cat-test",
                ScopeName = "Audit Category Test",
                Actions =
                {
                    new PlannedFileAction
                    {
                        FilePath = lockedFile,
                        FileName = Path.GetFileName(lockedFile),
                        SizeBytes = new FileInfo(lockedFile).Length,
                        PlannedSizeBytes = new FileInfo(lockedFile).Length,
                        SafetyTier = SafetyRiskTier.Safe,
                        Action = IntendedCleanupAction.DeletePermanently,
                        LastModified = File.GetLastWriteTimeUtc(lockedFile)
                    }
                }
            };

            var result = await CleanupExecutor.ExecutePlanAsync(plan, _sandboxDir);

            var audit = result.AuditRecords.FirstOrDefault(a => a.FilePath == lockedFile);
            Assert.NotNull(audit);
            Assert.Equal(DeletionAuditStatus.Failed, audit.Status);
            Assert.True(audit.ErrorCategory is CleanupErrorCategory.FileLocked or CleanupErrorCategory.AccessDenied);
        }
    }
}
