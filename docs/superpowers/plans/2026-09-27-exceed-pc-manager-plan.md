# Next-Gen Cleaner Engine (Exceeding Microsoft PC Manager) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement Delivery Optimization cache, Windows Servicing (CBS/DISM/LiveKernelReports) logs, WinGet & Store staging, and native RAM / Working Set Optimization to exceed Microsoft PC Manager in disk reclamation and speed.

**Architecture:** Extend `SystemCacheResolver` and `CleanerService` with new high-yield deep cache targets; create a standalone native `SystemMemoryOptimizationService` using Win32 API working-set trimming; expose memory boost in CLI (`--boost`) and UI.

**Tech Stack:** .NET 10, C# 14, WPF, Win32 P/Invoke (`psapi.dll`, `kernel32.dll`), xUnit.

**Spec:** `docs/superpowers/specs/2026-09-27-exceed-pc-manager-design.md`

## Global Constraints

- Target platform: Windows 10/11 x64, .NET 10.
- All 660+ existing tests must continue to pass without regression.
- Zero external bloatware/telemetry: 100% native C# and Win32 P/Invoke.
- Protect active files: only purge non-locked logs older than 24 hours.

---

### Task 1: Native RAM & Working Set Optimizer Service

**Files:**

- Create: `Services/SystemMemoryOptimizationService.cs`
- Test: `Tests/Deltempo.Tests/SystemMemoryOptimizationServiceTests.cs`

**Interfaces:**

- Produces: `public record MemoryOptimizationResult(long InitialWorkingSetBytes, long FinalWorkingSetBytes, long BytesReclaimed, int ProcessesTrimmed)`
- Produces: `public static MemoryOptimizationResult OptimizeWorkingSet(Action<string, LogLevel>? logAction = null)`

- [ ] **Step 1: Write the failing tests**
Write tests in `Tests/Deltempo.Tests/SystemMemoryOptimizationServiceTests.cs` validating that `OptimizeWorkingSet` returns valid non-negative metrics and does not throw exceptions even in unprivileged environments.

- [ ] **Step 2: Run test to verify it fails**
Run `dotnet test Tests\Deltempo.Tests\Deltempo.Tests.csproj --filter FullyQualifiedName~SystemMemoryOptimizationServiceTests` to verify failure.

- [ ] **Step 3: Implement SystemMemoryOptimizationService**
Create `Services/SystemMemoryOptimizationService.cs` with P/Invoke `EmptyWorkingSet`, process enumeration, protection of critical system processes (`csrss`, `smss`, `services`, `lsass`, `wininit`, `dwm`, `Deltempo`), and error tolerance.

- [ ] **Step 4: Run test to verify it passes**
Run the tests again and verify passing.

---

### Task 2: Deep Storage Resolver Extensions (Delivery Optimization, CBS, WinGet)

**Files:**

- Modify: `Services/SystemCacheResolver.cs`
- Modify: `Services/CleanerService.cs`
- Test: `Tests/Deltempo.Tests/CleanerServiceTests.cs`

**Interfaces:**

- Produces: `CleanerService.GetDeliveryOptimizationDirectories()`
- Produces: `CleanerService.GetServicingLogDirectories()`
- Produces: `CleanerService.GetWinGetAndAppStagingDirectories()`
- Produces: Register `DeliveryOptimization`, `WinServicingLogs`, and `WinGetAndAppPackages` in `CleanerService.GetDefaultTargets()`

- [ ] **Step 1: Write failing tests in CleanerServiceTests**
Add tests asserting that `CleanerService.GetDefaultTargets()` contains the new categories and directory resolver methods return non-empty lists.

- [ ] **Step 2: Run tests to verify failure**
Run: `dotnet test Tests\Deltempo.Tests\Deltempo.Tests.csproj --filter FullyQualifiedName~CleanerServiceTests`

- [ ] **Step 3: Implement resolvers in SystemCacheResolver and CleanerService**
Add the new directory resolution methods and update `GetDefaultTargets()` with appropriate risk tiers and display names.

- [ ] **Step 4: Run tests to verify they pass**
Verify all tests pass.

---

### Task 3: CLI & GUI Integration for Memory Boost

**Files:**

- Modify: `Cli/Program.cs`
- Modify: `MainWindow.xaml.cs` (or view model / quick action button)
- Test: `Tests/Deltempo.Tests/CliTests.cs` (if existing) or manual verification

- [ ] **Step 1: Add `--boost` / `-b` flag to CLI**
Enable running `deltempo_cli.exe --boost` to perform instantaneous memory trimming and report reclaimed RAM.

- [ ] **Step 2: Add RAM Boost trigger in GUI**
Hook memory optimization into GUI action so users can trigger 1-click RAM boost.

- [ ] **Step 3: Run full test suite & rebuild release binaries**
Run `dotnet test` and execute `scripts/build_release_exe.ps1`.
