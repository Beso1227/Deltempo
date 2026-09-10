# Deltempo System Architecture

## 1. High-Level Architectural Overview

Deltempo is built as a modular, high-reliability Windows maintenance, cache cleanup, and NT kernel memory optimization suite. The system strictly separates low-level system safety and deletion logic from UI presentation concerns.

```
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
│   │   - SFC, DISM, WinSxS, CHKDSK │   │   - Ed25519 & SHA256 Verify│   │
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
* **Update Verification**: Enforces cryptographic integrity on self-updates using dual SHA-256 payload matching and Ed25519 digital signature verification.

### 2.3 Presentation Layer (`ViewModels/`, `Views/`, `Cli/`)
* **MVVM Architecture**: ViewModels expose `ICommand` bindings, thread-safe observable collections, and immutable progress snapshots.
* **Non-Blocking Execution**: All I/O operations (scanning, cleaning, memory flushing, repair execution) run strictly asynchronously (`Task.Run` with bounded parallelism and `CancellationToken` support).
* **CLI Parity**: `Deltempo.Cli` exposes full engine functionality headlessly, outputting structured JSON streams and deterministic process exit codes for integration into automated administration scripts and CI pipelines.

---

## 3. Data Flow & The 5-Phase Safety Pipeline

Every file evaluated for cleanup passes through an unbypassable sequential verification pipeline:

```
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
      │  (Absolute blacklist: Windows OS, System32, User Documents, SSH keys, credentials)
      ▼
Phase 4: Multi-Signal Safety Engine Classification
      │  (Evaluates known disposable formats, extensions, 24-hour shields, cache paths)
      ▼
Phase 5: Two-Phase Transactional Execution
         - Phase A: Pre-deletion safety re-check (Symlink check, size drift, lock detection)
         - Phase B: Atomic Deletion / Recycle Bin move
         - Phase C: Post-deletion verification & structured audit logging
```
