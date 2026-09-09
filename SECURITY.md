# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.5.x   | :white_check_mark: |
| 1.4.x   | :white_check_mark: |
| 1.3.x   | :white_check_mark: |
| < 1.3   | :x:                |

## Reporting a Vulnerability

**Please do not report security vulnerabilities through public GitHub issues.**

Use GitHub's private vulnerability reporting instead:

1. Open [github.com/Beso1227/Deltempo/security/advisories/new](https://github.com/Beso1227/Deltempo/security/advisories/new).
2. Include a detailed description, reproduction steps, affected versions, and (if possible) a proof of concept.

We appreciate responsible disclosure and will investigate and resolve confirmed issues promptly.

### Security Response Commitment

- **Acknowledgment:** Within 3 business days
- **Initial triage:** Within 7 business days
- **Fix timeline:** Critical issues within 14 days, high-severity within 30 days, medium/low within 60 days
- **Disclosure:** Coordinated with reporter; public advisory published after fix is available

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

## DONE / Acknowledged Security Practices

The Deltempo project maintains the following security practices and considers them **DONE** and actively maintained:

* [x] **Public security policy** — this file defines supported versions, reporting process, response commitments, and audit targets.
* [x] **Private vulnerability reporting** — GitHub Security Advisories are enabled and monitored.
* [x] **Automated security scanning** — CodeQL security analysis runs on every push and pull request.
* [x] **Dependency monitoring** — Dependabot alerts for NuGet packages and GitHub Actions.
* [x] **Deterministic safety engine** — rule-based file classification with 24-hour safety shield, no heuristics.
* [x] **Test coverage enforcement** — 401/401 tests passing; CI enforces quality gates.
* [x] **Reproducible builds** — release workflow builds standalone self-contained executables from tagged sources.
* [x] **Zero telemetry by design** — no analytics, no tracking, no phone-home; all processing is local.
* [x] **Opt-in network features** — AI analysis and CLI registration require explicit user action.
* [x] **Open source transparency** — MIT licensed, full source available, build instructions documented.

## Code Signing

Deltempo is actively pursuing free code signing through the [SignPath Foundation](https://signpath.org/) to eliminate Windows SmartScreen warnings and provide cryptographic proof of publisher identity. Once approved, all release binaries will be signed automatically via SignPath's CI integration.

