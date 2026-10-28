using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Deltempo.App.WinUI.ViewModels;
using WinTempCleaner.Services;
using Xunit;
using StartupItem = WinTempCleaner.Services.StartupItem;

namespace Deltempo.Tests;

/// <summary>
/// The search/AI-everywhere batch: the three search boxes the WPF shell had that
/// the preview's shell sections lacked (Startup, Process, Settings) and the
/// startup AI briefing the WPF modal offered per row ("AI Info") and as
/// "AI Analyze All".
///
/// Pure data, no network: the AI cases go through the same offline-fallback
/// report path StartupIntelligenceTests already exercises, and the search cases
/// are plain string filters. The cards the settings search shows/hides are a
/// visual-tree change, so that surface is guarded as XAML shape instead.
/// </summary>
public class SearchAiParityTests
{
    private static StartupItem NewItem(
        string name,
        string friendly = "",
        string publisher = "Unknown",
        string location = "HKCU",
        string command = "",
        WinTempCleaner.Services.BootImpact impact = WinTempCleaner.Services.BootImpact.Low)
    {
        return new StartupItem
        {
            Name = name,
            FriendlyName = friendly,
            Publisher = publisher,
            Location = location,
            Command = command,
            Impact = impact,
        };
    }

    [Fact]
    public void StartupSearch_FiltersByTitlePublisherLocationAndCommand()
    {
        StartupWorkspaceViewModel vm = new();
        vm.LoadFrom(
        [
            NewItem("firefox.exe", friendly: "Firefox", publisher: "Mozilla", command: @"C:\firefox\firefox.exe"),
            NewItem("steam.exe", friendly: "Steam", publisher: "Valve", command: @"C:\steam\steam.exe"),
            NewItem("thing.exe", location: "HKLM", friendly: "Thing"),
        ]);

        vm.SearchText = "moz";
        Assert.Single(vm.Items);
        Assert.Equal("Firefox", vm.Items[0].DisplayTitle);

        // WPF's OrdinalIgnoreCase.
        vm.SearchText = "VALVE";
        Assert.Single(vm.Items);
        Assert.Equal("Steam", vm.Items[0].DisplayTitle);

        // The location search is how you find "which entry comes from this hiveset".
        vm.SearchText = "All Users";
        Assert.Single(vm.Items);
        Assert.Equal("Thing", vm.Items[0].DisplayTitle);

        // The command lines match too.
        vm.SearchText = @"c:\steam";
        Assert.Single(vm.Items);
        Assert.Equal("Steam", vm.Items[0].DisplayTitle);

        vm.SearchText = string.Empty;
        Assert.Equal(3, vm.Items.Count);
    }

    [Fact]
    public void StartupSearch_CombinesWithTheFilterChips()
    {
        StartupWorkspaceViewModel vm = new();
        vm.LoadFrom(
        [
            NewItem("game.exe", friendly: "Launcher", impact: WinTempCleaner.Services.BootImpact.High),
            NewItem("helper.exe", friendly: "Helper", impact: WinTempCleaner.Services.BootImpact.Low),
        ]);

        vm.SetFilterCommand.Execute(StartupFilter.HighImpact);
        vm.SearchText = "helper";

        // Both gates stay in force: the chip said high-impact, the search said Helper.
        Assert.Empty(vm.Items);

        vm.SearchText = "launcher";
        Assert.Single(vm.Items);
    }

    [Fact]
    public async Task StartupAnalyzeItem_FillsTheRowFromTheOfflineFallback()
    {
        StartupWorkspaceViewModel vm = new();
        StartupItem item = NewItem("wordpad.exe", friendly: "WordPad");
        vm.LoadFrom([item]);

        // Offline fallback, exactly as the service's own tests do: no key, no
        // network. forceOnline is the row button's explicit request, and the
        // service is responsible for choosing the provider; the row can only
        // receive a report.
        await vm.AnalyzeItemCommand.ExecuteAsync(item);

        Assert.True(item.IsAiEnriched, "the row must end enriched, not silently untouched");
        Assert.True(item.IsExpanded, "the WPF row opened its detail card");
        Assert.False(string.IsNullOrWhiteSpace(item.Description));
        Assert.False(string.IsNullOrWhiteSpace(item.AiProviderUsed));
        Assert.Contains(item.DisplayTitle, vm.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartupBatchAnalyze_WalksOnlyNotYetEnrichedRows()
    {
        StartupWorkspaceViewModel vm = new();
        List<StartupItem> items =
        [
            NewItem("batch-one.exe", friendly: "Batch One"),
            NewItem("batch-two.exe", friendly: "Batch Two"),
        ];
        vm.LoadFrom(items);

        await vm.BatchAnalyzeCommand.ExecuteAsync(null);

        Assert.All(items, x => Assert.True(x.IsAiEnriched, "every row should be enriched by the batch walk"));
        Assert.Contains("Batch AI analysis completed for 2 startup items", vm.StatusText, StringComparison.Ordinal);

        // Running again with a fully enriched set is a no-op that reports it.
        await vm.BatchAnalyzeCommand.ExecuteAsync(null);
        Assert.Contains("already analyzed", vm.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void ProcessSearch_FiltersByProcessNameAndTitle()
    {
        ProcessWorkspaceViewModel vm = new();
        vm.LoadFrom(
        [
            new ProcessRow
            {
                ProcessName = "chrome",
                WindowTitle = "Example - Google Chrome",
                ProcessId = 100,
                WorkingSetBytes = 40 * 1024 * 1024,
                IsSafeToClose = true,
            },
            new ProcessRow
            {
                ProcessName = "notepad",
                WindowTitle = "Untitled - Notepad",
                ProcessId = 200,
                WorkingSetBytes = 30 * 1024 * 1024,
                IsSafeToClose = true,
            },
        ]);

        vm.SearchText = "chrome";
        Assert.Single(vm.Processes);

        // Window titles match too, like WPF's DisplayName column.
        vm.SearchText = "Notepad";
        Assert.Single(vm.Processes);
        Assert.Equal(200, vm.Processes[0].ProcessId);

        vm.SearchText = string.Empty;
        Assert.Equal(2, vm.Processes.Count);
    }

    // ---------------------------------------------------------------------
    // XAML shape guards, so the surfaces cannot quietly lose their markup.
    // ---------------------------------------------------------------------

    [Fact]
    public void XamlStartup_CarriesTheSearchAndAiSurfaces()
    {
        string xaml = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.App.WinUI", "Views", "StartupWorkspaceView.xaml"));

        Assert.Contains("BatchAnalyzeCommand", xaml);
        Assert.Contains("AnalyzeItemCommand", xaml);
        Assert.Contains("Search startup apps by name, publisher, location, or command...", xaml);
        Assert.Contains("AI Info", xaml);
        Assert.Contains("AI Analyze All", xaml);
    }

    [Fact]
    public void XamlProcess_CarriesTheSearchSurface()
    {
        string xaml = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.App.WinUI", "Views", "ProcessWorkspaceView.xaml"));

        Assert.Contains("Search processes by name or title", xaml);
    }

    [Fact]
    public void XamlSettings_CarriesTheSearchSurface()
    {
        string xaml = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.App.WinUI", "Views", "SettingsWorkspaceView.xaml"));

        Assert.Contains("Search settings...", xaml);
        Assert.Contains("SettingsSearchBox", xaml);
    }

    [Fact]
    public void WpfCounterparts_StillOwnTheirSearchSurfaces()
    {
        string wpf = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.App.Wpf", "MainWindow.xaml"));

        // The fixture must keep testing against a WPF surface that exists.
        Assert.Contains("StartupSearchBox_TextChanged", wpf);
        Assert.Contains("ProcessSearchBox_TextChanged", wpf);
        Assert.Contains("SettingsSearchBox_TextChanged", File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.App.Wpf", "MainWindow.Settings.cs")));

        // The WPF startup AI actions this batch mirrored must keep existing too.
        string systemManagers = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.App.Wpf", "MainWindow.SystemManagers.cs"));
        Assert.Contains("StartupItemAi_Click", systemManagers);
        Assert.Contains("StartupBatchAi_Click", systemManagers);
        Assert.Contains("StartupIntelligenceService.AnalyzeStartupItemAsync", systemManagers);
    }

    /// <summary>Ascend until the solution file roots the repo.</summary>
    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 12; i++)
        {
            if (File.Exists(Path.Combine(dir, "deltempo.sln"))) return dir;
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }

        throw new InvalidOperationException("could not locate the repository root");
    }
}
