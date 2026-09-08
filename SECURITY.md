# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.4.x   | :white_check_mark: |
| 1.3.x   | :white_check_mark: |
| < 1.3   | :x:                |

## Reporting a Vulnerability

**Please do not report security vulnerabilities through public GitHub issues.**

Use GitHub's private vulnerability reporting instead:

1. Open [github.com/Beso1227/Deltempo/security/advisories/new](https://github.com/Beso1227/Deltempo/security/advisories/new).
2. Include a detailed description, reproduction steps, affected versions, and (if possible) a proof of concept.

We appreciate responsible disclosure and will investigate and resolve confirmed issues promptly.

### High-Value Audit Targets

Deltempo runs with administrator privileges and deletes files, so the following areas carry the greatest security impact:

* **Path & boundary security** (`Core/Safety/PathSecurity.cs`): directory traversal, symlink/junction redirection, UNC, and root-containment bypasses.
* **Protection policy** (`Core/Safety/ProtectionPolicy.cs`): any bypass that would allow deletion of personal documents, credentials, source repositories, or OS components.
* **Pre-deletion revalidation** (`Core/Cleaning/CleanupExecutor.cs`): TOCTOU race conditions between plan and delete.
* **Update pipeline** (`Core/Update/`): manifest signature verification, SHA-256 integrity, host allowlisting, and transaction/rollback manipulation.

## Security Controls

* Updates are verified end-to-end: HTTPS-only, GitHub host allowlisting, SHA-256 payload digests, and ECDSA P-256 manifest signatures.
* Cleanup operations are two-phase (`SCAN → PLAN → PROTECT → REVALIDATE → CLEAN`) with reparse-point rejection and containment enforcement immediately before every destructive action.
* Shell integration (PATH, registry aliases, PowerShell profiles) is strictly opt-in via `deltempo register` and fully reversible via `deltempo unregister`.
* Online AI file analysis is **off by default** and can be disabled entirely in Settings; it transmits file metadata only, never file contents.

