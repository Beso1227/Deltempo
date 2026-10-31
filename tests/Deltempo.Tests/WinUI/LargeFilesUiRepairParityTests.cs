using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Guards for the round that fixed the "row buttons didn't work" reports.
///
/// Root cause of the dead buttons: WinUI 3 never resolves an ElementName path from
/// inside a DataTemplate - the template is instantiated in its own namescope, where
/// the page's "Root" is invisible, so every per-row
/// Command="{Binding ViewModel.X, ElementName=Root}" binding silently failed to
/// attach (the row COMMANDS never fired; footer x:Bind buttons kept working). That
/// binding pattern had been installed by the previous fix round and pinned by these
/// very tests, which is why the buttons stayed dead anyway. The row actions are now
/// wired through code-behind Click handlers that read the item off the DataContext
/// and execute the ViewModel commands - the same pattern the Cleaner workspace's
/// Inspect button already used - across the Large Files, Startup, App Uninstall and
/// Cleaner row templates.
///
/// The model catalog entries were re-verified against the provider pages in
/// October 2026; a retired default sent every request for a provider to a 404
/// that the offline fallback silently masked, so the verified IDs are pinned.
/// </summary>
public class LargeFilesUiRepairParityTests
{
    // -----------------------------------------------------------------
    // Row buttons: no DataTemplate may drive the page through ElementName.
    // -----------------------------------------------------------------

    [Fact]
    public void LargeFilesRows_ConnectTheViewModelThroughClickHandlers()
    {
        string xaml = ReadView("LargeFilesWorkspaceView.xaml");

        // An ElementName path inside a DataTemplate is never resolved by WinUI 3;
        // pin the dead pattern out and the working Click pattern in.
        Assert.DoesNotContain("DataContext.ViewModel", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ElementName=Root", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnRowRevealClick\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnRowAskAiClick\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnRowRecycleClick\"", xaml, StringComparison.Ordinal);

        // The handlers forward into the commands - the gates (scan in progress,
        // already-analyzing row) keep working from the ViewModel side.
        string code = ReadViewCodeBehind("LargeFilesWorkspaceView.xaml.cs");
        Assert.Contains("ViewModel.RevealCommand.Execute", code, StringComparison.Ordinal);
        Assert.Contains("ViewModel.AnalyzeItemCommand.Execute", code, StringComparison.Ordinal);
        Assert.Contains("ViewModel.RecycleItemCommand.Execute", code, StringComparison.Ordinal);
    }

    [Fact]
    public void StartupRows_ConnectTheViewModelThroughClickHandlers()
    {
        string xaml = ReadView("StartupWorkspaceView.xaml");

        Assert.DoesNotContain("DataContext.ViewModel", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ElementName=Root", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnRowRevealClick\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnRowSearchOnlineClick\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnRowAnalyzeAiClick\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnRowCleanOrphanedClick\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnRowToggleExpandClick\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void AppUninstallRows_ConnectTheViewModelThroughClickHandlers()
    {
        string xaml = ReadView("AppUninstallView.xaml");

        Assert.DoesNotContain("DataContext.ViewModel", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ElementName=Root", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnRowFetchAiClick\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnRowOpenInstallFolderClick\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnRowScanTracesClick\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnRowForceRootCleanClick\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnRowDeepUninstallClick\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnRowToggleExpandClick\"", xaml, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------
    // Filter bar: the two-group card that replaced the overflowing row.
    // -----------------------------------------------------------------

    [Fact]
    public void LargeFilesFilterBar_KeepsTheControlNamesAndHandlersTheCodeBehindDrives()
    {
        string xaml = ReadView("LargeFilesWorkspaceView.xaml");

        // The code-behind populates the chips by name and the handlers carry
        // the selection tags, so the restyle may not rename any of them.
        Assert.Contains("x:Name=\"ScopeChips\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CategoryFilterCombo\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SortModeCombo\"", xaml, StringComparison.Ordinal);
        Assert.Contains("OnSizeChanged", xaml, StringComparison.Ordinal);
        Assert.Contains("OnCategoryFilterChanged", xaml, StringComparison.Ordinal);
        Assert.Contains("OnSortModeChanged", xaml, StringComparison.Ordinal);
        Assert.Contains("OnViewModeChanged", xaml, StringComparison.Ordinal);

        // The card layout that keeps the controls inside the workspace width.
        Assert.Matches(
            @"<Border Grid\.Row=""1""\s*\r?\n\s*Background=""\{ThemeResource SurfaceCardBrush\}""",
            xaml);
        Assert.Contains("GroupName=\"LargeFileSize\"", xaml, StringComparison.Ordinal);
        Assert.Contains("GroupName=\"LargeFileView\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void LargeFilesRows_DisableAskAiWhileTheRowIsAnalyzing()
    {
        string xaml = ReadView("LargeFilesWorkspaceView.xaml");

        // WPF's AskAiLargeFile_Click set btn.IsEnabled = false around the fetch;
        // the WinUI row expresses the same lock through AnalyzeItemCommand's
        // canExecute (item.IsAiAnalyzing), because a local IsEnabled binding
        // overrides the command-driven IsEnabled and left the scan gate inert.
        Assert.Contains(
            "item is not null && !IsLoading && !item.IsAiAnalyzing",
            ReadViewModel(),
            StringComparison.Ordinal);
        Assert.Contains(
            "Visibility=\"{Binding IsAiAnalyzing, Converter={StaticResource BoolToVis}}\"",
            xaml,
            StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------
    // Ask AI survives the scan/analysis race.
    // -----------------------------------------------------------------

    [Fact]
    public void RowAskAi_CanExecute_TracksTheScanGate()
    {
        // The scan rebuilds the item list wholesale on completion, so a verdict
        // applied mid-scan landed on an instance the list threw away and the row
        // silently reverted to UNKNOWN. The row button now follows the same gate
        // the bottom bar's "Analyze with AI" always had.
        var vm = new Deltempo.App.WinUI.ViewModels.LargeFilesWorkspaceViewModel();
        var item = new LargeFileInfo { FilePath = @"C:\temp\game37.arc", FileName = "game37.arc" };

        Assert.True(vm.AnalyzeItemCommand.CanExecute(item));

        vm.IsLoading = true;
        Assert.False(vm.AnalyzeItemCommand.CanExecute(item), "a mid-scan Ask AI click loses its verdict when the scan rebuilds the list");

        vm.IsLoading = false;
        Assert.True(vm.AnalyzeItemCommand.CanExecute(item));

        // A row that is already analyzing disables its own button too - the XAML
        // IsEnabled binding that used to do this overrode the command-driven
        // IsEnabled, which is what let the mid-scan click slip through.
        item.IsAiAnalyzing = true;
        Assert.False(vm.AnalyzeItemCommand.CanExecute(item), "a row that is analyzing must disable its own Ask AI button");

        item.IsAiAnalyzing = false;
        Assert.True(vm.AnalyzeItemCommand.CanExecute(item));

        Assert.False(vm.AnalyzeItemCommand.CanExecute(null));
    }

    [Fact]
    public void RowAskAi_TracksTheBatchLatchToo()
    {
        var vm = new Deltempo.App.WinUI.ViewModels.LargeFilesWorkspaceViewModel();
        var item = new LargeFileInfo { FilePath = @"C:\temp\game37.arc", FileName = "game37.arc" };

        vm.IsBatchAnalyzing = true;
        Assert.False(vm.BatchAnalyzeCommand.CanExecute(null));
    }

    // -----------------------------------------------------------------
    // Settings: the AI surface stays visible and tells the default model.
    // -----------------------------------------------------------------

    [Fact]
    public void SettingsAi_CarriesApiKeyRowProviderRowAndDefaultModelHint()
    {
        string xaml = ReadView("SettingsWorkspaceView.xaml");

        Assert.Contains("AiProviderCombo", xaml, StringComparison.Ordinal);
        Assert.Contains("AiApiKeyBox", xaml, StringComparison.Ordinal);
        Assert.Contains("AiDefaultModelText", xaml, StringComparison.Ordinal);
        Assert.Contains("TestAiConnectionCommand", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_VisibilityAndDefaultModelHint_AreWiredInCode()
    {
        // Deliberately pin the VM surface as source instead of instantiating it:
        // the preview view-model persists through SettingsService, and a test
        // must never write the real settings store.
        string vm = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.App.WinUI", "ViewModels", "SettingsWorkspaceViewModel.cs"));

        Assert.Contains(
            "ShowAiApiKeyRow => AiProvider is \"Gemini\" or \"Groq\" or \"OpenAI\" or \"OpenRouter\"",
            vm,
            StringComparison.Ordinal);
        Assert.Contains("ShowAiOllamaRow => AiProvider == \"Ollama\"", vm, StringComparison.Ordinal);
        Assert.Contains("AiDefaultModelText", vm, StringComparison.Ordinal);
        Assert.Contains("AiModelCatalog.ResolveModelId(AiProvider, userModelName: null)", vm, StringComparison.Ordinal);
        Assert.Contains("OnPropertyChanged(nameof(AiDefaultModelText));", vm, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------
    // Model catalog: verified live at the last check (Oct 2026).
    // -----------------------------------------------------------------

    [Fact]
    public void Catalog_Defaults_AreTheVerifiedLiveModels()
    {
        Assert.Equal("gemini-3.5-flash", AiModelCatalog.ResolveModelId("Gemini", null));
        Assert.Equal("gpt-5.4-mini", AiModelCatalog.ResolveModelId("OpenAI", null));
        Assert.Equal("openai/gpt-oss-120b", AiModelCatalog.ResolveModelId("Groq", null));
        Assert.Equal("meta-llama/llama-3.3-70b-instruct", AiModelCatalog.ResolveModelId("OpenRouter", null));
        Assert.Equal("llama3.2", AiModelCatalog.ResolveModelId("Ollama", null));
    }

    [Fact]
    public void Catalog_Suggestions_CarryNoRetiredId()
    {
        Assert.DoesNotContain(AiModelCatalog.GetSuggestedModels("OpenAI"),
            m => m.Id is "gpt-5-mini" or "gpt-5-nano" or "gpt-5" or "gpt-5.2-mini");
        Assert.DoesNotContain(AiModelCatalog.GetSuggestedModels("Gemini"),
            m => m.Id.Contains("gemini-3.5-pro", StringComparison.Ordinal));
        Assert.Contains(AiModelCatalog.GetSuggestedModels("Gemini"),
            m => m.Id == "gemini-3.8-flash");
        Assert.Contains(AiModelCatalog.GetSuggestedModels("Groq"),
            m => m.Id == "openai/gpt-oss-120b" && m.Note is not null && m.Note.Contains("Default", StringComparison.Ordinal));
    }

    private static string ReadViewModel()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 12; i++)
        {
            string candidate = Path.Combine(dir, "src", "Deltempo.App.WinUI", "ViewModels", "LargeFilesWorkspaceViewModel.cs");
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }

        throw new InvalidOperationException("could not locate LargeFilesWorkspaceViewModel.cs under the repository root");
    }

    private static string ReadView(string name)
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 12; i++)
        {
            string candidate = Path.Combine(dir, "src", "Deltempo.App.WinUI", "Views", name);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }

        throw new InvalidOperationException($"could not locate {name} under the repository root");
    }

    private static string ReadViewCodeBehind(string name)
        => ReadView(name);

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
