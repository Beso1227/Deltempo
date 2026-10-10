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
  * **Manifest signatures are not enforced.** `ManifestSignatureVerifier` implements ECDSA P-256 (not Ed25519) and fails closed, but its embedded public key is still a placeholder and `VerifyManifestJson` is not called from the live update path - only from unit tests. Those tests assert that malformed, empty, null and tampered manifests are *rejected*; there is no positive test, because no real signing key is installed yet.
  * **Authenticode is not verified in-app.** `PatchIntegrityVerifier.TryVerifyAuthenticode` exists but is not called before the updater helper is launched, so the in-app update path relies on SHA-256 + transport trust rather than the publisher signature.
  * **The staging directory is not ACL-hardened.** Payloads are staged under `%ProgramData%\Deltempo\Updates\<tx>`; no explicit DACL is applied, so it inherits `%ProgramData%` defaults. `ForceDeleteEngine` and `CleanupExecutor` do set explicit ACLs on their delete targets, but the update staging tree does not.

### 2.6 Token Privilege Minimization & Hygiene

* **Threat**: Escalated Windows token privileges (`SeDebugPrivilege`, `SeIncreaseQuotaPrivilege`, `SeProfileSingleProcessPrivilege`) retained in the running process could be abused by malware or child processes.
* **Mitigation**:
  * Privileges are acquired on-demand only for the duration of the memory call.
  * `RevertBoostPrivileges()` is called immediately following working set and cache optimization, ensuring privileges are restored to `SE_PRIVILEGE_DISABLED`.
