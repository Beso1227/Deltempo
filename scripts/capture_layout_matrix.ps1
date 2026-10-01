<#
.SYNOPSIS
    Captures Deltempo workspaces across a range of window sizes.
.DESCRIPTION
    Drives the app built-in offscreen renderer:
        Deltempo.exe --render-screens <outDir> <width> <height>
    which renders through RenderTargetBitmap. A live screen-grab harness cannot work here:
      * AllowsTransparency windows composite via DirectComposition, so PrintWindow
        returns an all-black frame.
      * The app is requireAdministrator, so UIPI blocks a non-elevated harness from
        MoveWindow / SetForegroundWindow on its window.
    The renderer needs no visible desktop, so this also runs over RDP or headless, and it
    clears the XAML minimums itself - so a single build covers every size.
#>
[CmdletBinding()]
param(
    [string]$Exe  = (Join-Path $PSScriptRoot "..\bin\Release\net10.0-windows\Deltempo.exe"),
    [string]$Out  = (Join-Path $PSScriptRoot "..\artifacts\ui"),
    [object[]]$Sizes = @()
)

$ErrorActionPreference = "Stop"

if ($Sizes.Count -eq 0) {
    $Sizes = @(
        @{ W = 1360; H = 840; Tag = "01-1360x840" },
        @{ W = 1100; H = 720; Tag = "02-1100x720" },
        @{ W = 960;  H = 640; Tag = "03-960x640" },
        @{ W = 880;  H = 620; Tag = "04-880x620" },
        @{ W = 800;  H = 560; Tag = "05-800x560-min" },
        @{ W = 720;  H = 520; Tag = "06-720x520-floor" }
    )
}

New-Item -ItemType Directory -Force -Path $Out | Out-Null
if (-not (Test-Path $Exe)) { throw "Executable not found: $Exe" }

Write-Host "Building once for all sizes ..."
& dotnet build (Join-Path $PSScriptRoot "..\deltempo.sln") -c Release --nologo -v quiet | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

foreach ($s in $Sizes) {
    $dir = Join-Path $Out $s.Tag
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }

    $proc = Start-Process -FilePath $Exe -ArgumentList @("--render-screens", $dir, "$($s.W)", "$($s.H)") -PassThru
    $proc.WaitForExit(120000) | Out-Null
    if (-not $proc.HasExited) { $proc | Stop-Process -Force; throw "Renderer timed out for $($s.Tag)" }
    if ($proc.ExitCode -ne 0) { throw "Renderer exit $($proc.ExitCode) for $($s.Tag)" }

    $frames = @(Get-ChildItem -Path $dir -Filter "*.png" -ErrorAction SilentlyContinue)
    if ($frames.Count -eq 0) { throw "No frames produced for $($s.Tag)" }
    Write-Host ("  {0,-16} {1} frame(s)" -f $s.Tag, $frames.Count)
}

Write-Host "Done. Frames in: $Out"