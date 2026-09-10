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

---

## 4. Distribution Channels

### 4.1 GitHub Releases
- Upload `Deltempo-v<version>-x64.zip` containing `Deltempo.exe` and `deltempo_cli.exe`.
- Include SHA256 checksums file `checksums.sha256`.
- Include generated `sbom-packages.json`.

### 4.2 Windows Package Manager (Winget)
Submit package manifest to [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs):
```powershell
wingetcreate new https://github.com/Beso1227/Deltempo/releases/download/v1.5.2/Deltempo.exe
```
Or validate existing manifest under `winget/manifests/b/Beso1227/Deltempo/`:
```powershell
winget validate --manifest winget/manifests/b/Beso1227/Deltempo/1.5.2
```

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
