# Code Review: Force Delete & Stubborn Purge Implementation

> **RESOLVED — all findings fixed.** Reviewed `bb41edc`; remediated in `162bd20` (v3.0.0).
> One finding (mojibake) was already fixed in `bde18fa`. Retained as an audit record only.
> The severity table below describes the state **as reviewed** — not the current code.
>
> | # | Finding | Status |
> | :-- | :--- | :--- |
> | 1 | Program Files / ProgramData root bypass | Fixed — roots now `AbsoluteBlock` |
> | 2 | `--retry N` value consumed as target | Fixed — option values skipped |
> | 3 | Selection toggle ignored | Fixed — `PropertyChanged` → `RefreshFooterState` |
> | 4 | Reboot-scheduled shown "Failed" | Fixed — now "Reboot Purge" |
> | 5 | Substring path matching | Fixed — anchored to Windows root |
> | 6 | Dry-run directory miscount | Fixed |
> | 7 | Reparse link attributes | Fixed — `StripAttributes` first |
> | 8 | Traversal abort on locked subfolder | Fixed — `IgnoreInaccessible` |
> | 9 | UI-thread blocking | Fixed — profiling off-thread |
> | 10 | CLI Tier B consent | Fixed — `--force-override` required |
> | 11 | Mojibake literal | Already fixed in `bde18fa` |
> | 12 | Shielded checkbox interactive | Fixed — inert |
> | 13 | Tier B transparency in confirm | Fixed — dedicated warning block |

**Scope:** Commit `bb41edc` (`ForceDeleteGate`, `ForceDeleteEngine`, `CliRunner`, `ForceDeleteService`, `ForceDeleteModal`)  
**Review Style:** Terse, actionable findings (`caveman-review`)

---

## Summary of Findings

| Severity | Count | Key Focus |
| :--- | :---: | :--- |
| 🔴 **Bug** | 4 | Safety gate path bypass, CLI argument parsing corruption, UI event binding, reboot status label |
| 🟡 **Risk** | 6 | Substring path matching, dry-run counters, reparse point attributes, tree traversal abort, UI thread blocking, CLI consent bypass |
| 🔵 **Nit** | 2 | Mojibake string literal, active checkbox for shielded targets |
| ❓ **Question** | 1 | Confirmation dialog transparency for Tier B overrides |

---

## Detailed Findings

### 🔴 Critical Bugs

- **`Core/Safety/ForceDeleteGate.cs:L135-L143`**
  - **Problem:** `lower == progFiles` (as well as `progFilesX86` and `progData`) escapes the `StartsWith(progFiles + "\\")` check. Root directories such as `C:\Program Files` or `C:\ProgramData` bypass the application directory check and evaluate as `Allowed` instead of `AbsoluteBlock`.
  - **Fix:** Explicitly check `lower == progFiles || lower.StartsWith(progFiles + "\\")` (and corresponding variables for x86 and ProgramData), and return `ForceDeleteTier.AbsoluteBlock` when targeting root directories.

- **`Services/CliRunner.cs:L363-L369`**
  - **Problem:** Positional target iteration consumes option argument values (e.g. `5` following `--retry 5`) into `targets`, attempting to delete `"5"` as a filesystem item.
  - **Fix:** Skip argument values when encountering options that accept parameters before collecting non-flag positional arguments.

- **`Views/Modals/ForceDeleteModal.xaml.cs:L24`**
  - **Problem:** `IsSelected` property changes on staged items do not trigger `RefreshFooterState()`. Deselecting or selecting targets does not update `ExecuteBtn.IsEnabled` or summary counts.
  - **Fix:** Subscribe to `PropertyChanged` on added `ForceDeleteTargetItem` instances to trigger `RefreshFooterState()` on selection toggle.

- **`Views/Modals/ForceDeleteModal.xaml.cs:L338-L345`**
  - **Problem:** Targets scheduled for reboot deletion (`ForceDeleteStage.ScheduleReboot`) are not counted under `succeeded` and are displayed with status `"Failed"` in the UI.
  - **Fix:** Check `ForceDeleteStage.ScheduleReboot` in attempt results and set `item.StatusText = "Reboot Purge"`.

---

### 🟡 Fragile Risks

- **`Core/Safety/ForceDeleteGate.cs:L107-L112`**
  - **Problem:** `lower.Contains(blocked)` matches substrings across arbitrary drives and non-system directories (e.g., `D:\repo\windows\system\file.txt`).
  - **Fix:** Anchor checks to `winDir` (e.g., `Path.Combine(winDir, blocked)`) with explicit directory separator boundaries.

- **`Core/Cleaning/ForceDeleteEngine.cs:L270-L274`**
  - **Problem:** `DryRun` increments `result.FilesDeleted` even when the target is a directory.
  - **Fix:** Branch on `Directory.Exists(normalized)` and increment `result.DirectoriesDeleted`.

- **`Core/Cleaning/ForceDeleteEngine.cs:L306-L333`**
  - **Problem:** `DeleteReparsePointLink` fails with `UnauthorizedAccessException` if the junction or symlink has ReadOnly or System attributes set on the link itself.
  - **Fix:** Call `StripAttributes(path)` prior to unlinking.

- **`Core/Cleaning/ForceDeleteEngine.cs:L418-L433`**
  - **Problem:** An unreadable subfolder during `di.EnumerateFiles()` or `di.EnumerateDirectories()` throws an exception and halts deletion of the entire parent directory and sibling files.
  - **Fix:** Wrap child entry enumeration in try/catch or configure `EnumerationOptions.IgnoreInaccessible = true`.

- **`Views/Modals/ForceDeleteModal.xaml.cs:L86`**
  - **Problem:** `AddPaths` calls synchronous `CalculateDirectorySize` on the UI thread, freezing the interface when large directories are selected.
  - **Fix:** Dispatch size calculation and initial target profiling to a background worker task.

- **`Services/CliRunner.cs:L454`**
  - **Problem:** `ExplicitOverrideConfirmed` is hardcoded to `true` in CLI mode, allowing `--yes` to delete Tier B items (`OverrideRequired`) without explicit operator consent.
  - **Fix:** Guard Tier B operations with an explicit `--force-override` CLI flag.

---

### 🔵 Nits & Polish

- **`Core/Cleaning/ForceDeleteEngine.cs:L189`**
  - **Problem:** Mojibake `" â€¢ "` present in source string literal.
  - **Fix:** Replace with standard bullet `" • "`.

- **`Views/Modals/ForceDeleteModal.xaml:L222-L228`**
  - **Problem:** Selection checkbox remains interactive for shielded targets (`AbsoluteBlock`), even though they are filtered out from purge execution.
  - **Fix:** Disable the checkbox (`IsEnabled="False"`) when `GateTier == ForceDeleteTier.AbsoluteBlock`.

---

### ❓ Architecture Questions

- **`Views/Modals/ForceDeleteModal.xaml.cs:L253-L304`**
  - **Question:** Why does `ConfirmDestructivePurge` omit a distinct warning section when staged targets contain `OverrideRequired` items?
