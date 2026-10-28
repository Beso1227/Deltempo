# Security Policy

## Supported Versions

Security fixes are applied to the **current minor release line only**. Older lines
receive no patches, because the safety engine is a deep, interconnected part of the
codebase and a partial backport is a worse guarantee than an explicit "not supported".

| Version | Supported          | Notes                                              |
| ------- | ------------------ | -------------------------------------------------- |
| 3.2.x   | :white_check_mark: | Current stable                                     |
| 3.1.x   | :white_check_mark: | Previous stable, security fixes only               |
| 3.0.x   | :x:                | Unsupported                                        |
| 2.x     | :x:                | Unsupported                                        |
| 1.x     | :x:                | Unsupported                                        |

If you are on an unsupported line, the fix you want is in the current release.
Upgrading is the remediation; there is no patch path for you.

## Reporting a Vulnerability

**Please do not report security vulnerabilities through public GitHub issues.**

Use GitHub's private vulnerability reporting instead:

1. Open [github.com/Beso1227/Deltempo/security/advisories/new](https://github.com/Beso1227/Deltempo/security/advisories/new).
2. Include a detailed description, reproduction steps, affected versions, and (if possible) a proof of concept.

We appreciate responsible disclosure and will investigate and resolve confirmed issues promptly.

For **conduct or Code of Conduct** concerns, which are not vulnerabilities, see
[CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) § Enforcement instead.

### Security Response Commitment

- **Acknowledgment:** Within 3 business days
- **Initial triage:** Within 7 business days
- **Fix timeline:** Critical issues within 14 days, high-severity within 30 days, medium/low within 60 days
- **Disclosure:** Coordinated with reporter; public advisory published after fix is available

### High-Value Audit Targets

Deltempo runs with administrator privileges and deletes files, so the following areas carry the greatest security impact:

- **Path & boundary security** (`Core/Safety/PathSecurity.cs`): directory traversal, symlink/junction redirection, UNC, and root-containment bypasses.
- **Protection policy** (`Core/Safety/ProtectionPolicy.cs`): any bypass that would allow deletion of personal documents, credentials, source repositories, or OS components.
- **Pre-deletion revalidation** (`Core/Cleaning/CleanupExecutor.cs`): TOCTOU race conditions between plan and delete.
- **Update pipeline** (`Core/Update/`): manifest signature verification, SHA-256 integrity, host allowlisting, and transaction/rollback manipulation.

## Security Controls

- For detailed threat analysis, attack vectors, and specific defenses against symlink traversal, TOCTOU, and privilege escalation, see [Deltempo Threat Model](docs/THREAT_MODEL.md).
- Updates are verified before execution: HTTPS-only, GitHub host allowlisting, and a SHA-256 payload digest checked against the `checksums.sha256` release asset, plus size, PE-header and version checks.
- **Manifest signature verification is enforced.** Every downloaded manifest is verified with ECDSA P-256 (SHA-256) against an explicitly provisioned trusted-key set before its digest is trusted. `kid` binds each manifest to the key that must have signed it, and the key id is inside the signed canonical payload, so a manifest cannot be re-pointed at a different key.
  - **Trusted keys are fail-closed, and there are none in the repository.** No public key is embedded in the source. An operator provisions a real signing key, resolved automatically in this order: a PEM file named by `DELTEMPOMANIFEST_TRUSTED_KEY_FILE`, then base64 DER in `DELTEMPOMANIFEST_TRUSTED_KEY`, then `%ProgramData%\Deltempo\update-signing-key.pub`. The key id comes from `DELTEMPOMANIFEST_TRUSTED_KEY_ID` (default `deltempo-release-2026a`) and must match the manifest's `kid`. With zero trusted keys, verification **fails** rather than passing unchecked. This is deliberate: shipping a placeholder key that "verifies" is worse than refusing to update.
  - **The signature binds the artifact, not just the version.** `artifact` and `cliArtifact` — including `downloadUrl`, `sha256` and `sizeBytes` — are inside the signed canonical payload. A manifest that keeps every signed field intact but repoints the download and restates the hash does not verify.
  - **Status: BLOCKED — awaiting production signing infrastructure.** The verification path is implemented, wired into the live update path, and resolves operator keys automatically; **no code change is needed to unblock it.** What is missing is a key: no production key has been provisioned, so in the current build every signed-manifest verification fails closed. Until an operator provisions a key and publishes manifests signed with the matching `kid`, do not rely on this control in production.
- **Authenticode publisher verification is enforced** against a publisher allowlist (`CN=Deltempo`, `CN=Beso1227`, `CN=Deltempo Project`) in addition to the signature check.
- **Redirects are followed manually** with `AllowAutoRedirect = false`, so every hop is re-validated by the same host/target rules as the original URL. Downloads are capped at 200 MB. The staging directory is ACL-restricted to SYSTEM plus the current user before any payload is written.
- Cleanup operations are two-phase (`SCAN → PLAN → PROTECT → REVALIDATE → CLEAN`) with reparse-point rejection and containment enforcement immediately before every destructive action.
- Shell integration (PATH, registry aliases, PowerShell profiles) is strictly opt-in via `deltempo register` and fully reversible via `deltempo unregister`.
- Online AI file analysis is **off by default** and can be disabled entirely in Settings; it transmits file metadata only, never file contents.

## DONE / Acknowledged Security Practices

The Deltempo project maintains the following security practices and considers them **DONE** and actively maintained:

- [x] **Public security policy** — this file defines supported versions, reporting process, response commitments, and audit targets.
- [x] **Private vulnerability reporting** — GitHub Security Advisories are enabled and monitored.
- [x] **Automated security scanning** — CodeQL security analysis runs on every push and pull request.
- [x] **Dependency monitoring** — Dependabot alerts for NuGet packages and GitHub Actions.
- [x] **Deterministic safety engine** — rule-based file classification with 24-hour safety shield, no heuristics.
- [x] **Test coverage enforcement** — 1475/1475 tests passing; CI enforces quality gates.
- [x] **Reproducible builds** — release workflow builds standalone self-contained executables from tagged sources.
- [x] **Zero telemetry by design** — no analytics, no tracking, no phone-home; all processing is local.
- [x] **Opt-in network features** — AI analysis and CLI registration require explicit user action.
- [x] **Open source transparency** — MIT licensed, full source available, build instructions documented.

## Code Signing

Release binaries are **not currently Authenticode-signed**. Until a certificate
provider is in place, Windows SmartScreen may warn end users, and the publisher
will be shown as `Unknown`.

The release pipeline already supports signing: `release.yml` fails the build if
signing is expected but no certificate is configured, so an unsigned release
cannot ship by accident. See [`docs/SIGNING.md`](docs/SIGNING.md) for available
options and the current limitations of SmartScreen reputation.
