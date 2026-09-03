# ==============================================================================
# Deltempo Checksum and Integrity Generator
# ==============================================================================

param(
    [string]$TargetDir = ""
)

$ErrorActionPreference = "Stop"

$scriptRoot = $PSScriptRoot
$projectRoot = "$scriptRoot\.."

$resolvedPath = if ([string]::IsNullOrWhiteSpace($TargetDir)) {
    "$projectRoot\publish"
} else {
    $TargetDir
}

if (-not (Test-Path $resolvedPath)) {
    Write-Error "Target directory does not exist: $resolvedPath"
    exit 1
}

Write-Host ">>> Generating SHA-256 and SHA-512 integrity manifests for: $resolvedPath" -ForegroundColor Cyan

$allFiles = Get-ChildItem -Path $resolvedPath -File | Where-Object { 
    $_.Name -ne "checksums.sha256" -and $_.Name -ne "integrity.json"
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

    Write-Host "  [OK] $($file.Name) [$sizeFormatted] -> SHA256: $sha256"
}

$sha256OutputFile = Join-Path $resolvedPath "checksums.sha256"
$sha256Lines | Out-File -FilePath $sha256OutputFile -Encoding utf8 -Force

$jsonOutputFile = Join-Path $resolvedPath "integrity.json"
$manifest | ConvertTo-Json -Depth 4 | Out-File -FilePath $jsonOutputFile -Encoding utf8 -Force

Write-Host ""
Write-Host "SUCCESS: Checksums generated successfully at $sha256OutputFile!" -ForegroundColor Green
