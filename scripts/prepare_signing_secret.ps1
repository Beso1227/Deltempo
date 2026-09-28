<#
.SYNOPSIS
    Converts a code-signing certificate (.pfx) into the base64 secret that the
    Deltempo release workflow expects, and optionally sets it on the repository.

.DESCRIPTION
    The release workflow reads two repository secrets:
      SIGNING_CERTIFICATE_PFX        base64-encoded .pfx bytes
      SIGNING_CERTIFICATE_PASSWORD   password protecting that .pfx

    Run this once, after you obtain a certificate, to encode and upload it.

.PARAMETER PfxPath
    Path to the .pfx file exported from your certificate authority.

.PARAMETER Password
    Password for the .pfx. Prompted securely if omitted.

.PARAMETER SetSecret
    Push the encoded certificate to GitHub repository secrets.
    Requires the GitHub CLI (gh) to be installed and authenticated.

.EXAMPLE
    .\scripts\prepare_signing_secret.ps1 -PfxPath .\deltempo.pfx -SetSecret

.NOTES
    Obtain a certificate via SignPath Foundation (free, open source),
    Azure Artifact Signing (~$9.99/mo), or a traditional OV certificate
    ($150-300/yr). Self-signed certificates do NOT suppress SmartScreen.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$PfxPath,

    [Parameter(Position = 1)]
    [SecureString]$Password,

    [switch]$SetSecret
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $PfxPath)) {
    throw "Certificate not found: $PfxPath"
}

Write-Host "==> Encoding certificate" -ForegroundColor Cyan
$bytes = [System.IO.File]::ReadAllBytes((Resolve-Path $PfxPath))
$base64 = [Convert]::ToBase64String($bytes)
Write-Host "    $($bytes.Length) bytes -> $base64.Length base64 chars" -ForegroundColor DarkGray

if ($Password) {
    $plain = [System.Net.NetworkCredential]::new('', $Password).Password
}
else {
    $plain = Read-Host -AsSecureString -Prompt "PFX password"
    $plain = [System.Net.NetworkCredential]::new('', $plain).Password
}

# Sanity-check the PFX actually opens with the supplied password before
# failing halfway through an upload.
try {
    $collection = [System.Security.Cryptography.X509Certificates.X509Certificate2Collection]::new()
    $collection.Import($bytes, $plain, [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable)
    $cert = $collection[0]
    Write-Host "    Subject:  $($cert.Subject)"
    Write-Host "    Issuer:   $($cert.Issuer)"
    Write-Host "    Expires:  $($cert.NotAfter)"
    if ($cert.NotAfter -lt (Get-Date)) {
        throw "Certificate expired on $($cert.NotAfter)."
    }
    $daysLeft = [int]($cert.NotAfter - (Get-Date)).TotalDays
    if ($daysLeft -lt 30) {
        Write-Warning "Certificate expires in $daysLeft days."
    }
}
catch {
    throw "Could not open the PFX. Is the password correct? $($_.Exception.Message)"
}

# Generate the commands so they are always available to copy/paste.
Write-Host ""
Write-Host "==> Add these repository secrets" -ForegroundColor Cyan
Write-Host "    (Settings -> Secrets and variables -> Actions -> New repository secret)" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  Name: SIGNING_CERTIFICATE_PFX" -ForegroundColor Yellow
Write-Host "  Value:"
Write-Host $base64
Write-Host ""
Write-Host "  Name: SIGNING_CERTIFICATE_PASSWORD" -ForegroundColor Yellow
Write-Host "  Value: <your PFX password>"
Write-Host ""

if ($SetSecret) {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "GitHub CLI (gh) not found. Install it from https://cli.github.com/ or add the secrets manually using the values above."
    }
    Write-Host "==> Uploading to $env:GITHUB_REPOSITORY" -ForegroundColor Cyan
    $base64 | gh secret set SIGNING_CERTIFICATE_PFX --repo $env:GITHUB_REPOSITORY
    if ($LASTEXITCODE -ne 0) { throw "Failed to set SIGNING_CERTIFICATE_PFX" }

    $plain | gh secret set SIGNING_CERTIFICATE_PASSWORD --repo $env:GITHUB_REPOSITORY
    if ($LASTEXITCODE -ne 0) { throw "Failed to set SIGNING_CERTIFICATE_PASSWORD" }

    Write-Host "    Both secrets set." -ForegroundColor Green
}
else {
    Write-Host "Tip: re-run with -SetSecret (and gh auth login) to upload automatically." -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "Next: push a v* tag. The workflow will sign and verify both binaries." -ForegroundColor Green
