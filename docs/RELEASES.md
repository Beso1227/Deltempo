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
| **GitHub Releases** | Single-File Executable (`Deltempo-vX.Y.Z-win-x64.zip`) | GitHub Release Assets | Self-contained x64 binary with embedded runtime. Includes `.sha256` checksum and `.sig` Ed25519 signature. |
| **WinGet** | Package Manifest (`Beso1227.Deltempo`) | `microsoft/winget-pkgs` | Official Windows Package Manager repository for one-line installation (`winget install deltempo`). |
| **Direct Binary** | Portable executable | Release bundle | Zero installer requirement; fully portable. |

---

## 3. Cryptographic Verification & Build Integrity

Every official release artifact is verified through automated pipelines:

1. **SHA-256 Checksums**:
   Published alongside each release in `SHA256SUMS.txt`. Users can verify integrity manually:
   ```powershell
   (Get-FileHash -Algorithm SHA256 .\Deltempo.exe).Hash
   ```
2. **Ed25519 Signatures**:
   The self-updater (`UpdateService`) checks Ed25519 cryptographic signatures using a pinned root public key before executing staged updates.
3. **Software Bill of Materials (SBOM)**:
   Generated during the CI pipeline via `dotnet list package --include-transitive --format json` to ensure full transparency of external dependencies.

---

## 4. Release Checklist for Maintainers

1. Verify working directory is clean and all tests pass:
   ```powershell
   dotnet test deltempo.sln -c Release --filter "Category!=Benchmark"
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
