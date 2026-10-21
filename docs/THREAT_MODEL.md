# Deltempo Threat Model & Security Posture

## 1. Scope & Security Boundaries

Deltempo performs high-privilege system operations, including file deletion across user profile directories, native NT kernel memory sweeps, and automated system repair commands.

This document identifies potential attack vectors, adversarial edge cases, and the concrete mitigations implemented in Deltempo.

---

## 2. Threat Analysis & Mitigations

### 2.1 Symlink, Directory Junction & Reparse Point Hijacking

* **Threat**: A low-privilege user or malicious process places an NTFS directory junction or symbolic link inside a recognized cleanup directory (e.g., `%TEMP%\exploit_junction` -> `C:\Windows\System32` or `C:\Users\Alice\Documents`). A naive recursive cleaner traversing the junction would delete critical operating system or user files.
* **Mitigation**:
  * `PathSecurity.IsReparsePointOrLink` inspects file and directory attributes.
  * `CleanupExecutor` and `CleanerService` explicitly check for `FileAttributes.ReparsePoint`.
  * The deletion engine **never traverses reparse points**. If a directory or file is flagged as a reparse point or link, it is unlinked directly without deleting target contents, or safely skipped (`CleanupErrorCategory.ReparsePointRejected`).

### 2.2 Sibling Directory Prefix Collision Attacks

* **Threat**: Exploiting path strings using naive string prefix checks (e.g., `path.StartsWith("C:\\Users\\User\\Documents")`). An adversary crafts a folder named `C:\Users\User\Documents_Fake` or `C:\Windows\System32_Evil` to trick safety rules.
* **Mitigation**:
  * Naive `StartsWith` string matching is prohibited across the codebase.
  * `PathSecurity.IsSubpathOf(path, parent)` enforces canonical path normalization and validates that paths either match exactly or are separated by standard directory separators (`Path.DirectorySeparatorChar`).

### 2.3 Path Traversal & Device Name Exploits

* **Threat**: Adversarial path inputs attempting relative escapes (`..\..\Windows\System32`), alternate data streams (`file.tmp:hidden`), or legacy DOS device names (`CON`, `PRN`, `AUX`, `NUL`).
* **Mitigation**:
  * `PathSecurity.HasPathTraversalSequences` rejects relative segment patterns.
  * `PathSecurity.NormalizeCanonicalPath` resolves fully-qualified absolute paths using `Path.GetFullPath`.
  * Canonicalization is hardened and idempotent: volume-less device prefixes (`\\?\Volume{...}`, `\\?\windows\...`) no longer resolve relative to the current directory, forward-slash device paths emitted by `Path.GetFullPath` fold to the same canonical form, rooted drive-less inputs re-resolve against the current drive, trailing separators never trim below the drive root, and Win32 dot/space-stripped segments that would resurrect `..` are rejected outright.
  * Illegal character sequences and malformed paths fail closed to `SafetyRiskTier.Protected`.
* **Verification**: `PathSecurityFuzzTests.cs` proves the canonicalizer against adversarial payloads on every CI build — a deterministic seeded campaign (traversal segments, device prefixes, ADS streams, reserved names, 8.3 short names, overlong paths, mixed separators) asserting five invariants per payload: never throws, no surviving multi-dot traversal segment, idempotent re-canonicalization, never null, blank input stays blank. Standard campaign (100k cases) runs in CI; deep campaign (1M cases, `Category=FuzzDeep`) runs locally.

### 2.4 Time-of-Check to Time-of-Use (TOCTOU) Drift

* **Threat**: A legitimate cache file is inspected during the scan phase, but before deletion occurs, the file is swapped, replaced with a hardlink, or modified to contain sensitive data.
* **Mitigation**:
  * `CleanupExecutor` verifies the file's file length against the original planned size (`ExpectedSizeBytes`).
  * If the size drifted or the file became a reparse point between planning and execution, deletion is aborted immediately with `CleanupErrorCategory.SizeDriftDetected`.

### 2.5 Update Hijacking & Tampering

* **Threat**: Malicious release binaries served via compromised CDN, DNS spoofing, or rogue repository forks.
* **Mitigation (as implemented today)**:
  * Downloads are restricted to HTTPS and to an allowlisted host + exact release path prefix (`Core/Update/UpdateSecurityValidator.cs`).
  * The staged payload is verified before it is ever executed: SHA-256 against the release manifest, plus size, PE-header and version checks (`PatchIntegrityVerifier.VerifyStagedArtifact`), and any mismatch aborts the transaction.
  * Released binaries are Authenticode-signed by the release workflow whenever a signing certificate is configured (`REQUIRE_SIGNING` can make a missing certificate a hard failure).

* **Known gaps (do not assume these are covered)**:
  * **Manifest signature verification is enforced but cannot succeed until an operator provisions a key — BLOCKED.** `ManifestSignatureVerifier` implements ECDSA P-256 (not Ed25519) and is now called from the live update path. The previous 18-byte placeholder public key has been removed entirely; trusted keys are supplied only by the operator (file or environment), and each manifest carries a `kid` that is inside the signed canonical payload. With no provisioned keys the verifier **fails closed**, so today's behaviour is "refuse to apply a signed update", not "accept an unsigned one". This is the intended failure direction, but it does mean signed updates do not work out of the box. Both the negative cases (malformed, empty, null, tampered, wrong-key, `kid`-mismatch) and a positive round-trip against a test-only key pair are covered by tests.
  * **Artifact substitution is covered, and this was a live gap.** The signature originally bound only metadata (version, channel, `keyId`), not `artifact` / `cliArtifact`. An attacker able to alter the manifest could repoint `downloadUrl`, restate `sha256` to match their own binary, and still present a valid signature — and because the attacker controls both the hash and the binary, the per-artifact hash check would pass too. `artifact` and `cliArtifact` are now part of the signed canonical payload, with all values JSON-escaped. Covered by `ManifestSignatureBindingTests`, including an explicit substitution-attack test.
  * **Key provisioning is automatic, so unblocking needs no code change.** `TryProvisionTrustedKeysFromHost()` is invoked from `VerifySignature` itself and resolves `DELTEMPOMANIFEST_TRUSTED_KEY_FILE`, then `DELTEMPOMANIFEST_TRUSTED_KEY` (base64 DER), then `%ProgramData%\Deltempo\update-signing-key.pub`, keyed by `DELTEMPOMANIFEST_TRUSTED_KEY_ID`. Invoking it from the verifier rather than a startup hook means no entry point can reach verification with an empty trusted set.
  * **Redirect handling is now explicit.** `AllowAutoRedirect = false`; each hop is re-validated against the same HTTPS + host + path-prefix + user-info + port rules as the original URL, with a 10-redirect ceiling and a 200 MB download cap.
  * **Staging ACLs are now applied.** `SetRestrictedStagingAcl` restricts the per-transaction staging directory to SYSTEM plus the current user and throws `SecurityException` rather than continuing unhardened.
  * **Authenticode is now verified in-app** against a publisher allowlist, in addition to the signature check itself.

### 2.6 Destructive Scope: AppData and Load-Bearing "Cache" Paths

* **Threat**: `AppData` is the single most destructive place a cleaner can operate. It is where
  OneDrive keeps its sync database, where Office and Outlook hold licence and credential state,
  where the Windows Credential Manager stores vaults, where the shell keeps recent-file jump
  lists, and where SSH and GnuPG agents keep key material. A path being *inside* `AppData` says
  nothing about whether it is disposable.
* **A second, subtler threat**: several paths that look exactly like build or download scratch are
  in fact load-bearing, and deleting them causes damage that is **unrecoverable without a
  reinstall** — not a lost cache, but broken machine state:
  * `%ProgramData%\Package Cache` and `%WinDir%\Installer\$PatchCache$` are the MSI source caches
    `msiexec` needs to repair and uninstall installed products. Removing them breaks
    Programs-and-Features machine-wide.
  * `%LOCALAPPDATA%\Microsoft\dotnet` is the per-user .NET SDK and runtime **install root**, not a
    cache.
  * Bare `C:\NVIDIA`, `C:\AMD`, `C:\Intel`, `C:\Realtek` are the roots a driver installer unpacks
    into; a run mid-installation deletes payload the installer has not consumed and cannot
    re-fetch.
  * `~\.cargo\registry\src` is the extracted crate source `cargo build --offline` compiles against.
  * `%LOCALAPPDATA%\Audacity\SessionData` is unsaved-session *recovery* data.
  * `%ProgramData%\Microsoft\Windows Defender\Definition Updates\Backup` holds signature rollback
    copies; `%LOCALAPPDATA%\Electronic Arts\EA Desktop\UserConfig` holds application settings.
* **Mitigation — two independent layers**:
  * Each path is **absent from the scope resolvers**, so it is never proposed as a target.
  * `ProtectionPolicy.IsProtected` **denies it independently**, so a resolver regression, a
    user-supplied rulepack, or a stale settings entry cannot reintroduce it. The security control
    is the second layer; the first is only a UX and performance decision.
  * Every refusal is covered by a `CleanerServiceTests` case asserting both that the path is
    absent *and* that the genuine scratch in the same resolver remains present, so the guard
    cannot decay into an over-broad removal.
* **Mitigation — per-directory, not per-file**:
  * A recursive `Directory.Delete(path, recursive: true)` removes an entire subtree in a single
    syscall, so a per-file `IsProtected` check never runs for any file inside it. A protected
    directory could be removed as collateral by a purge whose own target is legitimate.
  * `ProtectionPolicy.TryGetProtectedDescendant` is called by `RootLeftoverPurgeService.PurgeDirectory`
    before any recursive delete and refuses the operation when it finds a protected subtree.
  * The scan is **bounded at 5,000 directories and fails closed** on exceed: an incomplete scan
    must never authorize a recursive delete. It never traverses reparse points, so it cannot walk
    outside the AppData root, and it is skipped entirely outside AppData.
  * Markers are normalized to directory-shaped strings and matched case-insensitively. Both
    details are load-bearing: a trailing-separator marker must match the marked directory *itself*
    and not only its contents, and a case-sensitive comparison fails **open** — a differently-cased
    real path silently loses its protection. Both defects shipped, passed code review, and passed
    the build; they were found by measuring behaviour rather than by reading the code.
* **Known gaps**:
  * The AppData footprint report is read-only by construction, but it is a *measurement* surface
    and reads directory sizes across the user's profile. It runs in-process under the app's normal
    (elevated) token and applies no additional ACL or audit control of its own.
  * `StoreAppOrphanResolver` returns nothing when neither AppModel registry hive opens, rather than
    guessing. This is fail-safe, but it is silent: there is no user-visible indication that orphan
    detection was skipped because evidence was unavailable.

### 2.7 Token Privilege Minimization & Hygiene

* **Threat**: Escalated Windows token privileges (`SeDebugPrivilege`, `SeIncreaseQuotaPrivilege`, `SeProfileSingleProcessPrivilege`) retained in the running process could be abused by malware or child processes.
* **Mitigation**:
  * Privileges are acquired on-demand only for the duration of the memory call.
  * `RevertBoostPrivileges()` is called immediately following working set and cache optimization, ensuring privileges are restored to `SE_PRIVILEGE_DISABLED`.

### 2.8 CI & Release Pipeline Integrity

* **Threat**: A compromised or hijacked build dependency produces a signed Deltempo binary that users install and run with Administrator rights. The signing certificate is the trust anchor for the whole distribution channel, so a build-time compromise is strictly worse than the runtime threats above.
* **Mitigation**:
  * Every third-party GitHub Action is pinned to a full commit SHA with the version kept as a trailing comment, so a moved major tag cannot change what a workflow executes without a reviewable diff in this repository.
  * The release job's tag reaches the shell through `env:` (`TAG`), never through a `${{ }}` interpolation inside a `run:` body. Interpolating `workflow_dispatch` input directly into a script let a tag such as `v1"; whoami; "` break out of the PowerShell string and execute arbitrary code in the one job holding `SIGNING_CERTIFICATE_PFX` and `contents: write`.
  * The NuGet vulnerability audit fails closed: an audit that cannot run, or that produces no verdict line, is treated as a failure rather than as a clean result.
  * `CodeQL` runs `security-extended` over the C# sources on every push and pull request, and the results are surfaced in the repository's Security tab rather than only in the run log.
* **Known gaps**:
  * Branch protection does not apply to administrators (`enforce_admins: false`), so the required status check can be bypassed by a direct push. This is not theoretical: on 2026-10-17 a red `Build, Test & Quality Gate` reached `main` this way.
  * The release job downloads the MSIX and portable artefacts it signs from the same runner that built them, with no independent attestation of the build environment. Reproducible-build verification is not performed.
  * There is no second maintainer, so a change to the release workflow is reviewed by the same person who authored it. GitHub's approval rule does not allow self-approval, which is why `required_pull_request_reviews` cannot simply be switched on.
