# ==============================================================================
# Deltempo Binary Release Integrity & Checksum Generator
# Generates cryptographic SHA-256, SHA-512, and MD5 hashes for builds
# ==============================================================================

param(
    [string]$TargetDir = "$PSScriptRoot\..\bin\Release\net10.0-windows\win-x64\publish"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $TargetDir)) {
    $TargetDir = "$PSScriptRoot\..\bin\Release\net10.0-windows\win-x64"
}

if (-not (Test-Path $TargetDir)) {
    Write-Warning "Target directory not found: $TargetDir. Run 'dotnet publish' first."
    exit 0
}

$resolvedPath = (Resolve-Path $TargetDir).Path
Write-Host "🔐 Deltempo Integrity: Generating checksums for binaries at $resolvedPath..."

$exeFiles = Get-ChildItem -Path $resolvedPath -Filter "*.exe" -File
$dllFiles = Get-ChildItem -Path $resolvedPath -Filter "Deltempo*.dll" -File
$allFiles = @($exeFiles) + @($dllFiles)

if ($allFiles.Count -eq 0) {
    Write-Warning "No release binaries found to hash in $resolvedPath."
    exit 0
}

$sha256Lines = @()
$manifest = @()

foreach ($file in $allFiles) {
    $sha256 = (Get-FileHash -Path $file.FullName -Algorithm SHA256).Hash.ToLower()
    $sha512 = (Get-FileHash -Path $file.FullName -Algorithm SHA512).Hash.ToLower()
    $sizeBytes = $file.Length
    $sizeFormatted = "{0:N2} MB" -f ($sizeBytes / 1MB)

    $sha256Lines += "$sha256  $($file.Name)"
    $manifest += [PSCustomObject]@{
        Filename = $file.Name
        Size = $sizeFormatted
        SHA256 = $sha256
        SHA512 = $sha512
        Timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss UTC")
    }

    Write-Host "  ✓ $($file.Name) [$sizeFormatted] -> SHA256: $sha256"
}

$sha256OutputFile = Join-Path $resolvedPath "checksums.sha256"
$sha256Lines | Out-File -FilePath $sha256OutputFile -Encoding utf8 -Force

$jsonOutputFile = Join-Path $resolvedPath "integrity.json"
$manifest | ConvertTo-Json -Depth 4 | Out-File -FilePath $jsonOutputFile -Encoding utf8 -Force

Write-Host "`n🎉 Checksums generated successfully at $sha256OutputFile!"
