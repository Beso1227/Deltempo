# ==============================================================================
# Deltempo GitHub Release Helper
# ==============================================================================

[CmdletBinding()]
param (
    [string]$Tag = "v1.8.0",
    [string]$ReleaseName = "1.8.0"
)

$ErrorActionPreference = "Stop"

Write-Host "Deltempo Release Script: $ReleaseName ($Tag)" -ForegroundColor Cyan
Write-Host "Standalone release binaries located in publish directory."
