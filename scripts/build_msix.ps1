# ==============================================================================
# Deltempo MSIX Packager
# ------------------------------------------------------------------------------
# Stages the published single-file GUI binary together with the MSIX manifest and
# a generated tile/logo asset set, then packs (and optionally Authenticode-signs)
# an installable .msix.
#
#   .\scripts\build_msix.ps1 -PayloadDir .\publish
#   .\scripts\build_msix.ps1 -PayloadDir .\publish -Architecture arm64 `
#       -Publisher "CN=Contoso, O=Contoso, C=US" -SignCertPath .\cert.pfx -SignCertPassword pw
#
# Requires makeappx.exe (Windows SDK), which ships on the GitHub `windows-latest`
# runner where packaging runs. Use -StageOnly on machines without the SDK to stop
# after staging and manifest validation.
# ==============================================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path $_ -PathType Container })]
    [string]$PayloadDir,

    [string]$OutputDir,

    # Must equal the signing certificate subject exactly, or Windows refuses to install.
    [string]$Publisher = 'CN=Beso1227',

    [ValidateSet('x64', 'arm64')]
    [string]$Architecture = 'x64',

    # Defaults to AssemblyVersion in Directory.Build.props (4-part, as MSIX requires).
    [string]$Version,

    [string]$SignCertPath,
    [string]$SignCertPassword,

    # Stage and validate the payload without invoking makeappx (machines without the SDK).
    [switch]$StageOnly
)

$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$iconPath = Join-Path $projectRoot 'app.ico'
$manifestTemplate = Join-Path $projectRoot 'packaging\msix\AppxManifest.xml'

if (-not $OutputDir) { $OutputDir = Join-Path $projectRoot 'dist' }

foreach ($required in @($iconPath, $manifestTemplate)) {
    if (-not (Test-Path $required)) { throw "Required input not found: $required" }
}

# ---------------------------------------------------------------- Resolve version
if (-not $Version) {
    [xml]$props = Get-Content (Join-Path $projectRoot 'Directory.Build.props') -Raw
    $node = $props.SelectSingleNode('//AssemblyVersion')
    if (-not $node -or [string]::IsNullOrWhiteSpace($node.InnerText)) {
        throw 'AssemblyVersion is missing from Directory.Build.props.'
    }
    $Version = $node.InnerText.Trim()
}

$parts = @($Version -split '\.')
if ($parts.Count -gt 4) { $parts = $parts[0..3] }
while ($parts.Count -lt 4) { $parts += '0' }
$Version = $parts -join '.'

# --------------------------------------------------------------- Locate toolchain
function Find-WindowsSdkTool([string]$Name) {
    $roots = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'),
        (Join-Path $env:ProgramFiles 'Windows Kits\10\bin')
    ) | Where-Object { $_ -and (Test-Path $_) }

    foreach ($root in $roots) {
        $hit = Get-ChildItem $root -Recurse -Filter $Name -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending | Select-Object -First 1
        if ($hit) { return $hit.FullName }
    }

    $cmd = Get-Command $Name -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    return $null
}

$makeappx = Find-WindowsSdkTool 'makeappx.exe'
if (-not $makeappx -and -not $StageOnly) {
    throw "makeappx.exe not found (Windows SDK). Install the Windows SDK, or re-run with -StageOnly."
}

# ----------------------------------------------------------------------- Stage
$stageDir = Join-Path $OutputDir ("msix_stage_{0}" -f $Architecture)
if (Test-Path $stageDir) { Remove-Item $stageDir -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $stageDir 'Assets') -Force | Out-Null

$guiExe = Join-Path $PayloadDir 'Deltempo.exe'
if (-not (Test-Path $guiExe)) { throw "Deltempo.exe not found in payload directory: $PayloadDir" }
Copy-Item $guiExe (Join-Path $stageDir 'Deltempo.exe') -Force

# ------------------------------------------------- Generate tile/logo assets
# The repo ships only app.ico, so the MSIX-required PNG assets are rasterised from
# it at pack time instead of committing seven pre-scaled binaries.
Add-Type -AssemblyName System.Drawing

function New-MsixAsset {
    param(
        [System.Drawing.Icon]$Icon,
        [int]$Width,
        [int]$Height,
        [string]$Path
    )

    $bitmap = New-Object System.Drawing.Bitmap($Width, $Height)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

            # Icons are square; centre a fitted copy on the required (possibly wide) canvas.
            $side = [Math]::Min($Width, $Height)
            $x = [int](($Width - $side) / 2)
            $y = [int](($Height - $side) / 2)
            $graphics.DrawIcon($Icon, (New-Object System.Drawing.Rectangle($x, $y, $side, $side)))
        }
        finally {
            $graphics.Dispose()
        }

        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
    }
}

$assetDir = Join-Path $stageDir 'Assets'
$icon = New-Object System.Drawing.Icon($iconPath, 256, 256)
try {
    New-MsixAsset -Icon $icon -Width 50  -Height 50  -Path (Join-Path $assetDir 'StoreLogo.png')
    New-MsixAsset -Icon $icon -Width 44  -Height 44  -Path (Join-Path $assetDir 'Square44x44Logo.png')
    New-MsixAsset -Icon $icon -Width 71  -Height 71  -Path (Join-Path $assetDir 'SmallTile.png')
    New-MsixAsset -Icon $icon -Width 150 -Height 150 -Path (Join-Path $assetDir 'Square150x150Logo.png')
    New-MsixAsset -Icon $icon -Width 310 -Height 310 -Path (Join-Path $assetDir 'LargeTile.png')
    New-MsixAsset -Icon $icon -Width 310 -Height 150 -Path (Join-Path $assetDir 'Wide310x150Logo.png')
    New-MsixAsset -Icon $icon -Width 620 -Height 300 -Path (Join-Path $assetDir 'SplashScreen.png')
}
finally {
    $icon.Dispose()
}

# ------------------------------------------------------------- Patch manifest
# Edit the Identity element through the XML DOM so MinVersion / MaxVersionTested in
# Dependencies are never clobbered by a textual replace (a naive Version="..." regex
# matches inside MinVersion="...").
[xml]$manifest = Get-Content $manifestTemplate -Raw
$manifest.Package.Identity.Version = $Version
$manifest.Package.Identity.Publisher = $Publisher
$manifest.Package.Identity.ProcessorArchitecture = $Architecture
$manifestPath = Join-Path $stageDir 'AppxManifest.xml'
$manifest.Save($manifestPath)

Write-Host 'Staged MSIX payload:' -ForegroundColor Cyan
Write-Host "  $stageDir"
Write-Host "  Identity: Version=$Version | Publisher=$Publisher | Arch=$Architecture"

if ($StageOnly) {
    Write-Host 'STAGE ONLY: skipped makeappx pack (no Windows SDK on this machine).' -ForegroundColor Yellow
    return
}

# ----------------------------------------------------------------------- Pack
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$msixPath = Join-Path $OutputDir ("Deltempo-{0}-{1}.msix" -f $Version, $Architecture)
if (Test-Path $msixPath) { Remove-Item $msixPath -Force }

Write-Host ">>> Packing $msixPath" -ForegroundColor Cyan
& $makeappx pack /d $stageDir /p $msixPath /o
if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed with exit code $LASTEXITCODE." }

& $makeappx validate /p $msixPath
if ($LASTEXITCODE -ne 0) { throw "makeappx validate failed with exit code $LASTEXITCODE." }

# ----------------------------------------------------------------------- Sign
if ($SignCertPath) {
    if (-not (Test-Path $SignCertPath)) { throw "Signing certificate not found: $SignCertPath" }

    $signtool = Find-WindowsSdkTool 'signtool.exe'
    if (-not $signtool) { throw 'signtool.exe not found (Windows SDK); cannot sign the MSIX.' }

    $signArgs = @('sign', '/f', $SignCertPath, '/fd', 'SHA256',
                  '/tr', 'https://timestamp.digicert.com', '/td', 'SHA256')
    if ($SignCertPassword) { $signArgs += @('/p', $SignCertPassword) }
    $signArgs += $msixPath

    & $signtool @signArgs
    if ($LASTEXITCODE -ne 0) { throw "signtool sign failed for $msixPath." }

    & $signtool verify /pa $msixPath
    if ($LASTEXITCODE -ne 0) { throw "Signature verification failed for $msixPath." }

    Write-Host 'MSIX signed and verified.' -ForegroundColor Green
}
else {
    Write-Host 'MSIX left UNSIGNED (no -SignCertPath). Install will warn until signed.' -ForegroundColor Yellow
}

# -------------------------------------------------------------------- Cleanup
Remove-Item $stageDir -Recurse -Force -ErrorAction SilentlyContinue

$item = Get-Item $msixPath
Write-Host ("SUCCESS: {0} ({1:N2} MB)" -f $item.FullName, ($item.Length / 1MB)) -ForegroundColor Green


