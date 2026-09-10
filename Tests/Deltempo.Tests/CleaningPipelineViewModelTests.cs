using WinTempCleaner.Models;
using WinTempCleaner.ViewModels;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Tests for the cleaning pipeline presentation logic extracted from
/// MainWindow code-behind (MVVM phase 1): selection aggregation,
/// confirmation preview copy, and elevation requirements.
/// </summary>
public class CleaningPipelineViewModelTests
{
    private static TargetFolderInfo MakeTarget(
        string name,
        long sizeBytes = 0,
        int fileCount = 0,
        bool isSelected = true,
        bool requiresAdmin = false,
        bool hasAccess = true,
        string safetyBadge = "✓ SAFE • Cache",
        bool isOrphanedAppFolder = false,
        string category = "General")
        => new()
        {
            Id = name,
            Name = name,
            SizeBytes = sizeBytes,
            FileCount = fileCount,
            IsSelected = isSelected,
            RequiresAdmin = requiresAdmin,
            HasAccess = hasAccess,
            SafetyBadge = safetyBadge,
            IsOrphanedAppFolder = isOrphanedAppFolder,
            Category = category
        };

    [Fact]
    public void ComputeSelectionSummary_NothingSelected_ReturnsZeroState()
    {
        var targets = new[]
        {
            MakeTarget("User Temp", isSelected: false),
            MakeTarget("Windows System Temp", isSelected: false)
        };

        var summary = CleaningPipelineViewModel.ComputeSelectionSummary(targets);

        Assert.Equal(0L, summary.SelectedBytes);
        Assert.Equal(0L, summary.SelectedFiles);
        Assert.Equal(0, summary.SelectedCategoryCount);
        Assert.Equal("0.0 B", summary.HeroSizeText);
        Assert.Equal("Clean Selected (0.0 B)", summary.CleanButtonText);
        Assert.Equal("Selected categories are clean or ready for scan", summary.HeroSubtext);
    }

    [Fact]
    public void ComputeSelectionSummary_MixedSelection_AggregatesBytesFilesAndBadgeClasses()
    {
        var targets = new[]
        {
            MakeTarget("User Temp", sizeBytes: 100, fileCount: 10),
            MakeTarget("Prefetch", sizeBytes: 50, fileCount: 5, safetyBadge: "⚠ REVIEW REQUIRED"),
            MakeTarget("Defender", sizeBytes: 999, fileCount: 99, isSelected: false)
        };

        var summary = CleaningPipelineViewModel.ComputeSelectionSummary(targets);

        Assert.Equal(150L, summary.SelectedBytes);
        Assert.Equal(15L, summary.SelectedFiles);
        Assert.Equal(2, summary.SelectedCategoryCount);
        Assert.Equal(1, summary.SafeCategories);
        Assert.Equal(1, summary.ReviewCategories);
        Assert.Equal("Clean Selected (150.0 B)", summary.CleanButtonText);
        Assert.Contains("15 junk items selected across 2 categories", summary.HeroSubtext);
        Assert.Contains("(1 safe, 1 review required)", summary.HeroSubtext);
    }

    [Fact]
    public void ComputeSelectionSummary_AllSafe_ShowsVerifiedSafeBreakdown()
    {
        var targets = new[] { MakeTarget("User Temp", sizeBytes: 2048, fileCount: 4) };

        var summary = CleaningPipelineViewModel.ComputeSelectionSummary(targets);

        Assert.Equal("2.0 KB", summary.HeroSizeText);
        Assert.EndsWith("(100% verified safe)", summary.HeroSubtext);
    }

    [Fact]
    public void BuildConfirmationPreview_TruncatesCategoryListAfterFive()
    {
        var selected = Enumerable.Range(1, 7)
            .Select(i => MakeTarget($"Category {i}", fileCount: 1))
            .ToList();

        var preview = CleaningPipelineViewModel.BuildConfirmationPreview(selected, safeMode: true, recycleBin: true);

        Assert.Contains("Category 1, Category 2, Category 3, Category 4, Category 5 and 2 more", preview.CategorySummary);
        Assert.DoesNotContain("Category 6", preview.CategorySummary);
        Assert.Equal(7L, preview.EstimatedFileCount);
    }

    [Theory]
    [InlineData(true, true, "Safety Shield: Active", "Recycle Bin Protection: Active")]
    [InlineData(true, false, "Safety Shield: Active", "Permanent Deletion: Active")]
    [InlineData(false, true, "Safety Shield: Disabled", "Recycle Bin Protection: Active")]
    [InlineData(false, false, "Safety Shield: Disabled", "Permanent Deletion: Active")]
    public void BuildConfirmationPreview_ReflectsShieldAndDeletionModes(
        bool safeMode, bool recycleBin, string expectedShield, string expectedDeletionTitle)
    {
        var selected = new[] { MakeTarget("User Temp", sizeBytes: 4096, fileCount: 8) };

        var preview = CleaningPipelineViewModel.BuildConfirmationPreview(selected, safeMode, recycleBin);

        Assert.Equal(expectedShield, preview.ShieldText);
        Assert.Equal(safeMode, preview.IsShieldActive);
        Assert.Equal(recycleBin, preview.IsRecycleBinMode);
        Assert.Equal(expectedDeletionTitle, preview.DeletionModeTitle);
        Assert.Equal("4.0 KB", preview.EstimatedSizeText);
        Assert.Contains("System integrity, user credentials, and personal files remain 100% protected.", preview.CategorySummary);
    }

    [Fact]
    public void GetElevationRequirement_InaccessibleAdminTargets_RequireElevation()
    {
        var selected = new[]
        {
            MakeTarget("User Temp"),
            MakeTarget("Windows System Temp", requiresAdmin: true, hasAccess: false),
            MakeTarget("Windows Prefetch Cache", requiresAdmin: true, hasAccess: false)
        };

        var requirement = CleaningPipelineViewModel.GetElevationRequirement(selected);

        Assert.True(requirement.RequiresElevation);
        Assert.Equal(2, requirement.BlockedCategoryNames.Count);
        Assert.Contains("Windows System Temp", requirement.BlockedCategoryNames);
        Assert.Contains("Windows Prefetch Cache", requirement.BlockedCategoryNames);
    }

    [Fact]
    public void GetElevationRequirement_AccessibleOrUserTargets_DoNotRequireElevation()
    {
        var elevatedSession = new[]
        {
            MakeTarget("User Temp"),
            MakeTarget("Windows System Temp", requiresAdmin: true, hasAccess: true)
        };
        Assert.False(CleaningPipelineViewModel.GetElevationRequirement(elevatedSession).RequiresElevation);

        var standardSession = new[] { MakeTarget("User Temp", requiresAdmin: false, hasAccess: true) };
        Assert.False(CleaningPipelineViewModel.GetElevationRequirement(standardSession).RequiresElevation);
    }

    [Theory]
    [InlineData("ALL")]
    [InlineData(null)]
    [InlineData("UNKNOWN_CHIP")]
    public void MatchesFilter_AllOrUnknownTag_PassesEverything(string? tag)
    {
        var target = MakeTarget("Anything", safetyBadge: "⚠ REVIEW REQUIRED", isOrphanedAppFolder: true);

        Assert.True(CleaningPipelineViewModel.MatchesFilter(target, tag, "   "));
    }

    [Fact]
    public void MatchesFilter_SafeChip_ExcludesNonSafeModeEligibleTargets()
    {
        var eligible = MakeTarget("User Temp");
        var notEligible = MakeTarget("Special Shell Target");
        notEligible.IsSafeModeEligible = false;

        Assert.True(CleaningPipelineViewModel.MatchesFilter(eligible, "SAFE", ""));
        Assert.False(CleaningPipelineViewModel.MatchesFilter(notEligible, "SAFE", ""));
    }

    [Fact]
    public void MatchesFilter_SystemChip_KeywordMatching()
    {
        var matching = new[]
        {
            MakeTarget("Windows System Temp", category: "System & GPU"),
            MakeTarget("Device Drivers", category: "System & Drivers"),
            MakeTarget("Defender", category: "Security & Logs"),
            MakeTarget("Delivery Optimization", category: "Storage & Safety"),
        };

        Assert.All(matching, t => Assert.True(CleaningPipelineViewModel.MatchesFilter(t, "SYSTEM", "")));

        var excluded = MakeTarget("User Temp", category: "User Cache");
        Assert.False(CleaningPipelineViewModel.MatchesFilter(excluded, "SYSTEM", ""));
    }

    [Fact]
    public void MatchesFilter_GamingChip_MatchesGamingShaderGpuCategories()
    {
        Assert.True(CleaningPipelineViewModel.MatchesFilter(MakeTarget("DirectX", category: "Gaming & Shaders"), "GAMING", ""));
        Assert.True(CleaningPipelineViewModel.MatchesFilter(MakeTarget("NVIDIA", category: "GPU Shaders"), "GAMING", ""));
        Assert.False(CleaningPipelineViewModel.MatchesFilter(MakeTarget("User Temp", category: "User Cache"), "GAMING", ""));
    }

    [Fact]
    public void MatchesFilter_MediaChip_MatchesMediaAppBrowserStoreDevUserCreatorKeywords()
    {
        Assert.True(CleaningPipelineViewModel.MatchesFilter(MakeTarget("Thumbnails", category: "Media Cache"), "MEDIA", ""));
        Assert.True(CleaningPipelineViewModel.MatchesFilter(MakeTarget("Browsers", category: "Browser Cache"), "MEDIA", ""));
        Assert.True(CleaningPipelineViewModel.MatchesFilter(MakeTarget("Temp", category: "User Cache"), "MEDIA", ""));
        Assert.False(CleaningPipelineViewModel.MatchesFilter(MakeTarget("System Temp", category: "System & GPU"), "MEDIA", ""));
    }

    [Fact]
    public void MatchesFilter_Search_MatchesNameDescriptionCategoryOrPathCaseInsensitive()
    {
        var byName = MakeTarget("Prefetch Cache");
        var byDescription = MakeTarget("Delivery Optimization");
        byDescription.Description = "P2P Windows update delivery chunks";
        var byCategory = MakeTarget("Defender", category: "Security & Logs");
        var byPath = MakeTarget("Explorer Thumbnails");
        byPath.FolderPath = @"C:\Users\bhany\AppData\Local\Microsoft\Windows\Explorer";

        Assert.True(CleaningPipelineViewModel.MatchesFilter(byName, "ALL", "PREFETCH"));
        Assert.True(CleaningPipelineViewModel.MatchesFilter(byDescription, "ALL", "delivery chunks"));
        Assert.True(CleaningPipelineViewModel.MatchesFilter(byCategory, "ALL", "security"));
        Assert.True(CleaningPipelineViewModel.MatchesFilter(byPath, "ALL", "windows\\explorer"));
    }

    [Fact]
    public void MatchesFilter_SearchNoMatch_ReturnsFalse()
    {
        var target = MakeTarget("User Temp", category: "User Cache");

        Assert.False(CleaningPipelineViewModel.MatchesFilter(target, "ALL", "zzz-no-match"));
    }

    [Fact]
    public void MatchesFilter_FilterAndSearch_CombinedBothMustPass()
    {
        // Category passes the SYSTEM chip, but the search term does not match.
        var target = MakeTarget("Windows System Temp", category: "System & GPU");

        Assert.True(CleaningPipelineViewModel.MatchesFilter(target, "SYSTEM", "system"));
        Assert.False(CleaningPipelineViewModel.MatchesFilter(target, "SYSTEM", "prefetch"));
    }
}
