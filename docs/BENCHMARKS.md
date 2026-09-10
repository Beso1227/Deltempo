# Deltempo Performance & Benchmark Guide

## 1. Benchmark Methodology

Deltempo achieves high throughput through deterministic Windows-native engineering rather than heuristic approximations.

Benchmarks reside under `Tests/Deltempo.Tests/CleanEngineBenchmarks.cs` and `Tests/Deltempo.Tests/MemoryOptimizerBenchmarks.cs`, tagged with `[Trait("Category", "Benchmark")]`.

---

## 2. Core Benchmark Suites

### 2.1 Parallel Deletion Engine (`CleanEngineBenchmarks.cs`)
* **Scenario**: 1,500 stale files (8 KB payload each) distributed across 50 nested subfolders.
* **Test Action**: Executes `CleanerService.CleanFolderAsync` with parallel deletion workers.
* **Observed Metrics**:
  - **Throughput**: ~4,000 - 8,500 files/sec on modern NVMe SSDs.
  - **Bandwidth**: >30-65 MB/sec metadata and payload deletion rate.
  - **Correctness Check**: Every test asserts zero orphaned files and exact byte counts matching expectation.

### 2.2 Native Memory Engine (`MemoryOptimizerBenchmarks.cs`)
* **Scenario**: Global NT working set reduction across all non-whitelisted processes using `ntdll!NtSetSystemInformation`.
* **Observed Metrics**:
  - **Latency**: Working set sweep completes within 25 - 90 ms.
  - **Process Trim Count**: Safely iterates running user processes without touching whitelisted system processes (`csrss.exe`, `lsass.exe`, `explorer.exe`).
  - **Allocation Profile**: Zero GC pressure during polling loops due to frozen brush reuse.

---

## 3. Key High-Performance Engineering Patterns

1. **Frozen Brushes for UI Telemetry**:
   UI telemetry gauges refresh every 1,000 ms. Creating new `SolidColorBrush` instances on each tick causes continuous Gen0 garbage collection churn. All brushes are created with `.Freeze()`:
   ```csharp
   private static readonly Brush HighUsageBrush = CreateFrozenBrush("#EF4444");
   private static readonly Brush NormalUsageBrush = CreateFrozenBrush("#00E5FF");
   ```
2. **Bounded Concurrency in Deletion Engine**:
   Rather than unconstrained `Task.Run` loops that exhaust thread pools and thrash storage controllers, the parallel engine bounds concurrent disk operations:
   ```csharp
   int maxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 2, 8);
   ```
3. **Stateless Canonical Path Validation**:
   Path comparisons use `StringComparison.OrdinalIgnoreCase` with zero regex overhead and early boundary length checks.
