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
| **UpdaterTests.cs** | Supply chain & release security | SHA-256 validation, corrupted payload rejection, and manifest-signature rejection (empty, null, malformed and tampered manifests must all fail closed). The positive path lives in `ManifestKeyProvisioningTests.cs`, which runs a full verify round-trip against a test-only ECDSA P-256 key pair; `ManifestSignatureBindingTests.cs` additionally proves an artifact-substitution attack is rejected. Live verification stays blocked until an operator provisions a real trusted key (`docs/THREAT_MODEL.md` §2.5) |
| **ArchitectureAndViewModelTests.cs** | Layer separation & MVVM | Null safety, property change notifications, Core layer isolation from WPF |
| **CliRunnerTests.cs** | Headless CLI automation | Exit codes, argument parsing, dry-run simulation, JSON formatting |
| **CleanEngineBenchmarks.cs** | High-throughput stress testing | Parallel directory traversal and deletion throughput |
| **MemoryOptimizerBenchmarks.cs** | Memory engine performance | NT Kernel working set sweeps and physical memory telemetry |

---

## 3. Running Tests Locally

### 3.1 Standard Test Suite (CI Equivalent)

Run all functional, security, and architectural tests (excluding timing benchmarks).

Because the test project targets net10.0-windows on Microsoft.Testing.Platform, the
.NET 10 SDK's `dotnet test` cannot drive it — the VSTest bridge is rejected outright
("Testing with VSTest target is no longer supported"). Build once, then invoke the
test executable directly, exactly as `.github/workflows/ci.yml` does:

```powershell
dotnet build tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release --no-restore
./Tests/Deltempo.Tests/bin/Release/net10.0-windows/Deltempo.Tests.exe --filter "Category!=Benchmark"
```

The style gate must be run the same way CI runs it, against the solution:

```powershell
dotnet format deltempo.sln --verify-no-changes --no-restore
```

Running it against `src/Deltempo.App.Wpf/WinTempCleaner.csproj` instead is a strictly weaker check — that
project explicitly excludes `Core\`, `Tests\`, `Cli\` and `Services\`, so a per-project
run reports clean while CI fails. This has happened.

> **Read the summary, not the exit code.** Microsoft.Testing.Platform can exit non-zero
> when foreground threads outlive an otherwise-green run. CI therefore treats
> `failed: [1-9]` in the output as the verdict, and warns rather than fails on a
> non-zero exit after a full pass.

### 3.2 Running Performance Benchmarks

Run throughput and memory engine benchmarks explicitly:

```powershell
./Tests/Deltempo.Tests/bin/Release/net10.0-windows/Deltempo.Tests.exe --filter "Category=Benchmark"
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
dotnet build tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release --no-restore
# Standard campaign (100k cases, Category=Fuzz - also runs in CI via Category!=Benchmark)
./Tests/Deltempo.Tests/bin/Release/net10.0-windows/Deltempo.Tests.exe --filter "Category=Fuzz"
# Deep campaign (1M cases, Category=FuzzDeep - local runs, excluded from CI)
./Tests/Deltempo.Tests/bin/Release/net10.0-windows/Deltempo.Tests.exe --filter "Category=FuzzDeep"
```

### 3.4 Running with Code Coverage

Coverage is produced by the Microsoft.Testing.Extensions.CodeCoverage plugin, which is
already wired into the test project. Pass `--coverage` to the test executable — no
external tool install is involved:

```powershell
dotnet build tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release --no-restore
./Tests/Deltempo.Tests/bin/Release/net10.0-windows/Deltempo.Tests.exe `
  --filter "Category!=Benchmark" `
  --coverage `
  --coverage-output-format cobertura `
  --results-directory tests/Deltempo.Tests/TestResults
```

This replaces the VSTest-only `--collect:"XPlat Code Coverage"` flag and the retired
`dotnet-coverage` global tool, which is no longer what CI runs.

Two enforced minimum line coverage thresholds run in CI:

- **65%** on `WinTempCleaner.Core*` classes - the safety, cleaning, update and
  scanning pipelines. The WPF UI head is excluded because a headless CI run cannot
  exercise views, and including it would make the number meaningless.
- **25%** on `Deltempo.App.WinUI*` classes - the WinUI 3 shell's ViewModels. That
  shell was outside every gate until 2026-10-22: its ViewModels were not compiled
  into the test project at all, so thousands of lines of the shipping UI had no
  tests and no coverage measurement. They now live in their own assembly
  (`Deltempo.App.WinUI.ViewModels`) so the coverage collector can see them -
  linking them into the test project would have executed them while excluding them
  from the report, because the collector excludes the test assembly. The 25% is a
  FLOOR, not a target: it sits below the measured value with headroom, so that
  deleting a test file is caught while ordinary churn is not. Raise it as more
  seams appear.

### 3.5 Documentation Is Part of the Suite

`DocumentationConsistencyTests` is not a documentation nicety — it is a build gate, and
it fails the run rather than publishing a wrong claim:

- **`NoFile_TellsTheReaderToRunDotnetTest`** — fails if any instruction file re-introduces
  a `dotnet test` command line.
- **`Workflow_ThatRunsTests_UsesTheMtpTestExecutable`** — fails if `ci.yml` or
  `release.yml` regress to `dotnet test`. This exists because `ci.yml` was correct while
  `release.yml` was not, so a release could fail while CI stayed green.
- **`PublishedTestCount_MatchesTheRealSuiteSize`** — derives the true case count by
  reflection over this assembly and fails if the README badge disagrees. Internal
  consistency alone is not enough: badge-versus-prose-versus-translations agreed with
  each other for months while all of them were hundreds of tests stale.
- **`Installer_TrustAnchor_IsNotADecorativeFallback`** — fails if a stale
  `docs/checksums.sha256` reappears or the installer regains a Pages-hosted fallback.

> The reflection-based count is exact only while the suite uses no `MemberData`,
> `ClassData` or `TheoryData`. Every case is currently a `[Fact]` or a `[Theory]` with
> inline rows. Introducing a dynamic data source would make the count an undercount
> rather than a loud failure, and this method must be revisited when that happens.
