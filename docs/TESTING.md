# Deltempo Testing Guide & Strategy

## 1. Testing Philosophy & Invariants

Because Deltempo is a Windows maintenance and deletion utility operating near sensitive system files and user documents, the test suite adheres to strict non-negotiable principles:

1. **Zero Real User Profile Scanning**: Unit and integration tests must **never** scan or mutate real `%USERPROFILE%` documents, desktop items, or production system drives.
2. **Ephemeral Sandboxes**: All filesystem tests allocate unique temporary sandboxes (e.g. `%TEMP%\Deltempo_Test_<GUID>`) and clean them up deterministically on `Dispose()`.
3. **Zero Test Skips**: Tests must never be decorated with arbitrary `[Fact(Skip = "...")]` or silenced to make CI pass.
4. **Separation of Benchmarks**: Throughput benchmarks (`Category=Benchmark`) are decoupled from standard CI gates to prevent timing-based flakiness across virtualized CI runners.

---

## 2. Test Suite Architecture

| Test Suite | Purpose | Key Coverage Area |
| :--- | :--- | :--- |
| **AdversarialFilesystemTests.cs** | Edge cases & malicious filesystems | Reparse points, sibling directory collisions, path traversal, unknown file fail-closed invariants |
| **DestructiveHardeningTests.cs** | Deletion safeguards & drift | Pre/post-verification, size drift detection, audit records, lock handling |
| **PathSecurityTests.cs** | Canonical path normalization | Absolute path resolution, dot-segments, canonical containment |
| **PathSecurityFuzzTests.cs** | Property-based fuzzing of the canonicalizer | Deterministic seeded fuzzing (100k standard / 1M `FuzzDeep`): no-throw, no surviving traversal segments, idempotence, blank-stays-blank |
| **ProtectionPolicyTests.cs** | Non-negotiable blacklist | Windows OS, System32, User personal files, SSH/GPG keys, credentials |
| **FileSafetyEngineTests.cs** | Multi-signal risk classification | Safe, LowRisk, ReviewRequired, Unknown categorization |
| **SystemRepairServiceTests.cs** | Servicing stack orchestration | Command generation for SFC, DISM, WinSxS, and CHKDSK |
| **UpdaterTests.cs** | Supply chain & release security | Ed25519 signature checks, SHA-256 validation, corrupted payload rejection |
| **ArchitectureAndViewModelTests.cs** | Layer separation & MVVM | Null safety, property change notifications, Core layer isolation from WPF |
| **CliRunnerTests.cs** | Headless CLI automation | Exit codes, argument parsing, dry-run simulation, JSON formatting |
| **CleanEngineBenchmarks.cs** | High-throughput stress testing | Parallel directory traversal and deletion throughput |
| **MemoryOptimizerBenchmarks.cs** | Memory engine performance | NT Kernel working set sweeps and physical memory telemetry |

---

## 3. Running Tests Locally

### 3.1 Standard Test Suite (CI Equivalent)

Run all functional, security, and architectural tests (excluding timing benchmarks):

```bash
dotnet test deltempo.sln -c Release --filter "Category!=Benchmark"
```

### 3.2 Running Performance Benchmarks

Run throughput and memory engine benchmarks explicitly:

```bash
dotnet test Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release --filter "Category=Benchmark"
```

### 3.3 Path Canonicalizer Fuzz Campaigns

`PathSecurityFuzzTests.cs` runs a deterministic, seeded (seed `0x5EED_2024`) in-process
fuzz campaign against `PathSecurity.NormalizeCanonicalPath` on every build — no native
libFuzzer harness required, so it executes in CI and on contributor machines alike.
The generator targets the shapes that break naive canonicalizers: traversal segments,
device prefixes, ADS streams, reserved device names, 8.3 short names, overlong paths,
and mixed separators.

Every payload must uphold five invariants: never throw, never retain a surviving
multi-dot traversal segment, be idempotent across re-canonicalization (two code paths
must never disagree about what is being deleted), never return null, and never turn
blank input into a live path. A failure prints the exact input, and the fixed seed
(`0x5EED_2024`) makes it reproducible.

Because the test project targets net10.0-windows on Microsoft.Testing.Platform,
campaigns run via the built test executable (matching `.github/workflows/ci.yml`),
not `dotnet test`:

```powershell
dotnet build Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release --no-restore
# Standard campaign (100k cases, Category=Fuzz - also runs in CI via Category!=Benchmark)
./Tests/Deltempo.Tests/bin/Release/net10.0-windows/Deltempo.Tests.exe --filter "Category=Fuzz"
# Deep campaign (1M cases, Category=FuzzDeep - local runs, excluded from CI)
./Tests/Deltempo.Tests/bin/Release/net10.0-windows/Deltempo.Tests.exe --filter "Category=FuzzDeep"
```

### 3.4 Running with Code Coverage

To run with code coverage collection and report generation:

```powershell
dotnet tool install --global dotnet-coverage --version 18.*
dotnet-coverage collect "dotnet test Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release --filter Category!=Benchmark --no-restore" -f cobertura -o Tests/Deltempo.Tests/TestResults/coverage.cobertura.xml
```

Enforced minimum line coverage threshold in CI: **65%**.
