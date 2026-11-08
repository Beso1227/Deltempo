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

        // The package MUST declare an architecture. An omitted
        // ProcessorArchitecture means "neutral", which would offer an x64-only
        // self-contained payload to ARM64 devices. The pack script overrides
        // this per build; the template's value is the default.
        Assert.Equal("x64", (string)identity.Attribute("ProcessorArchitecture")!);

        // The Store listing name is the product, not the identity, and it is no
        // longer a preview: "Deltempo (WinUI Preview)" would ship to customers.
        XElement properties = doc.Descendants().First(e => e.Name.LocalName == "Properties");
        XElement propertiesDisplayName = properties.Elements().First(e => e.Name.LocalName == "DisplayName");
        Assert.Equal("Deltempo", propertiesDisplayName.Value);

        XElement visualElements = doc.Descendants().First(e => e.Name.LocalName == "VisualElements");
        Assert.Equal("Deltempo", (string)visualElements.Attribute("DisplayName")!);

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

        // CI can only prove the pipeline before the Partner Center account
        // exists, so its placeholder build must be requested explicitly.
        string ci = Read(".github", "workflows", "ci.yml");
        Assert.Contains("-AllowPlaceholder", ci);
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

    [Fact]
    public void BuildStorePackage_RewritesIdentityOnTheStagedCopyOnly()
    {
        // Identity is a build input, not repo state. The committed manifest
        // keeps its placeholders so a stale real identity can never be
        // mistaken for the current one; only the staged AppxManifest.xml is
        // rewritten, and the version comes from the same AssemblyVersion the
        // shell's footer shows.
        string script = Read("tools", "build-store-package.ps1");

        Assert.Contains("$stagedManifest", script);
        Assert.Contains("SetAttributeValue(\"Name\", $IdentityName)", script);
        Assert.Contains("SetAttributeValue(\"Publisher\", $Publisher)", script);
        Assert.Contains("SetAttributeValue(\"Version\", $version)", script);
        Assert.Contains("SetAttributeValue(\"ProcessorArchitecture\", $Architecture)", script);
        Assert.Contains("Directory.Build.props", script);
        Assert.Contains("<AssemblyVersion>", script);

        // The template is copied to staging and then rewritten - it is never
        // the save target.
        Assert.Contains("(Join-Path $staging \"AppxManifest.xml\")", script);
        Assert.Contains("$doc.Save($stagedManifest)", script);
        Assert.DoesNotContain("$doc.Save((Join-Path $repoRoot", script);
    }

    [Fact]
    public void BuildStorePackage_RefusesAPlaceholderIdentityUnlessAllowed()
    {
        // A green build that silently packages the placeholder identity is
        // exactly the artifact that must never reach Partner Center, so the
        // escape hatch has to be explicit.
        string script = Read("tools", "build-store-package.ps1");

        Assert.Contains("[switch]$AllowPlaceholder", script);
        Assert.Contains("Refusing to pack a placeholder identity", script);
        Assert.Contains("CN=Placeholder-Publisher", script);
        Assert.Contains("only for a CI dry run", script);
        Assert.Contains("starts with 'CN='", script);
    }

    [Fact]
    public void BuildStorePackage_NamesTheOutputAfterIdentityVersionAndArchitecture()
    {
        // The old literal "_x64.msix" said x64 while the manifest declared the
        // package neutral - the architecture was cosmetic. Both now follow the
        // same input.
        string script = Read("tools", "build-store-package.ps1");

        Assert.Contains("\"{0}_{1}_{2}.msix\" -f $IdentityName, $version, $Architecture", script);
        Assert.DoesNotContain("_x64.msix", script);
    }

    [Fact]
    public void InstallTest_SignsWithATestCertificateAndRemovesThePackage()
    {
        // The packaging pipeline cannot prove the package installs: unsigned
        // MSIX does not sideload. tools/test-store-package.ps1 closes that gap
        // with a self-signed cert trusted in TrustedPeople, and it must clean
        // up after itself.
        string script = Read("tools", "test-store-package.ps1");

        Assert.Contains("signtool", script);
        Assert.Contains("TrustedPeople", script);
        Assert.Contains("Add-AppxPackage", script);
        Assert.Contains("Remove-AppxPackage", script);
        Assert.Contains("-KeepInstalled", script);

        // Trust must be LocalMachine\TrustedPeople: CurrentUser was verified
        // insufficient on this machine (0x800B0109), so a per-user fallback
        // would only produce a confusing deployment error.
        Assert.Contains("Assert-InstallTrust", script);
        Assert.Contains("StoreLocation]::LocalMachine", script);
        Assert.Contains("Test-IsElevated", script);
        Assert.Contains("Import-Certificate", script);
        Assert.Contains("0x800B0109", script);
        // The gate runs AFTER signing, because the command it prints names the
        // signed .msix the user has to install.
        Assert.True(
            script.IndexOf("Assert-InstallTrust -Certificate", StringComparison.Ordinal) >
            script.IndexOf("$signtool sign", StringComparison.Ordinal),
            "The trust gate must run after signtool sign so its message can name the signed package.");

        // A self-signed cert can carry any subject, which is what lets the
        // same script test the real Partner Center identity later.
        Assert.Contains("self-signed cert can carry ANY subject", script);

        // The test artifact must never be uploaded.
        Assert.Contains("Never upload it", script);
        Assert.Contains("DeltempoStoreTest", script);
    }

    [Fact]
    public void StoreFlavor_DoesNotTellCustomersThePackageIsUnsigned()
    {
        // The Store re-signs every package with a Microsoft certificate, so the
        // About screen's "this executable is not yet code-signed" paragraph is
        // false in the Store channel - and it would ship in a listing
        // screenshot. The portable flavor keeps it; the Store flavor compiles
        // it out behind DELTEMPO_MSIX.
        string csproj = Read("src", "Deltempo.App.WinUI", "Deltempo.App.WinUI.csproj");
        Assert.Contains("$(DefineConstants);DELTEMPO_MSIX", csproj);

        string xaml = Read("src", "Deltempo.App.WinUI", "Views", "AboutWorkspaceView.xaml");
        Assert.Contains("x:Name=\"SmartScreenNote\"", xaml);
        Assert.Contains("not yet code-signed", xaml);

        string codeBehind = Read("src", "Deltempo.App.WinUI", "Views", "AboutWorkspaceView.xaml.cs");
        Assert.Contains("#if DELTEMPO_MSIX", codeBehind);
        Assert.Contains("SmartScreenNote.Visibility = Visibility.Collapsed", codeBehind);
    }

    [Fact]
    public void StoreScreenshots_AreCapturedFromTheStoreFlavorAtListingSize()
    {
        // Partner Center needs at least one 1366x768 screenshot of the real app.
        // The capture tool is in the repo (not scratch) so the asset set is
        // reproducible, and it launches the Store flavor because that build is
        // the one whose About screen is truthful for the Store.
        string script = Read("tools", "capture-store-screenshots.ps1");

        Assert.Contains("publish\\Deltempo.WinUI.exe", script);
        Assert.Contains("-p:DeltempoMSIX=true", script);
        Assert.Contains("1366", script);
        Assert.Contains("768", script);
        Assert.Contains("CopyFromScreen", script);

        // Repo-relative resolution only: an absolute path would break the tool
        // on any other machine.
        Assert.DoesNotContain("D:\\Projects", script);
        Assert.Contains("(Resolve-Path (Join-Path $PSScriptRoot \"..\"))", script);

        // The chosen views must stay free of local machine inventory.
        Assert.Contains("01-cleaner", script);
        Assert.Contains("02-repair", script);
        Assert.Contains("03-settings", script);
        Assert.Contains("04-about", script);
    }
}
