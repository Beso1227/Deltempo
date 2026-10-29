using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Store packaging asset parity (docs/PUBLISHING_MICROSOFT_STORE.md sections
/// 3 and 4): the identity manifest with its Partner Center placeholders and
/// the generated logo set must exist, must agree with each other, and the
/// logos must never be edited by hand - they come from the WPF head's app.ico
/// through tools/generate-store-logos.ps1, the single source of truth.
/// Shape-only, like the other shell parity tests.
/// </summary>
public class StorePackagingAssetTests
{
    private static string RepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "deltempo.sln")))
        {
            dir = Directory.GetParent(dir)!.FullName;
        }

        return dir;
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot() }.Concat(parts).ToArray()));

    [Fact]
    public void PackageManifest_ExistsWithPartnerCenterPlaceholdersMarkedForCopy()
    {
        string manifest = Read("src", "Deltempo.App.WinUI", "packaging", "Store", "Package.appxmanifest");

        // Placeholders are deliberately visible: the plan's section 1 says to
        // copy the real identity from Partner Center, so a real-looking
        // Publisher here would be a lie.
        XDocument doc = XDocument.Parse(manifest);
        XElement identity = doc.Descendants().First(e => e.Name.LocalName == "Identity");
        Assert.Equal("DeltempoWinUIPreview", (string)identity.Attribute("Name")!);
        Assert.Equal("CN=Placeholder-Publisher", (string)identity.Attribute("Publisher")!);

        // Full trust is scope, not elevation; the comment carries the trap.
        Assert.Contains("windows.fullTrustApplication", manifest);
        Assert.Contains("runFullTrust is scope, not elevation", manifest);
    }

    [Fact]
    public void LogoSet_ExistsAndMatchesTheSizesTheManifestAndSubmissionNeed()
    {
        // Section 4's sizes: 44 (package visual), 50, 71, 150, 256 (submission)
        // plus the 16 tile for file-dialog use.
        foreach (int size in new[] { 16, 44, 50, 71, 150, 256 })
        {
            string path = Path.Combine(RepoRoot(), "src", "Deltempo.App.WinUI", "Assets", "Store", $"Icon_{size}.png");
            Assert.True(File.Exists(path), $"missing Store logo Icon_{size}.png");
            Assert.True(new FileInfo(path).Length > 500, $"Icon_{size}.png looks like a blank stamp ({new FileInfo(path).Length} bytes); regenerate with tools/generate-store-logos.ps1");
        }

        // The manifest references must resolve: Logo paths in the manifest
        // are relative to the project root, so Assets\\Store\\*.png is the
        // only agreed spelling.
        string manifest = Read("src", "Deltempo.App.WinUI", "packaging", "Store", "Package.appxmanifest");
        foreach (string reference in new[] { "Assets\\Store\\Icon_44.png", "Assets\\Store\\Icon_150.png" })
        {
            Assert.Contains(reference, manifest);
            Assert.True(File.Exists(Path.Combine(RepoRoot(), "src", "Deltempo.App.WinUI",
                reference.Replace('\\', Path.DirectorySeparatorChar))),
                $"manifest references {reference} but the file is absent");
        }
    }

    [Fact]
    public void LogoGenerator_IsTheSingleSourceAndReadsTheBrandIco()
    {
        string script = Read("tools", "generate-store-logos.ps1");

        // The generator must read the brand icon and emit the exact set; if
        // someone edits a PNG by hand this chain (script -> PNG -> manifest)
        // loses traceability.
        Assert.Contains("src\\Deltempo.App.Wpf\\app.ico", script);
        Assert.Contains("16, 44, 50, 71, 150, 256", script);
    }
}
