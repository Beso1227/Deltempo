# ==============================================================================
# Deltempo Store package - local install test
# ==============================================================================
# Proves the Store-flavor .msix actually installs and runs, which the packaging
# pipeline alone cannot: an unsigned package will not sideload, and "the publish
# folder starts" says nothing about package identity.
#
#   .\tools\test-store-package.ps1                 # throwaway identity + cert
#   .\tools\test-store-package.ps1 -KeepInstalled  # leave it installed to poke
#
# What it does:
#   1. Creates (or reuses) a self-signed code-signing certificate whose subject
#      equals the package Publisher. A self-signed cert can carry ANY subject,
#      including the real Partner Center "CN=<id>" once reserved - so this same
#      script can later test the exact submission identity.
#   2. Builds the package with tools/build-store-package.ps1 using the test
#      identity, then signs it with signtool from the SDK BuildTools NuGet.
#   3. Requires the public half of the cert in LocalMachine\TrustedPeople - the
#      ONLY store Add-AppxPackage accepts for a non-Store signature (CurrentUser
#      was verified insufficient: 0x800B0109). When that is missing it stops with
#      the two exact elevated commands instead of a deployment error.
#   4. Installs, launches, records what it observed, and removes it again unless
#      -KeepInstalled.
#
# What it CANNOT assert (do these by hand while it is installed):
#   - scan/clean of standard paths runs unelevated;
#   - System Repair and restore-point cleaning still prompt for elevation;
#   - the tray icon and its flyout appear above the taskbar.
#
# The artifact this produces is a TEST package. Never upload it: the submission
# package is unsigned and carries the Partner Center identity.
#
# To undo the one-time trust step (the cert stays trusted until you remove it):
#   Get-ChildItem Cert:\LocalMachine\TrustedPeople |
#       Where-Object { $_.Subject -eq 'CN=Deltempo Store Test' } | Remove-Item
# ==============================================================================

[CmdletBinding()]
param(
    # Test identity. Distinct from the real one on purpose so a test install can
    # never collide with a Store-installed copy.
    [string]$IdentityName = "DeltempoStoreTest",

    # Must equal the signing certificate subject exactly.
    [string]$Publisher = "CN=Deltempo Store Test",

    [string]$DisplayName = "Deltempo (Store test)",

    [ValidateSet("x64", "arm64")]
    [string]$Architecture = "x64",

    # Forwarded to the build script.
    [string]$PublishDir = "",
    [switch]$SkipPublish,

    # Leave the package installed after the run (for manual checks).
    [switch]$KeepInstalled
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$workDir = Join-Path $repoRoot ".internal\store-test"
$outDir = Join-Path $repoRoot "packaging\Store\out"
New-Item -ItemType Directory -Force -Path $workDir | Out-Null

$pfxPath = Join-Path $workDir "deltempo-store-test.pfx"
# Throwaway credential for a throwaway certificate that never leaves
# .internal\ (gitignored). The submission package is never signed by us.
$pfxPassword = "deltempo-store-test"

function Resolve-Signtool {
    $roots = @()
    $msbuildFolders = dotnet msbuild (Join-Path $repoRoot "src\Deltempo.App.WinUI\Deltempo.App.WinUI.csproj") `
        -getProperty:NuGetPackageFolders -nologo 2>$null
    if ($LASTEXITCODE -eq 0 -and $msbuildFolders) {
        $roots += (($msbuildFolders -join "`n") -split ';' | Where-Object { $_ })
    }
    if ($env:NUGET_PACKAGES) { $roots += $env:NUGET_PACKAGES }
    $locals = dotnet nuget locals global-packages --list 2>$null
    if ($LASTEXITCODE -eq 0 -and $locals) {
        $roots += (($locals | Select-Object -First 1) -replace '^global-packages:\s*', '')
    }
    $roots += (Join-Path $env:USERPROFILE ".nuget\packages")
    $roots += (Join-Path $repoRoot ".nuget\packages")

    foreach ($root in ($roots | Where-Object { $_ } | Select-Object -Unique)) {
        $package = Get-ChildItem (Join-Path $root "microsoft.windows.sdk.buildtools") `
            -Directory -ErrorAction SilentlyContinue | Sort-Object Name | Select-Object -Last 1
        if ($null -eq $package) { continue }
        $exe = Get-ChildItem (Join-Path $package.FullName "bin") -Recurse -Filter "signtool.exe" `
            -ErrorAction SilentlyContinue | Where-Object { $_.Directory.Name -eq "x64" } | Select-Object -First 1
        if ($null -ne $exe) { return $exe.FullName }
    }
    throw "signtool.exe not found under Microsoft.Windows.SDK.BuildTools; run dotnet restore first."
}

function Get-OrCreateTestCertificate {
    param([string]$Subject, [string]$PfxPath, [string]$Password)

    if (Test-Path $PfxPath) {
        try {
            return [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
                $PfxPath, $Password,
                [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable)
        } catch {
            Remove-Item $PfxPath -Force
        }
    }

    $rsa = $null
    try { $rsa = [System.Security.Cryptography.RSA]::Create(2048) } catch { }
    if ($null -eq $rsa) {
        $rsa = New-Object System.Security.Cryptography.RSACryptoServiceProvider 2048
    }

    $request = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
        $Subject,
        $rsa,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)

    # Code Signing EKU (1.3.6.1.5.5.7.3.3) - an appx signature is rejected
    # without it, even when the chain is trusted.
    $eku = New-Object System.Security.Cryptography.OidCollection
    [void]$eku.Add((New-Object System.Security.Cryptography.Oid "1.3.6.1.5.5.7.3.3"))
    $request.CertificateExtensions.Add(
        [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($eku, $false))
    $request.CertificateExtensions.Add(
        [System.Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new(
            [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature, $false))

    $certificate = $request.CreateSelfSigned(
        [DateTimeOffset]::Now.AddDays(-1), [DateTimeOffset]::Now.AddYears(1))

    [System.IO.File]::WriteAllBytes(
        $PfxPath,
        $certificate.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $Password))

    return [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
        $PfxPath, $Password,
        [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable)
}

function Test-IsElevated {
    $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object System.Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Assert-InstallTrust {
    param(
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate,
        [string]$CerPath,
        [string]$MsixPath
    )

    # Add-AppxPackage only accepts a signature chained to the LOCAL MACHINE
    # TrustedPeople store. CurrentUser is not enough - verified on this machine
    # as 0x800B0109 ("root certificate ... must be trusted"), so this gate exists
    # instead of a confusing deployment error.
    $store = [System.Security.Cryptography.X509Certificates.X509Store]::new(
        "TrustedPeople",
        [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
    $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly)
    $trusted = $false
    foreach ($existing in $store.Certificates) {
        if ($existing.Thumbprint -eq $Certificate.Thumbprint) { $trusted = $true; break }
    }
    $store.Close()
    if ($trusted) { return "LocalMachine\TrustedPeople (already trusted)" }

    if (Test-IsElevated) {
        $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $store.Add($Certificate)
        $store.Close()
        return "LocalMachine\TrustedPeople (added)"
    }

    $message = ("The test certificate is not trusted for installation, and trusting it needs " +
        "elevation once. Run this in an elevated PowerShell, then re-run this script:`n" +
        "  Import-Certificate -FilePath '{0}' -CertStoreLocation Cert:\LocalMachine\TrustedPeople`n" +
        "Only the import needs elevation - the script installs, launches, observes and " +
        "removes the package unelevated. (`Add-AppxPackage -Path '{1}'` also works from " +
        "that elevated shell if you want to poke at it by hand first.)")
    throw ($message -f $CerPath, $MsixPath)
}

# ------------------------------------------------------------------------------
# 1. Certificate (created or reused) and its public half exported for the
#    one-time elevated trust step.
# ------------------------------------------------------------------------------
$cerPath = Join-Path $workDir "deltempo-store-test.cer"
$certificate = Get-OrCreateTestCertificate -Subject $Publisher -PfxPath $pfxPath -Password $pfxPassword
[System.IO.File]::WriteAllBytes(
    $cerPath,
    $certificate.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert))
Write-Host ("Certificate: {0} (thumbprint {1})" -f $certificate.Subject, $certificate.Thumbprint)

# ------------------------------------------------------------------------------
# 2. Build the test package (identity rewritten at pack time)
# ------------------------------------------------------------------------------
$buildArgs = @{
    IdentityName  = $IdentityName
    Publisher     = $Publisher
    DisplayName   = $DisplayName
    Architecture  = $Architecture
}
if ($PublishDir) { $buildArgs["PublishDir"] = $PublishDir }
if ($SkipPublish) { $buildArgs["SkipPublish"] = $true }

& (Join-Path $PSScriptRoot "build-store-package.ps1") @buildArgs

$msix = Get-ChildItem $outDir -Filter "*.msix" -File | Select-Object -First 1
if ($null -eq $msix) { throw "No .msix under $outDir after the build." }

# ------------------------------------------------------------------------------
# 3. Sign with the test certificate
# ------------------------------------------------------------------------------
$signtool = Resolve-Signtool
& $signtool sign /fd SHA256 /f $pfxPath /p $pfxPassword /d "Deltempo Store test package" $msix.FullName
if ($LASTEXITCODE -ne 0) { throw "signtool sign failed ($LASTEXITCODE)." }
Write-Host ("Signed: {0}" -f $msix.Name)

# ------------------------------------------------------------------------------
# 3b. Install trust gate (after signing: the command it prints names the .msix)
# ------------------------------------------------------------------------------
$trustedAt = Assert-InstallTrust -Certificate $certificate -CerPath $cerPath -MsixPath $msix.FullName
Write-Host ("Install trust: {0}" -f $trustedAt)

# ------------------------------------------------------------------------------
# 4. Install, launch, observe
# ------------------------------------------------------------------------------
$crashLog = Join-Path $env:LOCALAPPDATA "Deltempo\crash.log"
$crashBefore = if (Test-Path $crashLog) { (Get-Item $crashLog).LastWriteTimeUtc } else { [DateTime]::MinValue }

$installed = $null
try {
    Add-AppxPackage -Path $msix.FullName
    $installed = Get-AppxPackage -Name $IdentityName | Select-Object -First 1
    if ($null -eq $installed) { throw "Add-AppxPackage returned but $IdentityName is not registered." }
    Write-Host ("Installed: {0} ({1})" -f $installed.PackageFullName, $installed.PackageFamilyName)

    Start-Process "explorer.exe" -ArgumentList ("shell:AppsFolder\{0}!DeltempoWinUI" -f $installed.PackageFamilyName)
    Start-Sleep -Seconds 10

    $process = Get-Process -Name "Deltempo.WinUI" -ErrorAction SilentlyContinue | Select-Object -First 1
    $crashed = (Test-Path $crashLog) -and ((Get-Item $crashLog).LastWriteTimeUtc -gt $crashBefore)

    # Package identity may redirect %LOCALAPPDATA% writes into the package's
    # LocalCache. Whichever path exists is the one the privacy policy and the
    # uninstall-leftover decision have to name.
    $roamingPath = Join-Path $env:LOCALAPPDATA "Deltempo"
    $packagePath = Join-Path $env:LOCALAPPDATA ("Packages\{0}\LocalCache\Local\Deltempo" -f $installed.PackageFamilyName)

    $dataPath = if (Test-Path $packagePath) { $packagePath }
                elseif (Test-Path $roamingPath) { $roamingPath }
                else { "(none written yet - run a scan, then re-check)" }

    Write-Host ""
    Write-Host "==================== install test report ===================="
    Write-Host ("package          : {0}" -f $installed.PackageFullName)
    Write-Host ("architecture     : {0}" -f $installed.Architecture)
    Write-Host ("launch           : {0}" -f $(if ($null -ne $process) { "running (pid $($process.Id))" } else { "NOT RUNNING" }))
    Write-Host ("crash log        : {0}" -f $(if ($crashed) { "NEW ENTRY - investigate before submitting" } else { "unchanged" }))
    Write-Host ("data folder      : {0}" -f $dataPath)
    Write-Host "============================================================="
    Write-Host ""
    Write-Host "Manual checks while it is installed:"
    Write-Host "  - scan/clean of standard paths runs without elevation"
    Write-Host "  - System Repair / restore-point cleaning prompt for elevation"
    Write-Host "  - tray icon and flyout appear above the taskbar"
    Write-Host "  - a second launch defers to the running copy"

    if ($null -eq $process) {
        throw "The packaged app did not stay running; see the crash log above."
    }
    if ($crashed) {
        throw "The packaged app wrote a new crash.log entry."
    }
}
finally {
    if ($null -ne $installed) {
        Get-Process -Name "Deltempo.WinUI" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        if ($KeepInstalled) {
            Write-Host ("Left installed: {0}" -f $installed.PackageFullName)
        } else {
            Remove-AppxPackage -Package $installed.PackageFullName
            Write-Host ("Removed: {0}" -f $installed.PackageFullName)
        }
    }
}
