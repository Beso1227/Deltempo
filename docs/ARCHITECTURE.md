# Deltempo System Architecture

## 1. High-Level Architectural Overview

Deltempo is built as a modular, high-reliability Windows maintenance, cache cleanup, and NT kernel memory optimization suite. The system strictly separates low-level system safety and deletion logic from UI presentation concerns.

```text
┌────────────────────────────────────────────────────────────────────────┐
│                        Presentation Layer                              │
│   ┌───────────────────────────────┐   ┌────────────────────────────┐   │
│   │   WPF Modern UI (net10.0)     │   │     Headless CLI Engine    │   │
│   │   - MainWindow & Views        │   │     - Deltempo.Cli         │   │
│   │   - ViewModels & Converters   │   │     - JSON/Automation APIs │   │
│   └───────────────┬───────────────┘   └─────────────┬──────────────┘   │
└───────────────────┼─────────────────────────────────┼──────────────────┘
                    │                                 │
┌───────────────────▼─────────────────────────────────▼──────────────────┐
│                          Services Layer                                │
│   ┌───────────────────────────────┐   ┌────────────────────────────┐   │
│   │   MemoryOptimizerService      │   │   CleanerService           │   │
│   │   - NT Kernel Working Sets    │   │   - Scope Resolution       │   │
│   │   - Standby & Cache Purging   │   │   - Parallel Scanning      │   │
│   └───────────────────────────────┘   └─────────────┬──────────────┘   │
│   ┌───────────────────────────────┐   ┌─────────────▼──────────────┐   │
│   │   SystemRepairService         │   │   UpdateService            │   │
│   │   - SFC, DISM, WinSxS, CHKDSK │   │   - SHA256 Verify      │   │
│   └───────────────────────────────┘   └────────────────────────────┘   │
│   ┌───────────────────────────────┐   ┌────────────────────────────┐   │
│   │   AppDataFootprintService     │   │   StoreAppOrphanResolver   │   │
│   │   - Read-only measurement     │   │   - AppModel repo evidence │   │
│   │   - No delete code path       │   │   - PFN + publisher match  │   │
│   └───────────────────────────────┘   └────────────────────────────┘   │
└─────────────────────────────────────────────────────┼──────────────────┘
                    ┌─────────────────────────────────┘
┌───────────────────▼────────────────────────────────────────────────────┐
│                    Deltempo.Core Engine Layer                          │
│   ┌───────────────────────────────┐   ┌────────────────────────────┐   │
│   │   Core.Safety                 │   │   Core.Cleaning            │   │
│   │   - PathSecurity (Canonical)  │   │   - CleanupPlanner         │   │
│   │   - ProtectionPolicy (OS/User)│   │   - CleanupExecutor        │   │
│   │   - FileSafetyEngine (Tiers)  │   │   - CleanupTransaction     │   │
│   └───────────────────────────────┘   └────────────────────────────┘   │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Layer Responsibilities & Isolation Invariants

### 2.1 Core Engine (`Core/`)

* **Zero UI Dependency**: Contains no references to WPF, `System.Windows`, or XAML controls. Compiles as pure net10.0 logic reusable across desktop, console, and background daemon agents.
* **Deterministic Execution**: All safety evaluations are algorithmic and rule-based. AI intelligence reports, heuristics, or external telemetry can flag files for review but can **never** override deterministic safety rules or whitelist policies.
* **Fail-Closed Principle**: Unrecognized file formats, inaccessible paths, or ambiguous attributes default unconditionally to `SafetyRiskTier.Unknown` and are preserved.

### 2.2 Services Layer (`Services/`)

* **Memory Optimizer**: Ported and hardened from `WinMemoryCleaner`. Directly interfaces with Windows NT kernel APIs via `ntdll.dll!NtSetSystemInformation` and `psapi.dll!EmptyWorkingSet`. Employs fine-grained Windows token privilege elevation (`SeProfileSingleProcessPrivilege`, `SeIncreaseQuotaPrivilege`, `SeDebugPrivilege`) with automated post-operation privilege revocation.
* **System Repair**: Automates servicing stack recovery (`SFC /scannow`, `DISM /Online /Cleanup-Image /RestoreHealth`, `WinSxS /StartComponentCleanup`, volume `CHKDSK /scan`).
* **Update Verification**: Enforces cryptographic integrity on self-updates via HTTPS + host/path allowlisting and SHA-256 payload verification (plus size, PE-header and version checks) before anything is executed. An ECDSA P-256 manifest-signature verifier and an Authenticode verifier exist and fail closed, but neither is wired into the live update path yet — see `docs/THREAT_MODEL.md` §2.5.

### 2.3 Presentation Layer (`ViewModels/`, `Views/`, `Cli/`)

* **MVVM Architecture**: ViewModels expose `ICommand` bindings, thread-safe observable collections, and immutable progress snapshots.
* **Toolkit Base (Phase 2)**: `ViewModelBase` derives from CommunityToolkit.Mvvm's `ObservableObject`, which supplies `INotifyPropertyChanged` and the raise-only-on-change `SetProperty<T>` helper. `RelayCommand` / `AsyncRelayCommand` deliberately remain the hand-rolled types in `ViewModels/ViewModelBase.cs`: the toolkit's equivalents are `sealed`, expose no `Action<object?>` execute constructor, and name the refresh method `NotifyCanExecuteChanged()` rather than `RaiseCanExecuteChanged()` — all three are asserted by `ViewModelCommandTests`.
* **Converters (Phase 2)**: `Converters/` is down to three files — `WidthConverters.cs` (the three title-bar compact-mode converters plus their shared `CompactThreshold` parser), `SubtractConverter.cs`, and `PercentWidthConverter.cs` (an `IMultiValueConverter`). The unreferenced `BoolToBrushConverter` and `BoolToThicknessConverter` were removed together with their `App.xaml` resources.
* **Deferred — code-behind shrink**: the eight `MainWindow.*.cs` partials (~2,530 LOC) still own event wiring and direct control assignment. Moving that to bindings is staged as its own pass because binding failures surface only at runtime, where the headless unit-test suite cannot see them.
* **Non-Blocking Execution**: All I/O operations (scanning, cleaning, memory flushing, repair execution) run strictly asynchronously (`Task.Run` with bounded parallelism and `CancellationToken` support).
* **CLI Parity**: `Deltempo.Cli` exposes full engine functionality headlessly, outputting structured JSON streams and deterministic process exit codes for integration into automated administration scripts and CI pipelines.

---

## 3. Data Flow & The 5-Phase Safety Pipeline

Every file evaluated for cleanup passes through an unbypassable sequential verification pipeline:

```text
[Target Path]
      │
      ▼
Phase 1: Canonical Path Normalization & Traversal Defense
      │  (Resolves symlinks, relative segments '..', trailing dots/spaces)
      ▼
Phase 2: Canonical Containment Verification
      │  (Ensures target is strictly a subpath of authorized roots; blocks sibling spoofing)
      ▼
Phase 3: Centralized Protection Policy Check
      │  (Absolute blacklist: Windows OS, System32, User Documents, SSH keys,
      │   credentials, non-disposable installer/application state, and AppData
      │   durable state — evaluated per DIRECTORY, not only per file)
      ▼
Phase 4: Multi-Signal Safety Engine Classification
      │  (Evaluates known disposable formats, extensions, 24-hour shields, cache paths)
      ▼
Phase 5: Two-Phase Transactional Execution
         - Phase A: Pre-deletion safety re-check (Symlink check, size drift, lock detection)
         - Phase B: Atomic Deletion / Recycle Bin move
         - Phase C: Post-deletion verification & structured audit logging
```

---

## 4. Destructive-Scope Design: Refusal Over Enumeration

A cleanup engine is only as trustworthy as its idea of what is disposable. Deltempo encodes that
idea as an explicit, testable refusal list rather than as an ever-growing enumeration of targets.

### 4.1 Two-layer refusal

Every non-disposable path is refused **twice, independently**:

1. **Removed from the scope resolvers** (`Services/Providers/CacheResolvers/*.cs`), so it is never
   offered as a candidate in the first place.
2. **Denied by `Core/Safety/ProtectionPolicy.IsProtected`**, so a resolver regression, a
   user-supplied rulepack, or a stale settings entry cannot reintroduce it.

The second layer is the one that matters. The first is a performance and UX decision; the second
is the security control. Each refusal is covered by a `CleanerServiceTests` case that asserts
both that the path is **absent** and that the genuine scratch in the same resolver is **still
present** — so a fix cannot decay into an over-broad removal.

### 4.2 AppData durable state is refused per directory, not per file

`ProtectionPolicy.IsAppDataApplicationState` refuses AppData subtrees holding *durable state*
rather than disposable cache: OneDrive sync databases, Office and Outlook credentials, the
Windows Credential Manager vault, recent-file and jump-list destinations, and SSH and GnuPG key
material. The per-user shell hives (`NTUSER.DAT*`, `UsrClass.dat`) living in the AppData **root**
are matched by prefix, because the shell writes timestamped siblings.

Two implementation details are load-bearing:

- **Marker matching normalizes to a directory-shaped string.** Every subtree marker is anchored
  with a trailing separator, so a marker of `\vault\` matches `AppData\Roaming\Vault\keys` *and*
  `AppData\Roaming\Vault` itself. Without that, a pure "starts with directory + separator" test
  protects everything inside a directory while leaving the directory's own root unprotected —
  which is precisely the case that must fire.
- **Matching is case-insensitive by construction.** A case-sensitive comparison fails *open*: a
  differently-cased real path silently loses its protection. This bug shipped and passed review
  and the build; it was found by measuring behaviour, not by reading code.

### 4.3 The recursive-delete gap, and `TryGetProtectedDescendant`

Per-file protection is not sufficient for a recursive delete. `Directory.Delete(path,
recursive: true)` removes an entire subtree in a single syscall, so the per-file
`IsProtected` check consulted by `CleanupPlanner` and `CleanupExecutor` never runs for any file
in that subtree. A protected directory can therefore be removed as collateral damage by a purge
whose *own* target is legitimate.

`ProtectionPolicy.TryGetProtectedDescendant` closes this. `RootLeftoverPurgeService.PurgeDirectory`
calls it before any recursive delete and refuses the operation if it finds a protected subtree.
The walk is deliberately bounded and defensive:

| Property | Behaviour |
| :--- | :--- |
| Bound | 5,000 directories inspected |
| On bound exceeded | **Fails closed** — an incomplete scan must never authorize a recursive delete |
| Reparse points | Never traversed, so the walk cannot escape the AppData root |
| Outside AppData | Skipped entirely; the scan is not a general-purpose subtree checker |
| Marker semantics | A path that *is* a protected directory matches on equality, not only as a prefix |

### 4.4 The AppData footprint report is read-only by construction

`Services/AppDataFootprintService.cs` measures what occupies `LOCALAPPDATA`, `APPDATA` and
`LocalLow` at depth 1 and returns immutable `AppDataFootprintEntry` records — deliberately *not*
`TargetFolderInfo`, because it is not a cleanup target and must not be reachable from the
deletion pipeline. `CliRunner.AppData.cs` exposes `appdata` / `appdata-report` / `footprint` with
`--json` and `--top N`, and intentionally provides **no** `--clean`, `--yes`, or delete path. The
in-app `AppDataFootprintModal` is likewise a read-only overlay. Measurement runs off the UI
thread and deliberately does not set `_isBusy`, so the report stays usable mid-scan.

Two correctness rules the report depends on:

- **Reparse points are skipped.** Legacy AppData compatibility junctions (`Application Data`,
  `Local Settings`) point back at `Local`. Walking through them re-reads the same tree: measured
  on a real profile, that inflated 32.6 GB to 59.5 GB.
- **The AppData root is never emitted as a row of its own**, because doing so double-counts every
  file beneath it (63.1 GB reported against 31.53 GB actual).

### 4.5 Orphaned Store app detection uses binary evidence

`Services/Providers/CacheResolvers/StoreAppOrphanResolver.cs` determines whether a
`LOCALAPPDATA\Packages` subdirectory still has an installed MSIX package by reading the AppModel
package repository from the registry — `HKCU\Software\Classes\Local Settings\Software\Microsoft\
Windows\CurrentVersion\AppModel\Repository\Packages` and the `HKLM` equivalent — as **binary
evidence**, not by guessing from the directory name.

| Concern | Handling |
| :--- | :--- |
| Key-shape mismatch | The folder is `Name_PublisherId`; the repository key is `Name_Version_Arch_ResourceId_PublisherId`. Both sides split at the **final** underscore and match on publisher ID plus name prefix. The trailing underscore stops `aim` matching `aimgr_…`. |
| False matches | Publisher IDs are 13–14 alphanumerics; that length check is what excludes real legacy folders such as the IE lock folder `windows_ie_ac_001`. |
| Both hives unreadable | Returns **nothing** rather than guessing. |
| Result severity | `LowRisk` — `IsSafeModeEligible = false`, `IsSelected = false`. |
| Single source of truth | Refusals come from `ProtectionPolicy`, not a duplicated name list. |

Validated against 104 real `Packages` folders: 96 matched, 7 non-package directories skipped, 1
candidate rejected — **zero false positives**. Yield is machine-dependent by design; the
feature's value is correctness, not guaranteed reclaimable bytes.
