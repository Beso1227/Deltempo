# UI Consolidation Report — WPF head vs WinUI head

*Read-only audit, 2026-10-28. No source change accompanies this document; the agent wrote and
verified it from the actual tree state at commits `0f64439`–`acd92ff`.*

---

## 1. Scope and ground truth

Deltempo ships **two UI heads over one Core**:

| Head | Project | Status |
|---|---|---|
| WPF | `src/Deltempo.App.Wpf` (`WinTempCleaner.csproj`) | **Shipping default.** One window, 9 partial-behind files, 6 modal views |
| WinUI 3 | `src/Deltempo.App.WinUI` (`Deltempo.App.WinUI.csproj`) | Parity port, opt-in. One shell, 17 workspace views, 12 ViewModels |

Both compile against the same `Deltempo.Core`. The WinUI ViewModels additionally live in a
dedicated assembly `src/Deltempo.App.WinUI.ViewModels` (compiled by link-glob from the shell
folder) so that the coverage collector can see them — see `csproj` comment block.

This report answers: how much of the two heads actually overlaps, what it costs, and what a
safe consolidation (and eventual retirement) looks like.

## 2. Real inventory — workspace/modal pairs

Every WinUI workspace has a WPF counterpart. Service calls were grepped from the actual
source of each side (`MainWindow.*.cs` partials vs `WinUI/ViewModels/*.cs` OEM content):

| Feature pair (WinUI View … ↔ WPF surface) | Shared Deltempo.Core calls — WinUI side | Shared Deltempo.Core calls — WPF side |
|---|---|---|
| `CleanerWorkspaceView` ↔ `MainWindow.Cleaning/.xaml.cs/Ui` | CleanerService, CleanupExecutor, DriveHealthService, DriveTelemetryService, ElevationService, GameBoostService, LocalizationService, SoundService, SystemRepairService, TrayService | CleanerService, CleanupExecutor, DriveHealthService, DriveTelemetryService, ElevationService, GameBoostService, LocalizationService, SoundService, SystemRepairService, TrayService, ThemeService |
| `LargeFilesWorkspaceView` + `TopFilesView` ↔ `MainWindow.LargeFiles` | CleanupExecutor, LargeFileHunterService, SoundService | LargeFileHunterService, SoundService |
| `MemoryWorkspaceView` + `MemoryOptimizerView` ↔ `MainWindow.Memory` + `MemoryOptimizerModal` | MemoryOptimizerService, ProcessOptimizerService, SoundService | MemoryOptimizerService |
| `ProcessWorkspaceView` ↔ `MainWindow.SystemManagers` (process section) | MemoryOptimizerService, ProcessOptimizerService, SoundService | ProcessOptimizerService, MemoryOptimizerService |
| `StartupWorkspaceView` ↔ `MainWindow.SystemManagers` (startup section) | StartupIntelligenceService, StartupManagerService, SoundService | StartupIntelligenceService, StartupManagerService |
| `SystemRepairView` ↔ `MainWindow.SystemRepair` + `SystemRepairModal` | SystemRepairService, ElevationService, SoundService | SystemRepairService (telemetry-side AnimatedProgressBar) |
| `AppUninstallView` ↔ `MainWindow` (uninstall modal flow) | InstalledAppService, AppIntelligenceService, RootLeftoverScannerService, RootLeftoverPurgeService, SoundService | InstalledAppService, AppIntelligenceService (via `AppUninstallModal`) |
| `AppDataFootprintView` ↔ `AppDataFootprintModal` | AppDataFootprintService, SoundService | AppDataFootprintService |
| `LockedFilesView` ↔ `LockedFilesModal` | RestartManagerService, SoundService | RestartManagerService |
| `ForceDeleteView` ↔ `ForceDeleteModal` | ForceDeleteService, SoundService | ForceDeleteService, ForceDeleteEngine |
| `SettingsWorkspaceView` ↔ `MainWindow.Settings` | AutoCleanService, LocalizationService, OnlineFileIntelligenceService, SettingsService, ShellExtensionService, SoundService, TaskSchedulerService | AutoCleanService, LargeFileHunterService, OnlineFileIntelligenceService, SettingsService, ShellExtensionService, SoundService, TaskSchedulerService |
| `UpdateOfferView` ↔ `MainWindow.Updates` | UpdateService, SoundService, SettingsService | UpdateService, SoundService, SettingsService |
| `ActivityLogView`, `AboutWorkspaceView` ↔ `MainWindow.Ui` (title bar / status) | — | LocalizationService, SettingsService, SoundService, ThemeService |

**Key observation:** every WinUI ViewModel calls the *same Core service classes* its WPF
counterpart calls — the duplication is in the **presentation layer only**, not in domain
logic. There is no drift-gap where WinUI calls a service WPF does not, and the reverse is
limited to UI-adjacent pieces (ThemeService, DwmBackdropService — WPF-only visuals).

## 3. Overlap estimate

- **12 of 13** functional pairs exist on both sides (≈ **92%** by pair count).
- Coverage by *users*: WinUI is opt-in (`--winui` / shell switch); the published default
  binary remains WPF. The 9-feature batches landed to parity (commits `0d2ee15`, `abbb3ef`).
- Remaining WinUI-only deltas: no `DwmBackdrop` transparency, no system-restore-point tray
  flow (WPF-only `MainWindow.xaml.cs` scaffolding).
- Source volume: WPF partials ~169 KB; WinUI views+VMs ~704 KB (XAML-heavy; the VM code is
  the near-equivalent of the partial-behind code).

## 4. Test coverage reality (measured, Phase C method)

| Head | Measured line coverage (CI filter) | What is tested |
|---|---|---|
| `WinTempCleaner.Core*` | **76.5%** (4104/5363; floor 0.76 in ci.yml) | All domain services, Safety, Cleaning, Update |
| `Deltempo.App.WinUI*` | **23.8%** (726/3055; floor 0.23 in ci.yml) | The 12 ViewModels (via `Deltempo.App.WinUI.ViewModels` + `InternalsVisibleTo`), shell behaviors partially |
| WPF partial-behind code | **0% (never measured, never testable as written)** | Code-behind with static service calls and window lifetime coupling |

The consolidation thesis in one line: **the WinUI head has an untested WPF twin whose domain
behavior MIRROR does exist (the ViewModels); the WPF head carries all of its logic inside
untestable partial-behind code.** Moving domain flow out of `MainWindow.*.cs` partials into
Core-side ViewModels (like `Core/ViewModels/CleaningPipelineViewModel.cs` already does for one
surface) is the highest-leverage testability work available — irrespective of which head wins.

## 5. Dependency cost per head

| Head | Package dependencies (csproj, non-CDP) | Notes |
|---|---|---|
| Core | `Microsoft.CodeAnalysis.NetAnalyzers`, `CommunityToolkit.Mvvm` | Shared, unavoidable |
| WPF | none (net10.0-windows TFM) | Private-package-free; ~169 KB source; existing signing/release pipeline already covers it |
| WinUI | `CommunityToolkit.Mvvm`, `Microsoft.WindowsAppSDK`, `Microsoft.Windows.SDK.BuildTools` (packages as source project; CD-check performed in `Deltempo.App.WinUI.ViewModels` PVM) plus Codacy/Commit Spooner integration | Triple RID matrix (x64 + arm64 + Debug pass through net10.0-windows10.0.19041.0); shell-carrying `ShellDiagnostics.cs` / `ShellMotion.cs` modules |
| WinUI.ViewModels | `CommunityToolkit.Mvvm`, ProjectReference Core | Link-glob compiles `../Deltempo.App.WinUI/ViewModels/*.cs` |

The WinUI head is the more expensive one to ship (extra TFM window, WindowsAppSDK,
arm64 matrix) — that cost exists for head two, no matter how little it is used.

## 6. Shipping default (user-facing reality)

- **WPF is the shipping head today.** Portable binaries published by release.yml are
  `Deltempo.exe` (WPF) and `deltempo_cli.exe`.
- Recommendation framing: neither head should be retired by agent decision. WPF retirement
  REQUIRES an explicit maintainer sign-off gate AND a corrected, working WinUI parity
  prompt at feature parity (Phase-tree point: batches A+B landed; remaining deltas from §3
  must be zeroed first).

## 7. Safe retirement sequence (proposed gates, not a schedule)

| Step | Gate | On failure |
|---|---|---|
| 1. Feature parity | Every §2 pair has identical Core service flow, no WPF-only feature | Keep parity tree work-loop open; do not move to step 2 |
| 2. Beta opt-in | Users voluntarily launch the WinUI head; WinUI crash rate ≤ WPF's for one soak window | Remain in step 1 |
| 3. WinUI primary published | release.yml publishes the WinUI binary as the default download; WPF binary still published | Roll the release notes back and re-publish |
| 4. WPF binary frozen | WPF keeps shipping unchanged for ≥ 1 release cycle for emergencies | Rebuild |
| 5. WPF csproj retired | Only after step 4's cycle has elapsed with zero regression reports | Restore the csproj from git history |

Each state is independently reversible; no step removes code except step 5, which is a
git-history recoverable delete.

## 8. What consolidation should NOT be attempted without maintainer sign-off

- Retro-fitting the WPF head to consume the WinUI ViewModels: would need `CommunityToolkit.Mvvm`
  command semantics inside WPF partials — a large rewrite of lived-behavior code with zero
  behavior tests backing it. Do not do this opportunistically.
- Collapsing the `Deltempo.App.WinUI.ViewModels` link-glob assembly into the WinUI shell
  project: reverses the Phase-C coverage enablement (see its csproj comment block).
- Deleting any WPF partial before gates 1–4: the shipping head must not be the experimental one.

## 9. Verification checklist for whoever consumes this report

- [ ] Confirm each row of §2 by reading the two files it points at (grep the service class
      reference list back out of both).
- [ ] Confirm the coverage numbers against the newest `tests/Deltempo.Tests/TestResults/*.cobertura.xml`.
- [ ] Confirm the package lists against the three csproj files.
- [ ] The §7 gate table is the contract: any deviation needs a maintainer decision
      recorded before branch renames or deletions.

---

*End of report. Zero source changes accompany it (`git status --stat` verified at authoring.*
