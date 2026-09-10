# Deltempo Threat Model & Security Posture

## 1. Scope & Security Boundaries

Deltempo performs high-privilege system operations, including file deletion across user profile directories, native NT kernel memory sweeps, and automated system repair commands.

This document identifies potential attack vectors, adversarial edge cases, and the concrete mitigations implemented in Deltempo.

---

## 2. Threat Analysis & Mitigations

### 2.1 Symlink, Directory Junction & Reparse Point Hijacking
* **Threat**: A low-privilege user or malicious process places an NTFS directory junction or symbolic link inside a recognized cleanup directory (e.g., `%TEMP%\exploit_junction` -> `C:\Windows\System32` or `C:\Users\Alice\Documents`). A naive recursive cleaner traversing the junction would delete critical operating system or user files.
* **Mitigation**:
  - `PathSecurity.HasReparsePointsInPath` inspects directory hierarchy attributes.
  - `CleanupExecutor` and `CleanerService` explicitly check for `FileAttributes.ReparsePoint`.
  - The deletion engine **never traverses reparse points**. If a directory or file is flagged as a reparse point or link, it is unlinked directly without deleting target contents, or safely skipped (`CleanupErrorCategory.ReparsePointRejected`).

### 2.2 Sibling Directory Prefix Collision Attacks
* **Threat**: Exploiting path strings using naive string prefix checks (e.g., `path.StartsWith("C:\\Users\\User\\Documents")`). An adversary crafts a folder named `C:\Users\User\Documents_Fake` or `C:\Windows\System32_Evil` to trick safety rules.
* **Mitigation**:
  - Naive `StartsWith` string matching is prohibited across the codebase.
  - `PathSecurity.IsSubpathOf(path, parent)` enforces canonical path normalization and validates that paths either match exactly or are separated by standard directory separators (`Path.DirectorySeparatorChar`).

### 2.3 Path Traversal & Device Name Exploits
* **Threat**: Adversarial path inputs attempting relative escapes (`..\..\Windows\System32`), alternate data streams (`file.tmp:hidden`), or legacy DOS device names (`CON`, `PRN`, `AUX`, `NUL`).
* **Mitigation**:
  - `PathSecurity.HasPathTraversalSequences` rejects relative segment patterns.
  - `PathSecurity.NormalizeCanonicalPath` resolves fully-qualified absolute paths using `Path.GetFullPath`.
  - Illegal character sequences and malformed paths fail closed to `SafetyRiskTier.Protected`.

### 2.4 Time-of-Check to Time-of-Use (TOCTOU) Drift
* **Threat**: A legitimate cache file is inspected during the scan phase, but before deletion occurs, the file is swapped, replaced with a hardlink, or modified to contain sensitive data.
* **Mitigation**:
  - `CleanupExecutor` verifies the file's file length against the original planned size (`ExpectedSizeBytes`).
  - If the size drifted or the file became a reparse point between planning and execution, deletion is aborted immediately with `CleanupErrorCategory.SizeDriftDetected`.

### 2.5 Update Hijacking & Tampering
* **Threat**: Malicious release binaries served via compromised CDN, DNS spoofing, or rogue repository forks.
* **Mitigation**:
  - All automatic self-update assets must pass dual verification before staging:
    1. Cryptographic SHA-256 checksum matching the official release manifest.
    2. Ed25519 digital signature validation against Deltempo's hardcoded public root key.
  - Downloaded payloads execute from an isolated, randomized staging directory with strict user-only ACLs.

### 2.6 Token Privilege Minimization & Hygiene
* **Threat**: Escalated Windows token privileges (`SeDebugPrivilege`, `SeIncreaseQuotaPrivilege`, `SeProfileSingleProcessPrivilege`) retained in the running process could be abused by malware or child processes.
* **Mitigation**:
  - Privileges are acquired on-demand only for the duration of the memory call.
  - `RevertBoostPrivileges()` is called immediately following working set and cache optimization, ensuring privileges are restored to `SE_PRIVILEGE_DISABLED`.
