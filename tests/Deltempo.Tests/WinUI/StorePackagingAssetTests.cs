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

        // Full trust is declared the launchable way: Executable + the
        // Windows.FullTrustApplication EntryPoint ON the Application element,
        // backed by the rescap capability (the official pairing). The comment
        // carries the trap: runFullTrust is scope, not elevation.
        Assert.Contains("Executable=\"Deltempo.WinUI.exe\"", manifest);
        Assert.Contains("EntryPoint=\"Windows.FullTrustApplication\"", manifest);
        Assert.Contains("<rescap:Capability Name=\"runFullTrust\" />", manifest);
        Assert.Contains("<Application", manifest);
        Assert.DoesNotContain("Category=\"windows.fullTrustApplication\"", manifest);
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

    [Fact]
    public void PackagingPipeline_ProducesTheMsixWithoutVisualStudio()
    {
        // Section 3's final decision: the package is built by
        // tools/build-store-package.ps1, which packs the Store flavor's
        // publish folder with makeappx.exe resolved from the
        // Microsoft.Windows.SDK.BuildTools NuGet the repo already depends
        // on. No Visual Studio, no .wapproj, no VS-only AppxPackage targets -
        // it runs locally and on CI identically.
        string script = Read("tools", "build-store-package.ps1");

        Assert.Contains("microsoft.windows.sdk.buildtools", script);
        Assert.Contains("makeappx", script);
        // The package manifest's required in-package name.
        Assert.Contains("AppxManifest.xml", script);
        // Fail-closed posture: a makeappx failure must abort, never ship.
        Assert.Contains("makeappx pack failed", script);
        // PDBs are symbols, not runtime payload.
        Assert.Contains("*.pdb", script);
    }

    [Fact]
    public void Publish_StagesTheAppPriOrFailsClosed()
    {
        // dotnet publish leaves the app's own PRI in the build root, and a
        // publish-folder launch then dies with XamlParseException the moment
        // MainWindow.InitializeComponent resolves its compiled XAML
        // (verified 2026-10-30: stowed exception at LoadComponent, clean
        // launch once the PRI was copied in). The csproj must stage it on
        // every publish - fail closed if the build produced none - and the
        // packaging pipeline must refuse a staging folder without it.
        string csproj = Read("src", "Deltempo.App.WinUI", "Deltempo.App.WinUI.csproj");
        string script = Read("tools", "build-store-package.ps1");

        Assert.Contains("StageAppPriIntoPublish", csproj);
        Assert.Contains("$(AssemblyName).pri", csproj);
        Assert.Contains("XamlParseException", csproj);
        Assert.Contains("no Deltempo.WinUI.pri", script);
    }
}
