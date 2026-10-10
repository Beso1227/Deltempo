# Deltempo Release Engineering & Distribution Guide

## 1. Versioning & Tagging Policy

Deltempo follows Semantic Versioning (`MAJOR.MINOR.PATCH`):

* **MAJOR**: Architectural changes, breaking CLI arguments, or schema modifications.
* **MINOR**: New cleanup scopes, performance enhancements, or new repair modules.
* **PATCH**: Bug fixes, security hardening, or updated protection lists.

All official releases are tagged in git using `vX.Y.Z` and triggered through GitHub Actions.

---

## 2. Release Artifacts & Distribution Channels

| Channel | Format | Destination | Description |
| :--- | :--- | :--- | :--- |
| **GitHub Releases** | Standalone executables (`Deltempo.exe`, `deltempo_cli.exe`, plus ARM64 builds and MSIX packages) | GitHub Release Assets | Self-contained binaries with embedded runtime. Every artifact is Authenticode-signed when a signing certificate is configured, and a `checksums.sha256` manifest covering the portable executables is attached to the release. |
| **Direct Binary** | Portable executable | Release bundle | Zero installer requirement; fully portable. |
| **Terminal One-Liner** | PowerShell bootstrap script | `https://beso1227.github.io/Deltempo/win` | `irm .../win \| iex` downloads, SHA-256 verifies, caches, and launches the latest release. |

---

## 3. Cryptographic Verification & Build Integrity

Every official release artifact is verified through automated pipelines:

1. **SHA-256 Checksums**:
   Published alongside each release in `SHA256SUMS.txt`. Users can verify integrity manually:

   ```powershell
   (Get-FileHash -Algorithm SHA256 .\Deltempo.exe).Hash
   ```

2. **Authenticode Signing**:
   Release artifacts are signed with the configured code-signing certificate (`signtool /fd sha256 /tr <timestamp>`), and the workflow runs `signtool verify /pa` on each artifact before it is uploaded. Set `REQUIRE_SIGNING=true` to make a missing certificate a hard failure instead of a warning.
   A detached `.sig` manifest is **not** currently produced, and manifest signature verification is not yet enforced in-app — see `docs/THREAT_MODEL.md` §2.5.
3. **Software Bill of Materials (SBOM)**:
   Generated during the CI pipeline via `dotnet list package --include-transitive --format json` to ensure full transparency of external dependencies.

---

## 4. Release Checklist for Maintainers

1. Verify working directory is clean and all tests pass:

   ```powershell
   dotnet build Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release
   ./Tests/Deltempo.Tests/bin/Release/net10.0-windows/Deltempo.Tests.exe --filter "Category!=Benchmark"
   ```

2. Verify zero high or critical NuGet vulnerabilities:

   ```powershell
   dotnet list package --vulnerable --include-transitive
   ```

3. Update version number in `Directory.Build.props`.
4. Update `docs/changelog/index.html` with release notes and highlights.
5. Create and push signed git tag:

   ```bash
   git tag -s vX.Y.Z -m "Release vX.Y.Z"
   git push origin vX.Y.Z
   ```

6. Verify CI build completion, download generated binaries, and verify SHA-256 signatures before publishing the GitHub release.
