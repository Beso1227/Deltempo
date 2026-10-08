# Deltempo Performance & Benchmark Guide

## 0. Baseline & Guardrails (Phase 0)

Pinned build governance — single source of truth, do not override per-project:

| Guardrail | Value | Source |
| --- | --- | --- |
| .NET SDK | `10.0.401` (`rollForward: latestFeature`, `allowPrerelease: false`) | `global.json` |
| Central Package Management | `ManagePackageVersionsCentrally=true` | `Directory.Packages.props` |
| Nullable / Warnings-as-errors / Deterministic | `true` / `true` / `true` | `Directory.Build.props` |
| Version | `3.0.0` | `Directory.Build.props` |

### Recorded baseline (before Phase 1 hot-path work)

Captured on SDK `10.0.401`, Windows, `-c Release`:

```powershell
dotnet build deltempo.sln -c Release          # 0 Warning(s), 0 Error(s)
dotnet test Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release --no-build --filter "Category!=Benchmark"
```

| Metric | Baseline |
| --- | --- |
| Build warnings / errors | **0 / 0** |
| Test suite (`Category!=Benchmark`) | **894 passed, 0 failed, 0 skipped** |
| Test duration | ~25 s |
| Scan throughput (GB/s) | see §2 — re-measure with `scripts/benchmark.ps1` |
| Clean throughput (items/s) | ~4,000 - 8,500 files/s (NVMe) — §2.1 |
| Memory working-set delta | 25 - 90 ms sweep — §2.2 |
| Single-file publish size | re-measure via `scripts/build_release_exe.ps1` |

**Regression discipline:** any Phase 1+ change that moves these numbers >10% in the wrong
direction is a regression. Benchmarks are excluded from CI (`Category!=Benchmark`) and must be
run explicitly:

```powershell
dotnet test --filter "Category=Benchmark" -c Release
```

Vulnerability and currency audits both run read-only in CI (`dotnet list package --vulnerable`,
`dotnet list package --outdated`); neither can fail the build on its own.

### Phase 1 (hot-path) — changes applied & measured results

| Area | Change | Measured outcome |
| --- | --- | --- |
| `ProtectionPolicy`, `FileSafetyEngine`, `LargeFileHunterService` | 8 read-only `HashSet<string>` → `FrozenSet<string>` (`ToFrozenSet`, `OrdinalIgnoreCase`) | Locked mutable custom-exclusion sets stay `HashSet`; zero safety-tier behavior change (894 tests green) |
| `DuplicateFileService` | Header + full hash use static `SHA256.HashData` (no per-file `SHA256` instance, pooled stream buffer) | Duplicate scan **0.43s → 0.28s (922 → 1,453 files/s)** on the 400-file synthetic corpus |
| `VolumeScanCoordinator` | `SemaphoreSlim` + one `Task.Run` per item → `Parallel.ForEachAsync` bounded per volume | No per-item `Task` allocation; DOP-per-volume contract preserved (tests assert `≤ 2`) |
| `Core/Update` | Reflection-based `JsonSerializer` → source-generated `UpdateJsonContext` | Compile-time serializer metadata; Core stays NativeAOT/trimming-friendly |

#### BLAKE3 evaluation (rejected on measured evidence)

`Blake3` 3.0.2 was benchmarked against hardware-accelerated SHA-256 (`DuplicateScanBenchmarks`,
i7-12700H / SHA-NI + AVX2, median of 5 warmed iterations):

| Path | SHA-256 | BLAKE3 | Ratio |
| --- | --- | --- | --- |
| Full-file hash, 64 MB | 1723 MB/s | 1410 MB/s | **0.82x** (BLAKE3 slower) |
| Header stage, 4 KB × 2,000 ops | 4.5 ms | 10.7 ms | **0.42x** (BLAKE3 2.4x slower) |

**Decision:** production hashing stays on SHA-256. The 4 KB header stage is the highest-call-count
path (every size-colliding candidate), so BLAKE3 there would be a ~2.4x regression — failing the
Phase 1 success criterion of *faster* scans. `Blake3` is retained as a **test-only** reference so the
comparison can be re-run on non-SHA-NI hardware, where the result may invert. Re-evaluate only if
profiling targets CPUs without SHA extensions.

---

## 1. Benchmark Methodology

Deltempo achieves high throughput through deterministic Windows-native engineering rather than heuristic approximations.

Benchmarks reside under `Tests/Deltempo.Tests/CleanEngineBenchmarks.cs` and `Tests/Deltempo.Tests/MemoryOptimizerBenchmarks.cs`, tagged with `[Trait("Category", "Benchmark")]`.

---

## 2. Core Benchmark Suites

### 2.1 Parallel Deletion Engine (`CleanEngineBenchmarks.cs`)

* **Scenario**: 1,500 stale files (8 KB payload each) distributed across 50 nested subfolders.
* **Test Action**: Executes `CleanerService.CleanFolderAsync` with parallel deletion workers.
* **Observed Metrics**:
  * **Throughput**: ~4,000 - 8,500 files/sec on modern NVMe SSDs.
  * **Bandwidth**: >30-65 MB/sec metadata and payload deletion rate.
  * **Correctness Check**: Every test asserts zero orphaned files and exact byte counts matching expectation.

### 2.2 Native Memory Engine (`MemoryOptimizerBenchmarks.cs`)

* **Scenario**: Global NT working set reduction across all non-whitelisted processes using `ntdll!NtSetSystemInformation`.
* **Observed Metrics**:
  * **Latency**: Working set sweep completes within 25 - 90 ms.
  * **Process Trim Count**: Safely iterates running user processes without touching whitelisted system processes (`csrss.exe`, `lsass.exe`, `explorer.exe`).
  * **Allocation Profile**: Zero GC pressure during polling loops due to frozen brush reuse.

---

## 3. Key High-Performance Engineering Patterns

1. **Frozen Brushes for UI Telemetry**:
   UI telemetry gauges refresh every 1,000 ms. Creating new `SolidColorBrush` instances on each tick causes continuous Gen0 garbage collection churn. All brushes are created with `.Freeze()`:

   ```csharp
   private static readonly Brush HighUsageBrush = CreateFrozenBrush("#EF4444");
   private static readonly Brush NormalUsageBrush = CreateFrozenBrush("#00F2B0");
   ```

2. **Bounded Concurrency in Deletion Engine**:
   Rather than unconstrained `Task.Run` loops that exhaust thread pools and thrash storage controllers, the parallel engine bounds concurrent disk operations:

   ```csharp
   int maxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 2, 8);
   ```

3. **Stateless Canonical Path Validation**:
   Path comparisons use `StringComparison.OrdinalIgnoreCase` with zero regex overhead and early boundary length checks.
