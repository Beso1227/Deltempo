using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Store-packaging parity for the WinUI shell's elevation split (the first
/// gate in docs/PUBLISHING_MICROSOFT_STORE.md). The Store flavor must start
/// as the standard user (asInvoker) because Store review rejects
/// auto-elevation, while the preview/portable flavor keeps
/// requireAdministrator so dev behavior matches the shipped WPF head. Both
/// flavors select which manifest at build time; the runtime already carries
/// the per-action elevation path both share with the WPF head.
///
/// Shape-only, like the other shell parity tests.
/// </summary>
public class StoreElevationParityTests
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
    public void StoreFlavor_Manifest_StartsUnelevatedAndKeepsTheShellRules()
    {
        string manifest = Read("src", "Deltempo.App.WinUI", "app.store.manifest");

        // The whole point: Store review rejects requireAdministrator.
        Assert.Contains("requestedExecutionLevel level=\"asInvoker\"", manifest);
        Assert.DoesNotContain("requestedExecutionLevel level=\"requireAdministrator\"", manifest);

        // The DPI / codepage / long-path rules must not drift from app.manifest:
        // they are behavioural, not flavor-specific.
        string dev = Read("src", "Deltempo.App.WinUI", "app.manifest");
        Assert.Equal("UTF-8", ManifestValue(manifest, "activeCodePage"));
        Assert.Equal("true", ManifestValue(manifest, "longPathAware"));
        Assert.Equal("PerMonitorV2, PerMonitor", ManifestValue(manifest, "dpiAwareness"));
        Assert.Equal("http://schemas.microsoft.com/SMI/2016/WindowsSettings",
            ManifestNamespace(manifest, "longPathAware"));

        // Both manifests agree on the elevated framing they came from.
        Assert.Contains("requireAdministrator", dev);
    }

    private static string ManifestNamespace(string manifest, string elementName)
    {
        XElement element = ManifestElement(manifest, elementName);
        Assert.Equal("urn:schemas-microsoft-com:asm.v3", element.Parent!.Name.NamespaceName);
        return element.Name.NamespaceName;
    }

    private static string ManifestValue(string manifest, string elementName) =>
        ManifestElement(manifest, elementName).Value.Trim();

    private static XElement ManifestElement(string manifest, string elementName)
    {
        XDocument doc = XDocument.Parse(manifest);
        return doc.Descendants().First(e => e.Name.LocalName == elementName);
    }

    [Fact]
    public void Build_WiresBothManifestsExclusivelyOnTheStoreFlag()
    {
        string csproj = Read("src", "Deltempo.App.WinUI", "Deltempo.App.WinUI.csproj");

        Assert.Contains("<ApplicationManifest Condition=\"'$(DeltempoMSIX)' == 'true'\">app.store.manifest</ApplicationManifest>", csproj);
        Assert.Contains("<ApplicationManifest Condition=\"'$(DeltempoMSIX)' != 'true'\">app.manifest</ApplicationManifest>", csproj);

        // The plan is the documentation of why the flavors differ.
        Assert.True(File.Exists(Path.Combine(RepoRoot(), "docs", "PUBLISHING_MICROSOFT_STORE.md")));
    }

    [Fact]
    public void Runtime_KeepsThePerActionElevationPathWhileUnelevated()
    {
        // The shared per-task engines both heads reach for; the Store flavor
        // runs unelevated, so these are the only two legal elevation moves.
        string view = Read("src", "Deltempo.App.WinUI", "Views", "SystemRepairView.xaml.cs");
        Assert.Contains("ElevationService.RestartAsAdmin()", view);

        string repair = Read("src", "Deltempo.Core", "Services", "SystemRepairService.cs");
        Assert.Contains("Verb = ElevationService.IsAdministrator ? string.Empty : \"runas\"", repair);

        // The bail-out texts the runtime reports when a surface needs the
        // elevated flavor: the CLI's restore-point JSON error and DISM's
        // return-tuple messages are the contracts users see.
        string cli = Read("src", "Deltempo.Core", "Services", "CliRunner.Cleaning.cs");
        Assert.Contains("Administrator privileges required to query or clean restore points.", cli);
        string dism = Read("src", "Deltempo.Core", "Services", "CleanerService.Dism.cs");
        Assert.Contains("Administrator privileges required to manage restore points.", dism);
    }

    [Fact]
    public void TitleBar_LabelsBothPrivilegeStatesSoUnelevatedUsersAreNeverConfused()
    {
        // The shell's own contract: the title bar must state which flavor is
        // running. When the Store flavor ships, "Standard user" is the honest
        // state and the badge logic must not regress to admin-only wording.
        string shell = Read("src", "Deltempo.App.WinUI", "MainWindow.xaml.cs");
        Assert.Contains("ElevationService.IsAdministrator ? \"Administrator\" : \"Standard user\"", shell);
    }
}
