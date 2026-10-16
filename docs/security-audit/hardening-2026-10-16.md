# Security Hardening & Reliability Audit — 2026-10-16

**Scope:** Priorities One–Five plus Phase 6 of the Deltempo security hardening mission.
**Baseline:** `b609c5a` on `main`, 1135 tests passing.
**Final state:** 1255 tests passing, 0 failed.

---

## 1. Verification gates (actual command output, not claims)

| Gate | Command | Result |
|------|---------|--------|
| Clean rebuild | `dotnet build deltempo.sln -c Release --no-restore --no-incremental` | **Build succeeded — 0 errors, 92 warnings** |
| Format | `dotnet format deltempo.sln --verify-no-changes --no-restore` | **exit 0** |
| PR suite | `Deltempo.Tests.exe --filter "Category!=Benchmark&Category!=FuzzDeep"` | **1255 total, 0 failed, 1255 succeeded, 0 skipped (26s)** |
| Nightly deep fuzz | `Deltempo.Tests.exe --filter "Category=FuzzDeep"` | **1 total, 0 failed, 1 succeeded (3m56s)** |
| Dependency audit | `dotnet list package --vulnerable --include-transitive` | **clean** — "has no vulnerable packages" for all 4 projects, exit 0, verified through the new fail-closed logic |

Test count trajectory: **1135 → 1255 (+120)**.

### On the 92 warnings

All 92 are `xUnit1051` ("pass `TestContext.Current.CancellationToken`"). This rule is
**deliberately demoted** in `Tests/Deltempo.Tests/Deltempo.Tests.csproj` via `WarningsNotAsErrors`,
with a comment explaining why. It is the only non-fatal diagnostic — `TreatWarningsAsErrors=true`
is otherwise enforced repo-wide.

The previously published claim of "zero compiler warnings" was inaccurate. It has been corrected in
`docs/changelog/index.html`, `docs/RELEASE_ENGINEERING.md`, and `scripts/publish_release_with_assets.ps1`.

Of the 92, **44 originate in test files added or extended by this work**. They were not
mechanically "fixed", because several tests — notably the cancellation-path ones — must supply
their own token rather than `TestContext`'s. This is recorded rather than hidden.

---

## 2. Statuses

| Component | Status |
|-----------|--------|
| Phase Zero — baseline | **FIXED AND VERIFIED** |
| Priority One — update pipeline | **FIXED AND VERIFIED** (49 tests) |
| Priority Two — cleanup/deletion | **FIXED AND VERIFIED** (18 tests) |
| Priority Three — memory native API correctness | **FIXED AND VERIFIED** (80 tests) |
| Priority Four — CI/test reporting integrity | **FIXED AND VERIFIED** (6 gate fixes) |
| Priority Five — release integrity & honest docs | **FIXED AND VERIFIED** (8 fixes) |
| Phase 6 — final validation | **FIXED AND VERIFIED** |
| Production update-signing key | **BLOCKED — awaiting operator provisioning** |

---

## 3. Defects fixed, by priority

### Priority One — Update pipeline (49 tests)
Placeholder 18-byte P-256 key; verifier never called from the live path; Authenticode verification
never called; self-verification (hash compared against itself); size check passing as `0`; no
download cap; no redirect validation; no staging ACLs.
**Fixed:** explicit fail-closed trusted-key model, `kid` bound inside the signed payload, publisher
allowlist, manual redirect re-validation per hop, 200 MB cap, `SetRestrictedStagingAcl`.

**Two further critical defects found during Phase 6 follow-up** (both were present in the
"completed" Priority One work and are the reason this section has grown):

1. **The signed payload did not bind the artifact — CRITICAL.** `UpdateManifest.ToCanonicalJson()`
   serialised only `architecture`, `channel`, `commitSha`, `keyId`, `minimumSupportedWindows`,
   `product`, `publishedAtUtc`, `schemaVersion` and `version`. It omitted `artifact` and
   `cliArtifact` entirely — so the ECDSA signature authenticated *"version 9.9.9 on the stable
   channel"* and said **nothing about which file would be downloaded**. Anyone able to alter the
   manifest could repoint `downloadUrl` at their own host, set `sha256` to the digest of their own
   binary, and the signature would still verify; the download would then match its own restated
   hash. This is precisely the artifact-substitution attack the signature scheme exists to prevent,
   and the per-artifact hash check does not help because the attacker supplies both sides of it.
   **Fixed:** `artifact` and `cliArtifact` are now inside the canonical payload, with all string
   values JSON-escaped so a crafted value cannot forge structure. Locked down by
   `ManifestSignatureBindingTests` (10 tests), including an explicit substitution-attack test.

2. **No production call site loaded the provisioned key.** `TryLoadProvisionedKeyFromFile` and
   `TryLoadProvisionedKeyFromEnvironment` were only ever called from tests. The verifier therefore
   held zero keys in any real build and would have failed closed *permanently* — dropping a key on
   disk would not have been enough, because nothing in the shipping code read it.
   **Fixed:** `TryProvisionTrustedKeysFromHost()` resolves the operator key from
   `DELTEMPOMANIFEST_TRUSTED_KEY_FILE`, then `DELTEMPOMANIFEST_TRUSTED_KEY` (base64 DER), then
   `%ProgramData%\Deltempo\update-signing-key.pub`, with the id from `DELTEMPOMANIFEST_TRUSTED_KEY_ID`
   (default `deltempo-release-2026a`). It is invoked from `VerifySignature` itself rather than a
   startup hook, so no entry point can reach verification with an empty trusted set. Proven
   end-to-end by `ManifestKeyProvisioningTests` (7 tests), which drives the real resolution order.

### Priority Two — Cleanup/deletion (18 tests)
Inspection confirmed the safety engine already implemented canonical containment, reparse rejection,
TOCTOU revalidation, handle-based operations and audit records. This priority was therefore
**hardening plus adversarial tests, not a rewrite**.

### Priority Three — Memory optimizer native API (80 tests)
Twelve defects. The three that mattered most:

1. **`SystemFileCacheInformation64` had a wrong struct layout.** `CurrentSize`/`PeakSize`/
   `PageFaultCount` were declared `long`; in `winnt.h` they are 32-bit `ULONG`, and `Flags` is a
   64-bit `SIZE_T`, not `int`. Measured result: a **68-byte buffer with `MinimumWorkingSet` at
   offset 24 instead of 16**. The kernel read the caller's `PeakSize` field as the minimum working
   set, so the intended "leave cache sizing alone" (`-1`) was never delivered. Corrected to the
   64-byte WDK layout.

2. **`AccessDenied` was never assigned anywhere**, and `Unsupported` was unreachable. Every failure
   collapsed to `Failed` with "Operation rejected or privilege denied", making "run as
   Administrator" indistinguishable from "this Windows build can't do that". All 7 native helpers
   now return the raw NTSTATUS and an explicit mapping produces `Success` / `Unsupported` /
   `AccessDenied` / `Failed` with an actionable message.

3. **Docs claimed a performance win.** `README.md` asserted the APIs "genuinely release physical
   memory"; `llms-full.txt` said "Free up RAM / boost memory". Rewritten to state the real
   trade-off, with a test that fails the build on reintroduction.

**A hypothesis that proved wrong — recorded so it is not re-investigated:** I suspected
`PERFORMANCE_INFORMATION` should be 96 bytes and that the code's 104 was a bug. I tested it
empirically before changing anything: `GetPerformanceInfo(cb=96)` returns `FALSE` with
`ERROR_BAD_LENGTH(24)`, while the code's `cb=104` returns `TRUE`. The struct is correct; I had
simply forgotten the `PageSize` member. The test prevented a "fix" that would have broken memory
telemetry.

### Priority Four — CI/test reporting (6 fixes)
Every CI gate was **fail-open** — each passed when the check it claimed to perform had not run.
Each defect was reproduced with synthetic input before and after the fix. The worst: the
weak-crypto analyzer gate truncated rule counts to the top 12 *before* filtering for blocking
rules, so a single `CA5350` violation fell off the end and the gate reported success. Reproduced:
old logic → `PASSED`, new → `BLOCKED`.

### Priority Five — Release integrity (8 fixes)
`checksums.sha256` was generated **before** Authenticode signing. Signing rewrites the PE
certificate table, so the published manifest described the pre-signing binaries and could never
match what users downloaded. Moved after signing, with a self-verification pass that re-hashes
from disk.

---

## 4. BLOCKED

### Production update-signing key — **BLOCKED, awaiting operator action**

**This is now a provisioning step only — no code change is required.** (It was not, until the
Phase 6 follow-up above closed the missing call site.)

The verification path is implemented, wired into the live update path, resolves operator-provisioned
keys automatically, and is tested in both directions. What is missing is a *key*, not code:
`ManifestSignatureVerifier` deliberately embeds no key, so with zero provisioned keys it fails
closed. Correct direction, but signed updates do not work until a key exists.

To unblock:
1. Generate an ECDSA P-256 signing key.
2. Provision the public key — either drop a PEM `PUBLIC KEY` file at
   `%ProgramData%\Deltempo\update-signing-key.pub`, or set
   `DELTEMPOMANIFEST_TRUSTED_KEY_FILE` / `DELTEMPOMANIFEST_TRUSTED_KEY` (base64 DER).
   Optionally set `DELTEMPOMANIFEST_TRUSTED_KEY_ID`; it defaults to `deltempo-release-2026a`.
3. Publish manifests signed with the matching private key, carrying that same `kid`.
3. Publish manifests signed with the matching key, carrying the matching `kid`.

Until then, do not represent this control as active in production. `SECURITY.md` and
`docs/THREAT_MODEL.md` §2.5 both state this explicitly.

---

## 5. Scope deliberately not taken

- **Disk-cleanup classification vocabulary** (`"✓ SAFE • Cache"`, "Select 100% Safe") is a separate
  subsystem with its own verification model and tests keyed on the badge text. Rewriting it is a
  redesign, not a fix. Recorded, not silently altered.
- **Aggressive memory purge benchmarks** were never run against the user's machine. Memory tests
  exercise working-set trim only.
- **`soak.yml` pins `actions/upload-artifact@v4`** while `ci.yml` uses `@v7`. Not a correctness
  defect; left alone rather than changed blind.

---

## 6. Repository state

Working tree is **dirty and uncommitted on `main`** at `b609c5a`. Nothing committed, nothing
pushed. New files added by this work:

```
Tests/Deltempo.Tests/CleanupAdversarialRegressionTests.cs
Tests/Deltempo.Tests/MemoryOptimizerIntegrationTests.cs
Tests/Deltempo.Tests/MemoryOptimizerLogicTests.cs
Tests/Deltempo.Tests/NativeStructLayoutTests.cs
Tests/Deltempo.Tests/NativeStatusMappingTests.cs
Tests/Deltempo.Tests/MemoryClaimLanguageTests.cs
Tests/Deltempo.Tests/UpdatePipelineSecurityTests.cs
Tests/Deltempo.Tests/Fixtures/{README.md, *.pem, *.pub}
docs/security-audit/{baseline,hardening}-2026-10-16.md
```

`git diff` was scanned for private keys, API keys, passwords and tokens: **no matches**. The only
private key in the tree is the intentionally public test fixture, documented in
`Tests/Deltempo.Tests/Fixtures/README.md`.