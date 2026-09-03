# ==============================================================================
# Deltempo Standalone Executable Release Builder
# ==============================================================================

$ErrorActionPreference = "Stop"

$projectRoot = "$PSScriptRoot\.."
Set-Location $projectRoot

Write-Host ">>> Building and Publishing Standalone Deltempo (GUI and CLI win-x64)..." -ForegroundColor Cyan

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

# 1. Publish self-contained single-file GUI binary
Write-Host ">>> Publishing GUI Standalone (Deltempo.exe)..." -ForegroundColor Cyan
dotnet publish WinTempCleaner.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:EnableCompressionInSingleFile=true `
    -o "$projectRoot\publish"

Safe-CopyExecutable "$projectRoot\publish\Deltempo.exe" "$projectRoot\Deltempo.exe"

# 2. Publish self-contained single-file CLI binary
Write-Host ">>> Publishing CLI Standalone (deltempo_cli.exe)..." -ForegroundColor Cyan
dotnet publish Cli/Deltempo.Cli.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:EnableCompressionInSingleFile=true `
    -o "$projectRoot\publish_cli"

Safe-CopyExecutable "$projectRoot\publish_cli\deltempo_cli.exe" "$projectRoot\deltempo_cli.exe"
Safe-CopyExecutable "$projectRoot\publish_cli\deltempo_cli.exe" "$projectRoot\deltempo.com"

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

Write-Host ""
Write-Host "SUCCESS: All standalone binaries are freshly built and synchronized!" -ForegroundColor Green
