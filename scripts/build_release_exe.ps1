# ==============================================================================
# Deltempo Standalone Executable Release Builder
# ==============================================================================
# Builds self-contained single-file GUI + CLI binaries for win-x64 (primary) and
# win-arm64, then optionally packages and signs MSIX bundles.
#
#   .\scripts\build_release_exe.ps1
#   .\scripts\build_release_exe.ps1 -SignCertPath .\cert.pfx -SignCertPassword pw
#
# win-x64 is what lands at the repo root and in dist\. win-arm64 is written to
# publish_arm64 / publish_cli_arm64 as Deltempo-arm64.exe and
# deltempo_cli-arm64.exe.
# ==============================================================================

[CmdletBinding()]
param(
    # Optional Authenticode signing material, forwarded to scripts\build_msix.ps1.
    # Omit both to leave the MSIX unsigned (the portable exe is still produced).
    [string]$SignCertPath,
    [string]$SignCertPassword
)

$ErrorActionPreference = "Stop"

$projectRoot = "$PSScriptRoot\.."
Set-Location $projectRoot

Write-Host ">>> Building and Publishing Standalone Deltempo (GUI and CLI: win-x64 + win-arm64)..." -ForegroundColor Cyan

# Terminate any running instances if possible
Get-Process "deltempo_cli", "Deltempo" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300

function Safe-CopyExecutable($src, $dst) {
    if (Test-Path $dst) {
        $oldFile = "$dst.old"
        Remove-Item -Path $oldFile -Force -ErrorAction SilentlyContinue
        try {
            Move-Item -Path $dst -Destination $oldFile -Force -ErrorAction SilentlyContinue
        } catch {}
    }
    Copy-Item -Path $src -Destination $dst -Force
    Remove-Item -Path "$dst.old" -Force -ErrorAction SilentlyContinue
}

$sha = (git rev-parse HEAD).Trim()

# win-x64 is the primary/supported target and is what lands at the repo root and in
# dist/. win-arm64 is built alongside it so Windows-on-ARM devices get a native
# binary instead of running the x64 one under emulation. Both are single-file and
# self-contained; neither requires the .NET runtime on the target machine.
$architectures = @(
    @{ Rid = 'win-x64';  Suffix = 'x64' }
    @{ Rid = 'win-arm64'; Suffix = 'arm64' }
)

foreach ($arch in $architectures) {
    $outDir = if ($arch.Suffix -eq 'x64') { "$projectRoot\publish" } else { "$projectRoot\publish_$($arch.Suffix)" }

    # 1. Publish self-contained single-file GUI binary
    Write-Host ">>> Publishing GUI Standalone [$($arch.Rid)] (Deltempo.exe) with SourceRevisionId $sha..." -ForegroundColor Cyan
    dotnet publish WinTempCleaner.csproj `
        -c Release `
        -r $arch.Rid `
        --self-contained true `
        /p:PublishSingleFile=true `
        /p:IncludeNativeLibrariesForSelfExtract=true `
        /p:EnableCompressionInSingleFile=true `
        "-p:SourceRevisionId=$sha" `
        -o $outDir
    if ($LASTEXITCODE -ne 0) { throw "GUI publish failed for $($arch.Rid)." }

    if ($arch.Suffix -eq 'x64') {
        Safe-CopyExecutable "$outDir\Deltempo.exe" "$projectRoot\Deltempo.exe"
    }
    else {
        # Keep the non-primary architecture in a clearly named file so it can never be
        # mistaken for the x64 build when uploading release assets.
        Copy-Item "$outDir\Deltempo.exe" "$outDir\Deltempo-$($arch.Suffix).exe" -Force
    }
}

foreach ($arch in $architectures) {
    $cliOutDir = if ($arch.Suffix -eq 'x64') { "$projectRoot\publish_cli" } else { "$projectRoot\publish_cli_$($arch.Suffix)" }

    # 2. Publish self-contained single-file CLI binary
    Write-Host ">>> Publishing CLI Standalone [$($arch.Rid)] (deltempo_cli.exe)..." -ForegroundColor Cyan
    dotnet publish Cli/Deltempo.Cli.csproj `
        -c Release `
        -r $arch.Rid `
        --self-contained true `
        /p:PublishSingleFile=true `
        /p:IncludeNativeLibrariesForSelfExtract=true `
        /p:EnableCompressionInSingleFile=true `
        "-p:SourceRevisionId=$sha" `
        -o $cliOutDir
    if ($LASTEXITCODE -ne 0) { throw "CLI publish failed for $($arch.Rid)." }

    if ($arch.Suffix -eq 'x64') {
        Safe-CopyExecutable "$cliOutDir\deltempo_cli.exe" "$projectRoot\deltempo_cli.exe"
        Safe-CopyExecutable "$cliOutDir\deltempo_cli.exe" "$projectRoot\publish\deltempo_cli.exe"
    }
    else {
        Copy-Item "$cliOutDir\deltempo_cli.exe" "$cliOutDir\deltempo_cli-$($arch.Suffix).exe" -Force
    }
}

# Verify generated executables
$guiItem = Get-Item "$projectRoot\Deltempo.exe"
$cliItem = Get-Item "$projectRoot\deltempo_cli.exe"

Write-Host "SUCCESS: Standalone executables updated successfully!" -ForegroundColor Green
Write-Host ("   GUI Standalone: {0} ({1:N2} MB)" -f $guiItem.FullName, ($guiItem.Length / 1MB))
Write-Host ("   CLI Standalone: {0} ({1:N2} MB)" -f $cliItem.FullName, ($cliItem.Length / 1MB))

# Compute SHA-256 for release verification
$sha256 = (Get-FileHash $guiItem.FullName -Algorithm SHA256).Hash.ToLower()
Write-Host "   SHA-256 (Deltempo.exe): $sha256"
"$sha256  Deltempo.exe" | Out-File -FilePath "$projectRoot\publish\checksums.sha256" -Encoding utf8 -Force

if (Test-Path "$projectRoot\scripts\generate_checksums.ps1") {
    & "$projectRoot\scripts\generate_checksums.ps1" -TargetDir "$projectRoot\publish"
}

# 4. Synchronize dist folder
if (Test-Path "$projectRoot\dist") {
    Write-Host ">>> Synchronizing dist directory..." -ForegroundColor Cyan
    Safe-CopyExecutable "$projectRoot\publish\Deltempo.exe" "$projectRoot\dist\Deltempo.exe"
    Safe-CopyExecutable "$projectRoot\publish_cli\deltempo_cli.exe" "$projectRoot\dist\deltempo_cli.exe"
    if (Test-Path "$projectRoot\publish_arm64\Deltempo-arm64.exe") {
        Copy-Item "$projectRoot\publish_arm64\Deltempo-arm64.exe" "$projectRoot\dist\Deltempo-arm64.exe" -Force
    }
    if (Test-Path "$projectRoot\publish_cli_arm64\deltempo_cli-arm64.exe") {
        Copy-Item "$projectRoot\publish_cli_arm64\deltempo_cli-arm64.exe" "$projectRoot\dist\deltempo_cli-arm64.exe" -Force
    }
    if (Test-Path "$projectRoot\scripts\generate_checksums.ps1") {
        & "$projectRoot\scripts\generate_checksums.ps1" -TargetDir "$projectRoot\dist"
    }
}

# 5. Package the MSIX alongside the portable exe.
# The portable single-file remains the primary artefact; the MSIX is the optional
# installable channel (Start-menu entry, auto-update via the store feed).
if (Test-Path "$projectRoot\scripts\build_msix.ps1") {
    Write-Host ">>> Packaging MSIX (x64 + arm64)..." -ForegroundColor Cyan

    # Only pass signing material when it is actually supplied; otherwise build_msix.ps1
    # deliberately leaves the package unsigned rather than failing.
    $msixParams = @{ OutputDir = "$projectRoot\dist" }
    if ($SignCertPath) { $msixParams.SignCertPath = $SignCertPath }
    if ($SignCertPassword) { $msixParams.SignCertPassword = $SignCertPassword }

    foreach ($arch in $architectures) {
        $payload = if ($arch.Suffix -eq 'x64') { "$projectRoot\publish" } else { "$projectRoot\publish_$($arch.Suffix)" }
        & "$projectRoot\scripts\build_msix.ps1" -PayloadDir $payload -Architecture $arch.Suffix @msixParams
        if ($LASTEXITCODE -ne 0) { throw "MSIX packaging failed for $($arch.Suffix)." }
    }
}

Write-Host ""
Write-Host "SUCCESS: All standalone binaries are freshly built and synchronized!" -ForegroundColor Green
