using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Guards the packaging invariants that the release pipeline depends on but that no
/// unit test would otherwise notice: the RuntimeIdentifiers the workflow publishes for,
/// and the MSIX manifest the packager patches.
///
/// These exist because both failure modes are silent - a missing win-arm64 RID makes
/// `dotnet publish -r win-arm64` fail only at release time, and a corrupted
/// MinVersion/MaxVersionTested would ship a package that installs on unsupported Windows.
/// </summary>
public class PackagingConfigurationTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "deltempo.sln")))
            {
                dir = dir.Parent;
            }

            Assert.NotNull(dir);
            return dir!.FullName;
        }
    }

    private static string Read(params string[] relativeParts)
        => File.ReadAllText(Path.Combine(new[] { RepoRoot }.Concat(relativeParts).ToArray()));

    [Theory]
    [InlineData("WinTempCleaner.csproj")]
    [InlineData("Cli/Deltempo.Cli.csproj")]
    public void PublishableProjects_DeclareX64AndArm64RuntimeIdentifiers(string projectRelativePath)
    {
        var content = Read(projectRelativePath.Split('/'));

        Assert.Contains("<RuntimeIdentifiers>", content);
        var line = content
            .Split('\n')
            .Single(l => l.Contains("<RuntimeIdentifiers>", StringComparison.Ordinal));

        Assert.Contains("win-x64", line);
        Assert.Contains("win-arm64", line);
    }

    [Fact]
    public void PublishableProjects_KeepSingleFileCompressionAndReadyToRun()
    {
        // The release pipeline relies on these three settings; dropping any of them would
        // silently produce a multi-file or uncompressed build that breaks the one-liner.
        foreach (var project in new[] { "WinTempCleaner.csproj", "Cli/Deltempo.Cli.csproj" })
        {
            var content = Read(project.Split('/'));

            Assert.Contains("<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>", content);
            Assert.Contains("<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>", content);
            Assert.Contains("<PublishReadyToRun>true</PublishReadyToRun>", content);
        }
    }

    [Fact]
    public void CliProject_DoesNotEnableWpf()
    {
        // UseWPF sets WPF's non-trimmable Illink descriptors, which fails any future
        // PublishAot trial with NETSDK1168. The CLI has no System.Windows.* usage.
        var content = Read("Cli", "Deltempo.Cli.csproj");

        Assert.DoesNotContain("<UseWPF>true</UseWPF>", content);
    }

    [Fact]
    public void CoreProject_StillEnablesWpf()
    {
        // InstalledAppService depends on BitmapSource, so Core must keep UseWPF even
        // though the CLI does not. This test locks the pair so neither drifts.
        var content = Read("Core", "Deltempo.Core.csproj");

        Assert.Contains("<UseWPF>true</UseWPF>", content);
    }

    [Fact]
    public void MsixManifest_DeclaresFullTrustCapabilityAndWindowsVersionFloor()
    {
        var doc = XDocument.Load(Path.Combine(RepoRoot, "packaging", "msix", "AppxManifest.xml"));
        XNamespace apps = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
        XNamespace rescap = "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities";

        var family = doc.Descendants(apps + "TargetDeviceFamily").Single();
        Assert.Equal("Windows.Desktop", family.Attribute("Name")?.Value);

        // A full-trust cleaner cannot run in the AppContainer sandbox; without this the
        // MSIX installs but every protected cleanup fails at runtime.
        Assert.Contains(
            doc.Descendants(rescap + "Capability"),
            c => c.Attribute("Name")?.Value == "runFullTrust");

        // Windows 10 1809 is the declared floor - it must survive any manifest patching.
        Assert.StartsWith("10.0.17763", family.Attribute("MinVersion")?.Value ?? string.Empty);
    }

    [Fact]
    public void WingetManifests_CoverVersionInstallerAndLocale()
    {
        var wingetDir = Path.Combine(RepoRoot, "packaging", "winget");
        Assert.True(Directory.Exists(wingetDir), "packaging/winget is missing; the winget channel has no manifests.");

        var files = Directory.GetFiles(wingetDir).Select(Path.GetFileName).ToList();

        Assert.Contains("Beso1227.Deltempo.yaml", files);
        Assert.Contains("Beso1227.Deltempo.installer.yaml", files);
        Assert.Contains("Beso1227.Deltempo.locale.en-US.yaml", files);

        var installer = Read("packaging", "winget", "Beso1227.Deltempo.installer.yaml");
        var version = Read("packaging", "winget", "Beso1227.Deltempo.yaml");
        var locale = Read("packaging", "winget", "Beso1227.Deltempo.locale.en-US.yaml");

        // Both architectures must be offered, or ARM64 users get nothing from winget.
        Assert.Contains("Architecture: x64", installer);
        Assert.Contains("Architecture: arm64", installer);

        // The three manifests are submitted to winget-pkgs as a set and must agree on version.
        var pinnedVersion = version
            .Split('\n')
            .Single(l => l.StartsWith("PackageVersion:", StringComparison.Ordinal))
            .Split(':')[1]
            .Trim();

        foreach (var (name, content) in new[] { ("installer", installer), ("locale", locale) })
        {
            Assert.Contains($"PackageVersion: {pinnedVersion}", content);
        }
    }
}
