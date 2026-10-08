# Deltempo Release Engineering & Security Trust Boundaries

This document defines the release engineering, verification gates, security trust boundaries, and incident response/rollback procedures for **Deltempo**.

---

## 1. Security Architecture & Trust Boundaries

Deltempo is a high-privilege system utility operating on Windows NT. It must strictly maintain deterministic trust boundaries across all release and runtime operations:

### 1.1 Deletion & TOCTOU Hardening

- **Root Containment:** All cleanup actions require explicit canonical root boundaries. Symlinks, directory junctions, and reparse points pointing outside designated roots are rejected (`PathEscapedRoot`, `ReparsePointDetected`).
- **TOCTOU Pre-Deletion Revalidation:** Files undergo a two-phase check. Immediately prior to unlinking, `CleanupExecutor.RevalidateBeforeDeletion` verifies size consistency, timestamp drift (`< 5s`), lack of `FileAttributes.System`, and policy compliance.
- **Audit Records:** Every attempted or executed deletion logs an immutable `DeletionAuditRecord` detailing path, file size, safety risk tier, matched rule, and verification status.

### 1.2 Self-Update Trust Boundaries

- **Hash Integrity:** Updates downloaded over HTTPS must match SHA-256 digests published in signed release metadata.
- **Elevation Boundary:** Writes to protected locations (e.g. `%ProgramFiles%`, system services) require verified UAC elevation before payload staging.
- **Isolated Staging:** Temporary update downloads are staged in isolated user directories (`%LocalAppData%\Deltempo\Updates\staging\<guid>`) with restrictive ACLs, preventing multi-user race tampering.
- **Transaction Journaling:** The `UpdateTransactionCoordinator` writes a disk-backed journal before modifying any binary. In-place replacements maintain atomic `.bak` fallbacks (`File.Replace`).
- **Automated Rollback:** If a newly staged binary fails health verification or process bootstrap, the transaction coordinator restores the `.bak` binary immediately and logs the failure reason.

### 1.3 Secret & Credential Handling

- Persisted secrets (e.g., AI API keys) must **never** be saved in plaintext on disk.
- Keys are protected via Windows DPAPI (`DataProtectionScope.CurrentUser`) with application-specific entropy (`dpapi:<base64>`).
- If DPAPI is unavailable, the application fails closed rather than persisting credentials in plaintext.

---

## 2. Release Engineering Workflow

### 2.1 Centralized Versioning

Version numbers are centralized in `Directory.Build.props`. Individual `.csproj` files do not specify version properties.

```xml
<PropertyGroup>
  <Version>1.5.2</Version>
  <FileVersion>1.5.2.0</FileVersion>
  <AssemblyVersion>1.5.2.0</AssemblyVersion>
</PropertyGroup>
```

### 2.2 Quality Gates & Verification Checklist

Before any release artifact is built or distributed:

1. **Full Clean Build:**

   ```powershell
   dotnet clean deltempo.sln
   dotnet build deltempo.sln -c Release
   ```

2. **Quality Gates Enforced:**
   - `<Nullable>enable</Nullable>`
   - `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` (0 warnings allowed)
3. **Automated Test Suite:**

   ```powershell
   dotnet test deltempo.sln -c Release --no-build --filter "Category!=Benchmark"
   ```

   *Verification:* 100% tests must pass (0 failures, 0 skips).
4. **Vulnerability Audit:**

   ```powershell
   dotnet list package --vulnerable --include-transitive
   ```

5. **Software Bill of Materials (SBOM):**

   ```powershell
   dotnet list deltempo.sln package --include-transitive --format json | Out-File -FilePath "artifacts/sbom-packages.json" -Encoding utf8
   ```

---

## 3. Artifact Build & Publishing

### 3.1 GUI Executable (Self-Contained Single-File)

```powershell
dotnet publish WinTempCleaner.csproj -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:PublishReadyToRun=true `
  -o ./publish
```

### 3.2 CLI Executable (Self-Contained Single-File)

```powershell
dotnet publish Cli/Deltempo.Cli.csproj -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:PublishReadyToRun=true `
  -o ./publish_cli
```

### 3.3 Authenticode Code Signing

For official public distribution, sign binaries using a trusted code signing certificate:

```powershell
signtool sign /tr http://timestamp.digicert.com /td sha256 /fd sha256 /a ./publish/Deltempo.exe
signtool sign /tr http://timestamp.digicert.com /td sha256 /fd sha256 /a ./publish_cli/deltempo_cli.exe
```

CI signs every portable artefact (`Deltempo.exe`, `deltempo_cli.exe`, and both `-arm64`
binaries) and fails the release unless a certificate is present. Opting out requires
setting the `REQUIRE_SIGNING` repository variable to `false` explicitly — a missing
secret is never silently tolerated.

To move to **Azure Trusted Signing** (no certificate file on the runner), replace the
`Authenticode Sign Release Binaries` step with the Azure Artifact Signing action and
delete the `SIGNING_CERTIFICATE_PFX` secret. The Ed25519 + SHA256 manifest verification
in `Core/Update/ManifestSignatureVerifier.cs` is independent of Authenticode and stays
as-is: it is what makes the self-updater trust a release, not the binary signature.

### 3.4 Architectures (x64 + ARM64)

`WinTempCleaner.csproj` and `Cli/Deltempo.Cli.csproj` both declare
`<RuntimeIdentifiers>win-x64;win-arm64</RuntimeIdentifiers>`.

- **win-x64** is the primary target: it lands at the repo root, in `dist/`, and is the
  URL the terminal one-liner downloads.
- **win-arm64** ships as `Deltempo-arm64.exe` / `deltempo_cli-arm64.exe` so Windows-on-ARM
  runs natively instead of emulating x64. The arch suffix is mandatory in the filename —
  shipping a second file named `Deltempo.exe` would make the two indistinguishable in the
  release asset list.

### 3.5 MSIX Packaging

`scripts/build_msix.ps1` stages the published single-file GUI together with
`packaging/msix/AppxManifest.xml` and packs an installable `.msix`:

```powershell
# x64
.\scripts\build_msix.ps1 -PayloadDir .\publish -OutputDir .\dist -Architecture x64 `
    -Publisher "CN=Beso1227" -SignCertPath .\cert.pfx -SignCertPassword pw

# arm64
.\scripts\build_msix.ps1 -PayloadDir .\publish_arm64 -OutputDir .\dist -Architecture arm64
```

- The seven required tile/logo PNGs are rasterised from `app.ico` at pack time
  (`New-MsixAsset`), so no pre-scaled binaries are committed.
- The manifest declares `rescap:runFullTrust` and `Identity Version` is patched through the
  XML DOM rather than a textual replace, because a naive `Version="..."` regex also matches
  inside `MinVersion="10.0.17763.0"` and would corrupt the OS floor.
- `makeappx.exe` comes from the Windows SDK and is present on the GitHub `windows-latest`
  runner. On a machine without it, `-StageOnly` stops after staging and manifest patching.
- **MSIX is the optional channel.** The portable single-file remains primary; the MSIX adds a
  Start-menu entry and store-managed updates.

### 3.6 CLI NativeAOT Trial — Result: Not Adopted

`PublishAot=true` was trialled against `Cli/Deltempo.Cli.csproj` for faster automation
startup, as planned. **It does not build and is not enabled.** Measured result:

```
error NETSDK1168: WPF is not supported or recommended with trimming enabled
```

`UseWPF` was removed from the CLI project (it had zero `System.Windows.*` usings), which
cleared NETSDK1168 and surfaced the real blocker: **61 IL3050/IL2026 trim warnings as
errors across 18 files**, all reached because `Deltempo.Core.csproj` compiles
`Services/**` via `<Compile Include="..\Services\**\*.cs" />`:

| Site | Count |
|---|---|
| Reflection-based `JsonSerializer.Serialize`/`Deserialize` | ~59 |
| `Marshal.SizeOf(Type)` in `CleanerService.cs`, `MemoryOptimizerService.Native.cs` | 2 |

`MemoryOptimizerService.Native.cs` contains the `ntdll!NtSetSystemInformation` working-set
path and is explicitly out of scope for AOT/trimming work. Converting Core to be fully
AOT-clean means source-generated `JsonSerializerContext` types for every one of the ~59
call sites (rulepacks, settings, intelligence caches, transaction journal) plus reworking
the two marshalling sites — a separate, testable refactor, not a packaging change.

**The CLI therefore keeps `PublishReadyToRun`.** R2R already removes JIT warm-up for the
automation path without requiring Core to be trim-safe. Re-run the trial after Core is
source-gen clean:

```powershell
dotnet publish Cli/Deltempo.Cli.csproj -c Release -r win-x64 -p:PublishAot=true --self-contained true
```

The WPF head is never AOT'd — `WinTempCleaner` is not trimmable by design.

---

## 4. Distribution Channels

### 4.1 GitHub Releases

- Upload `Deltempo-v<version>-x64.zip` containing `Deltempo.exe` and `deltempo_cli.exe`.
- Include SHA256 checksums file `checksums.sha256`.
- Include generated `sbom-packages.json`.

### 4.2 Winget

`packaging/winget/` holds the three manifests winget requires:

| File | Role |
|---|---|
| `Beso1227.Deltempo.yaml` | version pin |
| `Beso1227.Deltempo.installer.yaml` | per-architecture URLs + digests |
| `Beso1227.Deltempo.locale.en-US.yaml` | metadata, description, license |

The `Generate Winget Manifests` release step fills the `InstallerSha256` placeholders from
the freshly built artefacts and pins `PackageVersion` to the tag, so a submitted manifest can
never reference a digest that differs from what was published. It hard-fails if any
`REPLACE_WITH_SHA256` placeholder survives. The pinned bundle is attached to the release as
`winget-manifests-<version>.zip`, ready to open a PR against `microsoft/winget-pkgs`.

```powershell
winget install Beso1227.Deltempo
```

Installer type is `portable`, so winget shims the single-file exe onto PATH rather than
running an installer.

### 4.3 MSIX

`.msix` packages are attached to the release as `Deltempo-<version>-<arch>.msix`. They are
an optional convenience channel — the portable executable stays primary and the updater
keeps verifying releases through `checksums.sha256` + the Ed25519 manifest signature, so an
MSIX install never becomes a new trust anchor.

### 4.4 Terminal One-Liner

Served from GitHub Pages as two extensionless PowerShell scripts: `docs/win` (GUI) and `docs/win-cli` (headless CLI).

- `docs/.nojekyll` disables Jekyll, so both are served raw as `application/octet-stream`. `Invoke-RestMethod` still returns a string for that content type, which is what makes `| iex` work — verified against PowerShell 7.6.6.
- Bootstrap flow: read `checksums.sha256` → download the chosen binary → verify SHA-256 → cache in `%LOCALAPPDATA%\Deltempo\bin` → launch.
- Trust anchor, in order of preference: the `checksums.sha256` release asset (authoritative, attached by the `Upload Checksum Manifest` step), then `docs/checksums.sha256` published on Pages as a bootstrap fallback. A missing manifest or a digest mismatch aborts the install; verification is never skipped or downgraded to a warning.
- `Invoke-Expression` accepts no arguments, so `win-cli` passes the target through `DELTEMPO_TARGET`. `win` consumes that variable, so a later run defaults back to the GUI.
- Re-running the command always pulls the newest published release.

---

## 5. Rollback Procedures & Incident Response

### 5.1 Automated Client Rollback

When an update is applied:

1. Current running binary is renamed to `Deltempo.exe.bak`.
2. New binary is moved to `Deltempo.exe`.
3. If new binary fails initial process launch or self-test verification, `UpdateTransactionCoordinator` rolls back:
   - Terminates spawned child process.
   - Restores `Deltempo.exe.bak` -> `Deltempo.exe`.
   - Records rollback in `%LocalAppData%\Deltempo\logs\updater_journal.log`.

### 5.2 Manual Rollback Procedure

If an issue occurs on end-user machines:

1. Close all running instances of Deltempo:

   ```powershell
   Stop-Process -Name "Deltempo", "deltempo_cli" -Force -ErrorAction SilentlyContinue
   ```

2. In the application directory:

   ```powershell
   Copy-Item -Force Deltempo.exe.bak Deltempo.exe
   ```

3. Settings and telemetry state remain intact in `%LocalAppData%\Deltempo\settings.json`.

### 5.3 Post-Mortem Diagnostics

When investigating failures:

- Application crash dumps: `%LocalAppData%\Deltempo\logs\crash_*.log`
- Diagnostics trace logs: `%LocalAppData%\Deltempo\logs\trace.log`
- Transaction journal: `%LocalAppData%\Deltempo\Updates\journal.json`
