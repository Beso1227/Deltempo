using System;
using System.IO;
using WinTempCleaner.Core.Safety;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// W3 coverage: durable AppData application state must never be classified as a cache.
///
/// Motivation: the per-app resolvers clean well-known cache trees inside AppData correctly,
/// but nothing refused the OTHER AppData subtrees - cloud sync databases, licence data,
/// credential vaults, key stores. Protection for those was implicit, held only by every
/// current resolver happening to be careful. These tests pin it explicitly.
///
/// The near-miss cases matter as much as the positive ones: the markers are separator-anchored,
/// so a folder merely NAMED like a protected one (OneDriveCache) must still be cleanable.
/// </summary>
public class AppDataProtectionPolicyTests
{
    private static string AppData(params string[] segments) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData",
            Path.Combine(segments));

    // ---------------------------------------------------------------- positive

    [Theory]
    [InlineData("Microsoft", "OneDrive", "settings.dat")]
    [InlineData("Microsoft", "OneDrive", "syncroot.db")]
    [InlineData("Microsoft", "Office", "licence.dat")]
    [InlineData("Microsoft", "Credentials", "vault-token.bin")]
    [InlineData("Microsoft", @"Windows\Recent\AutomaticDestinations", "foo.lnk")]
    [InlineData("Microsoft", @"Windows\Recent\CustomDestinations", "bar.lnk")]
    [InlineData("Vault", "keys", "keystore.kdbx")]
    [InlineData("gnupg", "private-keys-v1.d", "secring.gpg")]
    public void IsProtected_DurableAppDataState_IsAlwaysProtected(string a, string b, string file)
    {
        string probe = AppData("Roaming", a, b, file);

        bool isProtected = ProtectionPolicy.IsProtected(probe, out string reason);

        Assert.True(isProtected, $"'{probe}' is durable application state and must be protected.");
        Assert.False(string.IsNullOrWhiteSpace(reason), "A refusal must always carry a reason.");
    }

    /// <summary>
    /// The NEW rule must be the one firing for these, not merely any earlier rule that
    /// happened to cover the same path. Note "ssh" is deliberately absent: it was ALREADY
    /// refused by the pre-existing developer-credential rule, so asserting this reason there
    /// would test the wrong mechanism.
    /// </summary>
    [Theory]
    [InlineData("Microsoft", "OneDrive", "settings.dat")]
    [InlineData("Microsoft", "Office", "licence.dat")]
    [InlineData("Microsoft", "Credentials", "vault-token.bin")]
    [InlineData("Microsoft", @"Windows\Recent\AutomaticDestinations", "foo.lnk")]
    [InlineData("Vault", "keys", "keystore.kdbx")]
    [InlineData("gnupg", "private-keys-v1.d", "secring.gpg")]
    public void IsProtected_DurableAppDataState_RefusedByTheNewDenylistRule(string a, string b, string file)
    {
        string probe = AppData("Roaming", a, b, file);

        bool isProtected = ProtectionPolicy.IsProtected(probe, out string reason);

        Assert.True(isProtected);
        Assert.Contains("Application data", reason);
    }

    /// <summary>
    /// Regression guard for a bug found while writing these tests: the core predicate matched
    /// its markers case-sensitively, so the public wrapper silently returned false for every
    /// real mixed-case path while IsProtected (which pre-lowercases) worked fine. A guard that
    /// fails open is worse than no guard, so pin the wrapper directly.
    /// </summary>
    [Fact]
    public void IsAppDataApplicationState_MixedCasePath_StillDetects() =>
        Assert.True(ProtectionPolicy.IsAppDataApplicationState(
            @"C:\Users\Someone\AppData\Roaming\Microsoft\OneDrive\settings.dat"));

    /// <summary>
    /// The shell hive files sit in the AppData ROOT and the shell writes timestamped siblings
    /// (NTUSER.DAT-1FA2B3C4), so this is prefix-matched rather than an exact name match.
    /// </summary>
    [Theory]
    [InlineData("NTUSER.DAT")]
    [InlineData("NTUSER.DAT-1FA2B3C4")]
    [InlineData("UsrClass.dat")]
    public void IsProtected_AppDataRootShellHives_AreAlwaysProtected(string fileName)
    {
        string probe = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), fileName);

        bool isProtected = ProtectionPolicy.IsProtected(probe, out string reason);

        Assert.True(isProtected, $"'{probe}' is a shell hive file and must be protected.");
        Assert.Contains("Application data", reason);
    }

    [Fact]
    public void IsAppDataApplicationState_DurableState_ReportsTrue()
    {
        string probe = AppData("Roaming", "Microsoft", "OneDrive", "settings.dat");
        Assert.True(ProtectionPolicy.IsAppDataApplicationState(probe));
    }

    // ---------------------------------------------------------------- near-miss

    /// <summary>
    /// The load-bearing negative test. If the markers were matched loosely (Contains on a
    /// bare word rather than a separator-anchored segment), a legitimate cache folder would
    /// become undeletable and the guard would be quietly wrong in the other direction.
    /// </summary>
    [Theory]
    [InlineData("Local", "OneDriveCache", "blob.tmp")]
    [InlineData("Local", "OfficeCache", "entry.tmp")]
    [InlineData("Roaming", "sshd-cache", "sock.tmp")]
    [InlineData("Roaming", "VaultCache", "index.tmp")]
    public void IsProtected_NearMissAppDataNames_AreNotBlockedByTheDenylist(string a, string b, string file)
    {
        string probe = AppData(a, b, file);

        bool isProtected = ProtectionPolicy.IsProtected(probe, out string reason);

        Assert.False(isProtected,
            $"'{probe}' is a cache, not application state, and must remain cleanable. Reason given: {reason}");
    }

    /// <summary>
    /// The denylist is deliberately AppData-scoped. A developer's own source tree named
    /// "vault" outside AppData is not application state and must not be refused.
    /// </summary>
    [Fact]
    public void IsAppDataApplicationState_SameNameOutsideAppData_IsNotBlocked()
    {
        string outside = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Projects", "vault", "secrets.env");

        Assert.False(ProtectionPolicy.IsAppDataApplicationState(outside));
    }

    // ------------------------------------------- directory-unit (recursive delete) guard

    /// <summary>
    /// Directory.Delete(recursive: true) removes a whole subtree in one call, so the per-file
    /// IsProtected check never runs for it. A scope root that merely CONTAINS a protected
    /// subtree must be refused outright - this is the gap W3 closes.
    /// </summary>
    [Fact]
    public void TryGetProtectedDescendant_RootContainingOneDrive_IsRefused()
    {
        string sandbox = CreateSandbox("AppData", "Roaming", "Contoso");
        try
        {
            string protectedChild = Path.Combine(sandbox, "Microsoft", "OneDrive");
            Directory.CreateDirectory(protectedChild);
            File.WriteAllText(Path.Combine(protectedChild, "settings.dat"), "state");

            bool blocked = ProtectionPolicy.TryGetProtectedDescendant(sandbox, out string matched);

            Assert.True(blocked, "A tree containing OneDrive state must not be recursively deleted.");
            Assert.Contains("OneDrive", matched, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SafeDeleteSandbox(sandbox);
        }
    }

    [Fact]
    public void TryGetProtectedDescendant_RootContainingCredentialVault_IsRefused()
    {
        string sandbox = CreateSandbox("AppData", "Roaming", "Contoso");
        try
        {
            string vault = Path.Combine(sandbox, "Vault");
            Directory.CreateDirectory(vault);
            File.WriteAllText(Path.Combine(vault, "keystore.kdbx"), "secret");

            bool blocked = ProtectionPolicy.TryGetProtectedDescendant(sandbox, out _);

            Assert.True(blocked, "A tree containing a credential vault must not be recursively deleted.");
        }
        finally
        {
            SafeDeleteSandbox(sandbox);
        }
    }

    [Fact]
    public void TryGetProtectedDescendant_PureCacheTree_IsAllowed()
    {
        string sandbox = CreateSandbox("AppData", "Local", "Contoso");
        try
        {
            string cache = Path.Combine(sandbox, "Cache", "Code Cache");
            Directory.CreateDirectory(cache);
            File.WriteAllText(Path.Combine(cache, "blob.tmp"), "junk");

            bool blocked = ProtectionPolicy.TryGetProtectedDescendant(sandbox, out _);

            Assert.False(blocked, "A pure cache tree contains no protected state and must stay cleanable.");
        }
        finally
        {
            SafeDeleteSandbox(sandbox);
        }
    }

    /// <summary>
    /// Outside AppData the denylist cannot match, so the walk is skipped entirely rather than
    /// scanning an arbitrary tree. A pure-cache Program Files folder must not be refused.
    /// </summary>
    [Fact]
    public void TryGetProtectedDescendant_OutsideAppData_IsNotScanned()
    {
        string sandbox = Path.Combine(
            Path.GetTempPath(), "DeltempoTests_" + Guid.NewGuid().ToString("N"), "Program Files", "Contoso");
        try
        {
            Directory.CreateDirectory(Path.Combine(sandbox, "Cache"));
            File.WriteAllText(Path.Combine(sandbox, "Cache", "blob.tmp"), "junk");

            bool blocked = ProtectionPolicy.TryGetProtectedDescendant(sandbox, out _);

            Assert.False(blocked, "The AppData-scoped guard must not refuse trees outside AppData.");
        }
        finally
        {
            SafeDeleteSandbox(sandbox);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"C:\does\not\exist\at\all\AppData\Roaming\X")]
    public void TryGetProtectedDescendant_UnusableInput_IsNotBlocked(string path)
    {
        Assert.False(ProtectionPolicy.TryGetProtectedDescendant(path, out string matched));
        Assert.Equal(string.Empty, matched);
    }

    // ---------------------------------------------------------------- helpers

    private static string CreateSandbox(params string[] underAppData)
    {
        string sandbox = Path.Combine(
            Path.GetTempPath(),
            "DeltempoTests_" + Guid.NewGuid().ToString("N"),
            Path.Combine(underAppData));
        Directory.CreateDirectory(sandbox);
        return sandbox;
    }

    /// <summary>
    /// Sandboxes contain no junctions (unlike a real profile), so a plain recursive delete is
    /// safe here - the same rule JunctionSafetyTests enforces for the production path.
    /// </summary>
    private static void SafeDeleteSandbox(string sandbox)
    {
        try
        {
            string? root = Path.GetFullPath(sandbox);
            while (!string.IsNullOrEmpty(root) && !Directory.Exists(root))
            {
                root = Path.GetDirectoryName(root);
            }
            if (string.IsNullOrEmpty(root) || !root.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            Directory.Delete(root, recursive: true);
        }
        catch
        {
            // best effort cleanup
        }
    }
}
