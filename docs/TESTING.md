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

### 3.3 Running with Code Coverage
To run with code coverage collection and report generation:
```powershell
dotnet tool install --global dotnet-coverage --version 18.*
dotnet-coverage collect "dotnet test Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release --filter Category!=Benchmark --no-restore" -f cobertura -o Tests/Deltempo.Tests/TestResults/coverage.cobertura.xml
```
Enforced minimum line coverage threshold in CI: **65%**.
