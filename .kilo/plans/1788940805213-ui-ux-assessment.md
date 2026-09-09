# Deltempo UI/UX Assessment & Improvement Plan

**Date:** 2026-09-09  
**Assessed By:** Kilo (Plan Mode)  
**Scope:** `MainWindow.xaml`, `MainWindow.*.cs`, modals, settings, log drawer, tray UX

---

## Current Rating: 7.2 / 10

### Strengths
- Strong visual hierarchy: hero telemetry → scope explorer → bento cards → action dock
- Consistent dark theme with tokenized brushes and Fluent iconography
- Safety-first mental model: Safety Shield toggle is prominent, badges are clear
- Modals are well-structured (confirmation, celebration, settings, inspector)
- Accessibility hooks present (`AutomationProperties.Name`, `LiveSetting`, `HelpText`)
- Real-time progress feedback via progress bars and log stream

### Critical UX Gaps

| # | Gap | Impact | Severity |
|---|-----|--------|----------|
| 1 | **No empty state for target cards** | First-time users see a blank card list with no guidance to click "Scan Now" | High |
| 2 | **Errors hidden in collapsed log drawer** | Scan/clean failures are silently logged; users see no inline failure state | High |
| 3 | **Confirmation modal omits selected categories** | Users cannot verify which of 25+ scopes will be cleaned | Medium-High |
| 4 | **No keyboard shortcut visibility** | F5 and Ctrl+Enter are only in tooltips/AutomationProperties | Medium |
| 5 | **Settings lacks search and reset** | 4-tab settings is dense; no quick-find or "Restore Defaults" | Medium |
| 6 | **No inline loading skeletons** | Category cards show stale data during re-scan; no shimmer/skeleton state | Medium |
| 7 | **Log drawer has no notification indicator** | Users don't know new logs arrived while drawer is collapsed | Low-Medium |
| 8 | **Fixed modal widths** | `MaxWidth="660/560/500/600"` may clip on 1366×768 or scaled displays | Low |
| 9 | **Tray menu not reviewed for UX** | Dynamic tray items lack grouping labels and separator clarity | Low |

---

## Proposed Improvements (Implementation-Ready)

### 1. Add Empty State Placeholder to Target Cards
**File:** `MainWindow.xaml`  
**Change:** When `_targets.Count == 0` or all items are unscanned, show a centered placeholder in the `ScrollViewer` (row 3) with icon, "No scan data yet", and subtext "Click Scan Now to discover reclaimable files". Hide when scan results exist.

### 2. Show Inline Error State on Failed Scans
**File:** `MainWindow.xaml.cs` + `TargetFolderInfo.cs`  
**Change:** Add `bool HasError` and `string ErrorMessage` to `TargetFolderInfo`. When `ScanFolderAsync` catches, set these instead of only logging. Bind a red error border + retry button in the card `DataTemplate` when `HasError == true`.

### 3. List Selected Categories in Confirmation Modal
**File:** `MainWindow.Cleaning.cs` + `MainWindow.xaml`  
**Change:** Before showing `ConfirmModalOverlay`, build a comma-separated string of selected `target.Name` values (truncate at 5 + "and N more"). Display in `ConfirmModalSummaryText` or a new `TextBlock` above the action buttons.

### 4. Add Visible Keyboard Shortcut Hints
**File:** `MainWindow.xaml`  
**Change:** Add small `TextBlock` badges next to `HeroScanBtn` ("F5") and `CleanButton` ("Ctrl+Enter") using `TextMutedBrush` and `MonospaceFont`. Do not clutter; only on primary CTAs.

### 5. Add Settings Search + Reset Button
**File:** `MainWindow.Settings.cs` + `MainWindow.xaml`  
**Change:** Add a `TextBox` above the tab bar in `SettingsModalOverlay` with real-time filtering: as user types, hide settings panels whose labels don't match. Add a small "Reset to Defaults" `PillButton` in the footer that restores factory `AppSettings` values and re-opens the modal.

### 6. Add Skeleton Loading State for Cards
**File:** `MainWindow.xaml` + `MainWindow.Cleaning.cs`  
**Change:** During `RunScanAllAsync`, show 6 placeholder card rows with animated shimmer `Border` backgrounds instead of empty space. Replace with real data as each scan completes.

### 7. Add Log Notification Badge
**File:** `MainWindow.xaml.cs` + `MainWindow.xaml`  
**Change:** Add a small red dot `Border` (width=6, height=6, cornerRadius=3) on `ToggleLogBtn` when `LogDrawerBorder.Visibility == Collapsed` and new logs exist. Hide when drawer opens.

### 8. Make Modals Responsive
**File:** `MainWindow.xaml`  
**Change:** Replace hardcoded `MaxWidth` on modals with `MaxWidth="{Binding ActualWidth, RelativeSource={RelativeSource AncestorType=Window}, Converter={StaticResource HalfWindowConverter}}"` or use `Width="Auto"` with `MinWidth` and let content dictate size.

### 9. Tray Menu UX Polish
**File:** `Services/TrayService.cs`  
**Change:** Group tray items under `Separator` lines: "Quick Actions" (Clean Safe, Boost RAM), "Tools" (Large Files, Startup, Repair), "App" (Settings, Exit). Add tooltips to each item.

---

## Validation Plan

1. **Visual regression**: Run app on 1920×1080 and 1366×768; verify modals fit, empty state appears, skeletons show during scan.
2. **Accessibility**: Use Narrator to tab through primary flows; confirm shortcut hints and error states are announced.
3. **Functional**: Verify confirmation modal shows selected categories; verify settings search filters panels; verify reset restores defaults.
4. **Performance**: Ensure skeleton shimmer runs on UI thread without blocking scan; confirm no layout thrash during `Task.WhenAll` scan updates.

---

## Open Questions

1. Should the confirmation modal list all selected categories or cap at N with "and X more"? **Recommended: cap at 6 + "and N more" to avoid modal overflow.**
2. Should settings search filter by tab or show matching panels across all tabs? **Recommended: stay on current tab, filter within that tab to avoid disorientation.**
3. Should the empty state animate in (fade/slide) or be static? **Recommended: subtle fade-in (200ms) to match existing modal animations.**
