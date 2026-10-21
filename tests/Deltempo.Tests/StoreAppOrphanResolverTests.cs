using System;
using System.Collections.Generic;
using System.IO;
using WinTempCleaner.Services.Providers.CacheResolvers;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// W1 coverage: orphaned Microsoft Store app data detection.
///
/// The parse tests exist because the folder name and the repository key name are NOT equal
/// strings ("aimgr_8wekyb3d8bbwe" vs "aimgr_0.20.62.0_x64__8wekyb3d8bbwe"). A naive equality
/// test marks every installed Store app as an orphan, which would delete live app data.
/// </summary>
public class StoreAppOrphanResolverTests
{
    // ---------------------------------------------------------- name parsing

    [Theory]
    [InlineData("aimgr_8wekyb3d8bbwe", "aimgr", "8wekyb3d8bbwe")]
    [InlineData("5319275A.WhatsAppDesktop_cv1g1gvanyjgm", "5319275A.WhatsAppDesktop", "cv1g1gvanyjgm")]
    [InlineData("AppUp.IntelArcSoftware_8j3eq9eme6ctt", "AppUp.IntelArcSoftware", "8j3eq9eme6ctt")]
    public void TrySplitPackageFolderName_SplitsOnFinalUnderscore(
        string folderName, string expectedName, string expectedPublisher)
    {
        bool ok = StoreAppOrphanResolver.TrySplitPackageFolderName(folderName, out string name, out string publisherId);

        Assert.True(ok, $"'{folderName}' should parse as a package folder.");
        Assert.Equal(expectedName, name);
        Assert.Equal(expectedPublisher, publisherId);
    }

    /// <summary>
    /// Load-bearing negatives. Each of these is a REAL folder under %LOCALAPPDATA%\Packages on
    /// at least one supported machine, and each must never be treated as a package folder - a
    /// false "orphan" here means proposing deletion of live system/app data.
    /// </summary>
    [Theory]
    [InlineData("ActiveSync")]                    // legacy folder, no publisher id
    [InlineData("windows_ie_ac_001")]             // IE ActiveX lock folder; "001" is not a publisher id
    [InlineData("Some.App")]                      // no underscore at all
    [InlineData("")]                              // empty
    [InlineData("_8wekyb3d8bbwe")]                // no name
    [InlineData("aimgr_")]                       // no publisher id
    public void TrySplitPackageFolderName_NonPackageNames_AreRejected(string folderName)
    {
        Assert.False(StoreAppOrphanResolver.TrySplitPackageFolderName(folderName, out _, out _));
    }

    [Theory]
    [InlineData("8wekyb3d8bbwe", true)]   // 13 chars, the normal width
    [InlineData("cv1g1gvanyjgm", true)]
    [InlineData("001", false)]            // too short - this is what excluded windows_ie_ac_001
    [InlineData("has-dash-inside", false)] // publisher ids are alphanumeric only
    public void IsValidPublisherId_EnforcesShape(string candidate, bool expected)
    {
        Assert.Equal(expected, StoreAppOrphanResolver.IsValidPublisherId(candidate));
    }

    // ---------------------------------------------------------- registry matching

    [Fact]
    public void IsRegistered_MatchesDespiteExtraVersionSegments()
    {
        // Repository key: aimgr_0.20.62.0_x64__8wekyb3d8bbwe -> prefix "aimgr_0.20.62.0_x64__"
        var registered = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["8wekyb3d8bbwe"] = new List<string> { "aimgr_0.20.62.0_x64__" }
        };

        bool isRegistered = StoreAppOrphanResolver.IsRegistered("8wekyb3d8bbwe", "aimgr", registered);

        Assert.True(isRegistered,
            "A registered package must not be reported as an orphan just because the folder " +
            "name omits the version/arch segments.");
    }

    [Fact]
    public void IsRegistered_DifferentPackageSamePublisher_IsNotRegistered()
    {
        var registered = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["8wekyb3d8bbwe"] = new List<string> { "SomeOther.App_1.0_x64__" }
        };

        Assert.False(StoreAppOrphanResolver.IsRegistered("8wekyb3d8bbwe", "aimgr", registered));
    }

    [Fact]
    public void IsRegistered_UnknownPublisher_IsNotRegistered()
    {
        var registered = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        Assert.False(StoreAppOrphanResolver.IsRegistered("8wekyb3d8bbwe", "aimgr", registered));
    }

    [Fact]
    public void ReadRegisteredPackages_SucceedsOnThisMachine()
    {
        var registered = StoreAppOrphanResolver.ReadRegisteredPackages(out bool readable);

        // If the repository is unreadable the resolver must fail closed, so this is the
        // precondition for the feature working at all on a normal Windows install.
        Assert.True(readable,
            "The AppModel package repository must be readable, otherwise no orphan can be proven.");
        Assert.NotEmpty(registered);
    }

    /// <summary>
    /// A well-known Store app that ships inbox on Windows 11 must never be proposed. Guards the
    /// end-to-end behaviour on a real machine rather than only the parser in isolation.
    /// </summary>
    [Fact]
    public void Resolve_NeverProposesAWellKnownInboxStoreApp()
    {
        var targets = StoreAppOrphanResolver.Resolve();

        Assert.All(targets, t =>
            Assert.DoesNotContain("WhatsApp", t.Name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Whatever this machine's real state, resolution must not throw and must not produce a
    /// target that is pre-selected or marked safe-eligible.
    /// </summary>
    [Fact]
    public void Resolve_ProducesOnlyOptInLowRiskTargets()
    {
        var targets = StoreAppOrphanResolver.Resolve();

        Assert.All(targets, t =>
        {
            Assert.False(t.IsSelected, "Store orphans must be unchecked by default.");
            Assert.False(t.IsSafeModeEligible, "Store orphans must stay out of 100% safe / smart cleans.");
            Assert.True(t.IsOrphanedAppFolder);
            Assert.True(t.SizeBytes > 0);
        });
    }
}
