# ==============================================================================
# Deltempo Standalone Executable Release Builder
# ==============================================================================

$ErrorActionPreference = "Stop"

$projectRoot = "$PSScriptRoot\.."
Set-Location $projectRoot

Write-Host "🚀 Building & Publishing Standalone Deltempo.exe (win-x64)..." -ForegroundColor Cyan

# Publish self-contained single file binary
dotnet publish WinTempCleaner.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:EnableCompressionInSingleFile=true `
    -o "$projectRoot\publish"

# Check the generated executable
$exePath = "$projectRoot\publish\Deltempo.exe"
if (Test-Path $exePath) {
    $exeItem = Get-Item $exePath
    $sizeMb = "{0:N2} MB" -f ($exeItem.Length / 1MB)
    Write-Host "✅ Deltempo.exe built successfully!" -ForegroundColor Green
    Write-Host "   📁 Location: $exePath"
    Write-Host "   📦 File Size: $sizeMb"

    # Compute SHA-256
    $sha256 = (Get-FileHash $exePath -Algorithm SHA256).Hash.ToLower()
    Write-Host "   🔐 SHA-256: $sha256"

    # Save to checksums.sha256
    "$sha256  Deltempo.exe" | Out-File -FilePath "$projectRoot\publish\checksums.sha256" -Encoding utf8 -Force
} else {
    Write-Error "Deltempo.exe was not found in $projectRoot\publish."
}

# Run full checksum script
if (Test-Path "$projectRoot\scripts\generate_checksums.ps1") {
    & "$projectRoot\scripts\generate_checksums.ps1" -TargetDir "$projectRoot\publish"
}

Write-Host "`n🎉 Standalone build complete and ready for deployment!" -ForegroundColor Green
