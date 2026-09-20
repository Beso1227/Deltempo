# ==============================================================================
# Deltempo GitHub Release Helper
# ==============================================================================

[CmdletBinding()]
param (
    [string]$Tag = "v1.7.5",
    [string]$ReleaseName = "1.7.5"
)

$ErrorActionPreference = "Stop"

Write-Host "Deltempo Release Script: $ReleaseName ($Tag)" -ForegroundColor Cyan
Write-Host "Standalone release binaries located in publish directory."
