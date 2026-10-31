using System;
using System.IO;
using System.Text.RegularExpressions;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Guards for the round that fixed the "Ask AI didn't work" report and the
/// overflowing Large Files filter bar.
///
/// Root cause of the dead buttons: the app never sets a DataContext, so the
/// per-row commands bound as {Binding DataContext.ViewModel.X, ElementName=Root}
/// resolved through null and never fired (Large Files, Startup and App
/// Uninstall row actions were all dead, not just Ask AI). The working pattern
/// was CleanerWorkspaceView's {Binding ViewModel.X, ElementName=Root}; these
/// tests pin that no dead path can come back.
///
/// The model catalog entries were re-verified against the provider pages in
/// October 2026; a retired default sent every request for a provider to a 404
/// that the offline fallback silently masked, so the verified IDs are pinned.
/// </summary>
public class LargeFilesUiRepairParityTests
{
    // -----------------------------------------------------------------
    // Bindings: no view may path through Drawable DataContext again.
    // -----------------------------------------------------------------

    [Fact]
    public void LargeFilesRows_BindTheViewModelWithoutADeadDataContextPath()
    {
        string xaml = ReadView("LargeFilesWorkspaceView.xaml");

        Assert.DoesNotContain("DataContext.ViewModel", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ViewModel.AnalyzeItemCommand, ElementName=Root}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ViewModel.RevealCommand, ElementName=Root}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ViewModel.RecycleItemCommand, ElementName=Root}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void StartupRows_BindTheViewModelWithoutADeadDataContextPath()
    {
        string xaml = ReadView("StartupWorkspaceView.xaml");

        Assert.DoesNotContain("DataContext.ViewModel", xaml, StringComparison.Ordinal);
        Assert.Contains("ViewModel.AnalyzeItemCommand, ElementName=Root", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void AppUninstallRows_BindTheViewModelWithoutADeadDataContextPath()
    {
        string xaml = ReadView("AppUninstallView.xaml");

        Assert.DoesNotContain("DataContext.ViewModel", xaml, StringComparison.Ordinal);
        Assert.Contains("ViewModel.FetchAiCommand, ElementName=Root", xaml, StringComparison.Ordinal);
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
        // the WinUI row expresses the same lock through the row state.
        Assert.Contains(
            "IsEnabled=\"{Binding IsAiAnalyzing, Converter={StaticResource NotBool}}\"",
            xaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "Visibility=\"{Binding IsAiAnalyzing, Converter={StaticResource BoolToVis}}\"",
            xaml,
            StringComparison.Ordinal);
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
