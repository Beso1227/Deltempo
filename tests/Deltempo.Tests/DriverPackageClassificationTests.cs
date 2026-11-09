using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Pins the driver-package classification contract. The rule that matters is the one
/// that protects hardware: anything currently bound is untouchable, and an unbound
/// package whose device is merely disconnected stays installed.
/// </summary>
public class DriverPackageClassificationTests
{
    private static SupersededDriverPackage Package(
        string published,
        string original,
        string version,
        string date = "2026-01-01")
        => new()
        {
            PublishedName = published,
            OriginalName = original,
            DriverVersion = $"{date} {version}",
            DriverDate = DateTime.Parse(date, System.Globalization.CultureInfo.InvariantCulture),
            ParsedVersion = Version.Parse(version)
        };

    private static HashSet<string> Set(params string[] values)
        => new(values, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void ClassifyDriverPackages_ActiveBinding_IsBoundEvenWhenOlder()
    {
        var packages = new[]
        {
            Package("oem1.inf", "nvami.inf", "32.0.16.2114"),
            Package("oem2.inf", "nvami.inf", "31.0.15.1000")
        };

        var classified = WindowsDriverMaintenanceService.ClassifyDriverPackages(
            packages,
            activeInfs: Set("oem2.inf"),
            installedInfs: Set("oem1.inf", "oem2.inf"));

        Assert.Equal(DriverPackageState.Bound, classified.Single(p => p.PublishedName == "oem2.inf").State);
        // The newer package is unbound but installed, so it is retained too.
        Assert.Equal(DriverPackageState.Installed, classified.Single(p => p.PublishedName == "oem1.inf").State);
    }

    [Fact]
    public void ClassifyDriverPackages_OlderDuplicate_IsSuperseded()
    {
        var packages = new[]
        {
            Package("oem10.inf", "netwtw08.inf", "24.80.0.2"),
            Package("oem11.inf", "netwtw08.inf", "24.70.0.3")
        };

        var classified = WindowsDriverMaintenanceService.ClassifyDriverPackages(
            packages,
            activeInfs: Set("oem10.inf"),
            installedInfs: Set("oem10.inf"));

        var older = classified.Single(p => p.PublishedName == "oem11.inf");
        Assert.Equal(DriverPackageState.Superseded, older.State);
    }

    [Fact]
    public void ClassifyDriverPackages_UnboundInstalledPackage_IsRetained()
    {
        // A laptop dock driver whose dock is not plugged in: unbound today, required
        // tomorrow. Deleting it would force a re-download or leave the device unusable.
        var packages = new[]
        {
            Package("oem77.inf", "ibtusb.inf", "23.160.0.9")
        };

        var classified = WindowsDriverMaintenanceService.ClassifyDriverPackages(
            packages,
            activeInfs: Set(),
            installedInfs: Set("oem77.inf"));

        Assert.Equal(DriverPackageState.Installed, classified[0].State);
    }

    [Fact]
    public void ClassifyDriverPackages_PublishedWithoutInstalledInf_IsStaged()
    {
        var packages = new[]
        {
            Package("oem90.inf", "rogkb.inf", "1.0.47.0")
        };

        var classified = WindowsDriverMaintenanceService.ClassifyDriverPackages(
            packages,
            activeInfs: Set(),
            installedInfs: Set());

        Assert.Equal(DriverPackageState.Staged, classified[0].State);
    }

    [Fact]
    public void ClassifyDriverPackages_NewestUnboundInstalledKeepIsNotReclaimable()
    {
        var packages = new[]
        {
            Package("oem20.inf", "iigd_dch.inf", "32.0.101.7000"),
            Package("oem21.inf", "iigd_dch.inf", "32.0.101.6790"),
            Package("oem22.inf", "iigd_dch.inf", "31.0.101.5000")
        };

        var classified = WindowsDriverMaintenanceService.ClassifyDriverPackages(
            packages,
            activeInfs: Set(),
            installedInfs: Set("oem20.inf"));

        Assert.Equal(DriverPackageState.Installed, classified.Single(p => p.PublishedName == "oem20.inf").State);
        Assert.Equal(DriverPackageState.Superseded, classified.Single(p => p.PublishedName == "oem21.inf").State);
        Assert.Equal(DriverPackageState.Superseded, classified.Single(p => p.PublishedName == "oem22.inf").State);
    }
}
