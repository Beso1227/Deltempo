using WinTempCleaner.Models;

namespace WinTempCleaner.ViewModels;

/// <summary>
/// Presentation logic for the cleaning pipeline, extracted from MainWindow
/// code-behind (MVVM Phase 1) so selection aggregation, confirmation copy,
/// and elevation requirements are unit-testable without a WPF Application
/// context. Phase 2 will surface these results through INotifyPropertyChanged
/// properties instead of code-behind control assignment.
/// </summary>
public static class CleaningPipelineViewModel
{
    /// <summary>
    /// Aggregated hero/button state for the currently selected categories.
    /// </summary>
    public sealed record SelectionSummary(
        long SelectedBytes,
        long SelectedFiles,
        int SafeCategories,
        int ReviewCategories,
        int SelectedCategoryCount,
        string HeroSizeText,
        string CleanButtonText,
        string HeroSubtext);

    /// <summary>
    /// Confirmation modal content derived from the pending cleanup request.
    /// </summary>
    public sealed record ConfirmationPreview(
        string EstimatedSizeText,
        string ShieldText,
        bool IsShieldActive,
        bool IsRecycleBinMode,
        string DeletionModeTitle,
        string DeletionModeDescription,
        long EstimatedFileCount,
        string CategorySummary);

    /// <summary>
    /// Selected categories that cannot be cleaned with the current process token.
    /// </summary>
    public sealed record ElevationRequirement(bool RequiresElevation, IReadOnlyList<string> BlockedCategoryNames);

    /// <summary>
    /// Aggregates the selected categories into the hero size, action button
    /// label, and breakdown subtext. Mirrors the original RecalculateTotals
    /// semantics exactly (review classification is driven by the SafetyBadge text).
    /// </summary>
    public static SelectionSummary ComputeSelectionSummary(IEnumerable<TargetFolderInfo> targets)
    {
        var selected = targets.Where(t => t.IsSelected).ToList();

        long selectedBytes = 0;
        long selectedFiles = 0;
        int safeCount = 0;
        int reviewCount = 0;

        foreach (var target in selected)
        {
            selectedBytes += target.SizeBytes;
            selectedFiles += target.FileCount;
            if (target.SafetyBadge.Contains("REVIEW", StringComparison.OrdinalIgnoreCase))
                reviewCount++;
            else
                safeCount++;
        }

        string formattedSize = TargetFolderInfo.FormatBytes(selectedBytes);
        string cleanButtonText = $"Clean Selected ({formattedSize})";

        string heroSubtext;
        if (selectedBytes == 0)
        {
            heroSubtext = "Selected categories are clean or ready for scan";
        }
        else
        {
            string breakdown = reviewCount > 0
                ? $" ({safeCount} safe, {reviewCount} review required)"
                : " (100% verified safe)";
            heroSubtext = $"{selectedFiles:N0} junk items selected across {selected.Count} categories{breakdown}";
        }

        return new SelectionSummary(
            selectedBytes,
            selectedFiles,
            safeCount,
            reviewCount,
            selected.Count,
            formattedSize,
            cleanButtonText,
            heroSubtext);
    }

    /// <summary>
    /// Builds the confirmation modal content: estimated size, safety-shield state,
    /// deletion mode (Recycle Bin vs permanent), and the category summary sentence.
    /// </summary>
    public static ConfirmationPreview BuildConfirmationPreview(
        IReadOnlyList<TargetFolderInfo> selectedTargets,
        bool safeMode,
        bool recycleBin)
    {
        long totalEstimatedBytes = selectedTargets.Sum(t => t.SizeBytes);
        long totalEstimatedFiles = selectedTargets.Sum(t => (long)t.FileCount);

        var categoryNames = selectedTargets.Select(t => t.Name).ToList();
        string categoryList = categoryNames.Count <= 5
            ? string.Join(", ", categoryNames)
            : string.Join(", ", categoryNames.Take(5)) + $" and {categoryNames.Count - 5} more";

        string categorySummary =
            $"Cleaning {selectedTargets.Count} selected categories ({totalEstimatedFiles:N0} estimated files). " +
            $"Categories: {categoryList}. System integrity, user credentials, and personal files remain 100% protected.";

        return new ConfirmationPreview(
            TargetFolderInfo.FormatBytes(totalEstimatedBytes),
            safeMode ? "Safety Shield: Active" : "Safety Shield: Disabled",
            safeMode,
            recycleBin,
            recycleBin ? "Recycle Bin Protection: Active" : "Permanent Deletion: Active",
            recycleBin
                ? "Files will be moved to the Windows Recycle Bin and can be restored if needed."
                : "Files will be permanently deleted from disk to maximize free space and cannot be restored.",
            totalEstimatedFiles,
            categorySummary);
    }

    /// <summary>
    /// Detects selected categories that require an elevated token but are not
    /// accessible in the current session (RequiresAdmin and access was denied
    /// at target initialization). Drives the UAC re-launch prompt before cleanup.
    /// </summary>
    public static ElevationRequirement GetElevationRequirement(IEnumerable<TargetFolderInfo> selectedTargets)
    {
        var blocked = selectedTargets
            .Where(t => t.RequiresAdmin && !t.HasAccess)
            .Select(t => t.Name)
            .ToList();

        return new ElevationRequirement(blocked.Count > 0, blocked);
    }

    /// <summary>
    /// Determines whether a target passes the active filter chip and search term.
    /// Mirrors the original FilterTargetPredicate semantics exactly: unknown or
    /// "ALL" tags apply no category filtering; search matches name, description,
    /// category, and folder path (OrdinalIgnoreCase); whitespace search passes all.
    /// </summary>
    public static bool MatchesFilter(TargetFolderInfo target, string? filterTag, string? searchText)
    {
        // 1. Tag filter
        if (filterTag == "SAFE" && !target.IsSafeModeEligible) return false;
        if (filterTag == "SYSTEM" &&
            !target.Category.Contains("System", StringComparison.OrdinalIgnoreCase) &&
            !target.Category.Contains("Driver", StringComparison.OrdinalIgnoreCase) &&
            !target.Category.Contains("Diagnostics", StringComparison.OrdinalIgnoreCase) &&
            !target.Category.Contains("Security", StringComparison.OrdinalIgnoreCase) &&
            !target.Category.Contains("Storage", StringComparison.OrdinalIgnoreCase) &&
            !target.Category.Contains("SO", StringComparison.OrdinalIgnoreCase)) return false;
        if (filterTag == "GAMING" &&
            !target.Category.Contains("Gaming", StringComparison.OrdinalIgnoreCase) &&
            !target.Category.Contains("Shader", StringComparison.OrdinalIgnoreCase) &&
            !target.Category.Contains("GPU", StringComparison.OrdinalIgnoreCase)) return false;
        if (filterTag == "MEDIA" &&
            !target.Category.Contains("Media", StringComparison.OrdinalIgnoreCase) &&
            !target.Category.Contains("App", StringComparison.OrdinalIgnoreCase) &&
            !target.Category.Contains("Browser", StringComparison.OrdinalIgnoreCase) &&
            !target.Category.Contains("Store", StringComparison.OrdinalIgnoreCase) &&
            !target.Category.Contains("Dev", StringComparison.OrdinalIgnoreCase) &&
            !target.Category.Contains("User", StringComparison.OrdinalIgnoreCase) &&
            !target.Category.Contains("Creator", StringComparison.OrdinalIgnoreCase)) return false;

        // 2. Search text filter
        if (string.IsNullOrWhiteSpace(searchText)) return true;

        var term = searchText.Trim();
        return target.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
            || target.Description.Contains(term, StringComparison.OrdinalIgnoreCase)
            || target.Category.Contains(term, StringComparison.OrdinalIgnoreCase)
            || target.FolderPath.Contains(term, StringComparison.OrdinalIgnoreCase);
    }
}
