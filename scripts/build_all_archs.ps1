# ==============================================================================
# Deltempo multi-head / multi-architecture build
# ==============================================================================
# Publishes every shipping head for both Windows architectures into ONE
# organized tree, then writes provenance next to the binaries:
#
#   artifacts/
#     README.md                     generated: what this tree is, how to rebuild
#     manifest.json                 generated: version, commit, dirty flag, per-head facts
#     checksums.sha256              generated: SHA-256 of the four single-file binaries
#     wpf/x64/Deltempo.exe          self-contained single-file GUI (primary channel)
#     wpf/arm64/Deltempo-arm64.exe  same, native ARM64
#     cli/x64/deltempo_cli.exe      self-contained single-file CLI
#     cli/arm64/deltempo_cli-arm64.exe
#     winui/x64/                    self-contained WinUI 3 shell (folder publish)
#     winui/arm64/
#
# Why a separate script instead of build_release_exe.ps1: that one is the
# release-pipeline layout (publish*/dist*/repo root), and release.yml consumes
# those exact paths. This script is for a local, inspectable tree and never
# touches them.
#
# The Store .msix is deliberately NOT produced here: tools/build-store-package.ps1
# refuses a placeholder identity unless asked, and an artifact folder is exactly
# where a placeholder package could be mistaken for a submittable one. Build it
# explicitly once Partner Center has issued the real identity.
#
#   .\scripts\build_all_archs.ps1
#   .\scripts\build_all_archs.ps1 -SkipWinUI
#   .\scripts\build_all_archs.ps1 -OutputRoot D:\builds\deltempo
# ==============================================================================

[CmdletBinding()]
param(
    # Root of the organized tree. Defaults to <repo>\artifacts (gitignored).
    [string]$OutputRoot = "",

    [switch]$SkipWpf,
    [switch]$SkipCli,
    [switch]$SkipWinUI
)

# Deliberately NOT 'Stop': Windows PowerShell 5.1 promotes a native command's
# first stderr line to a terminating NativeCommandError, which aborts the build
# mid-publish. Every dotnet call below is checked through $LASTEXITCODE instead.
$ErrorActionPreference = 'Continue'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repoRoot

if (-not $OutputRoot) { $OutputRoot = Join-Path $repoRoot 'artifacts' }
$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

$version = (Get-Content (Join-Path $repoRoot 'Directory.Build.props') |
    Select-String '<Version>([^<]+)</Version>').Matches[0].Groups[1].Value
$commit = (& git rev-parse HEAD).Trim()
$dirty = [bool](& git status --porcelain)

$architectures = @(
    @{ Rid = 'win-x64';   Arch = 'x64'   }
    @{ Rid = 'win-arm64'; Arch = 'arm64' }
)

function Invoke-DotnetPublish {
    param(
        [string]$Project,
        [string]$Rid,
        [string]$OutDir,
        [string[]]$ExtraArgs = @()
    )

    if (Test-Path $OutDir) { Remove-Item -Recurse -Force $OutDir }
    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

    $publishArgs = @('publish', $Project, '-c', 'Release', '-r', $Rid, '--self-contained', 'true',
        "-p:SourceRevisionId=$commit") + $ExtraArgs + @('-o', $OutDir)

    Write-Host (">>> dotnet {0}" -f ($publishArgs -join ' ')) -ForegroundColor Cyan
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw ("dotnet publish failed for {0} ({1}), exit {2}." -f $Project, $Rid, $LASTEXITCODE)
    }
}

function Get-PeMachine {
    param([string]$Path)

    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $reader = New-Object System.IO.BinaryReader($stream)
        $stream.Position = 0x3C
        $peOffset = $reader.ReadInt32()
        $stream.Position = $peOffset + 4
        $machine = $reader.ReadUInt16()
    }
    finally {
        $stream.Dispose()
    }

    switch ($machine) {
        0x8664 { return 'x64' }
        0xAA64 { return 'arm64' }
        0x014C { return 'x86' }
        default { return ('0x{0:X4}' -f $machine) }
    }
}

function Assert-Architecture {
    param([string]$Path, [string]$Expected)

    $actual = Get-PeMachine -Path $Path
    if ($actual -ne $Expected) {
        throw ("{0} is a {1} binary, expected {2}." -f $Path, $actual, $Expected)
    }

    Write-Host ("    verified {0} ({1})" -f (Split-Path $Path -Leaf), $actual) -ForegroundColor DarkGray
}

# Nothing may hold an output binary open. Same guard build_release_exe.ps1 uses.
Get-Process 'deltempo_cli', 'Deltempo', 'Deltempo.WinUI' -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300

New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null

Write-Host (">>> Deltempo {0} @ {1}{2}" -f $version, $commit.Substring(0, 7), $(if ($dirty) { ' (dirty working tree)' } else { '' })) -ForegroundColor Green
Write-Host (">>> Output: {0}" -f $OutputRoot) -ForegroundColor Green

# ------------------------------------------------------------------------------
# 1. WPF head - the shipping default. Self-contained single file, one per arch.
# ------------------------------------------------------------------------------
$wpfPublishArgs = @(
    '-p:PublishSingleFile=true'
    '-p:IncludeNativeLibrariesForSelfExtract=true'
    '-p:EnableCompressionInSingleFile=true'
    '-p:PublishReadyToRun=true'
)

# The documented CLI recipe (§3.2) does not use ReadyToRun; keep the artefact the
# same shape the release pipeline produces.
$cliPublishArgs = @(
    '-p:PublishSingleFile=true'
    '-p:IncludeNativeLibrariesForSelfExtract=true'
    '-p:EnableCompressionInSingleFile=true'
)

if (-not $SkipWpf) {
    foreach ($arch in $architectures) {
        $out = Join-Path $OutputRoot ('wpf\{0}' -f $arch.Arch)
        Invoke-DotnetPublish -Project 'src/Deltempo.App.Wpf/WinTempCleaner.csproj' -Rid $arch.Rid -OutDir $out -ExtraArgs $wpfPublishArgs

        if ($arch.Arch -ne 'x64') {
            # Architecture-suffixed name so two Deltempo.exe files can never be
            # confused in a folder listing or an upload.
            Move-Item (Join-Path $out 'Deltempo.exe') (Join-Path $out ('Deltempo-{0}.exe' -f $arch.Arch)) -Force
        }

        Assert-Architecture -Path (Join-Path $out ('Deltempo{0}.exe' -f $(if ($arch.Arch -eq 'x64') { '' } else { '-' + $arch.Arch }))) -Expected $arch.Arch
    }
}

# ------------------------------------------------------------------------------
# 2. CLI head. Same treatment.
# ------------------------------------------------------------------------------
if (-not $SkipCli) {
    foreach ($arch in $architectures) {
        $out = Join-Path $OutputRoot ('cli\{0}' -f $arch.Arch)
        Invoke-DotnetPublish -Project 'src/Deltempo.Cli/Deltempo.Cli.csproj' -Rid $arch.Rid -OutDir $out -ExtraArgs $cliPublishArgs

        if ($arch.Arch -ne 'x64') {
            Move-Item (Join-Path $out 'deltempo_cli.exe') (Join-Path $out ('deltempo_cli-{0}.exe' -f $arch.Arch)) -Force
        }

        Assert-Architecture -Path (Join-Path $out ('deltempo_cli{0}.exe' -f $(if ($arch.Arch -eq 'x64') { '' } else { '-' + $arch.Arch }))) -Expected $arch.Arch
    }
}

# ------------------------------------------------------------------------------
# 3. WinUI 3 head. Folder publish: the Windows App SDK runtime is self-contained
#    and ~300 MB per architecture, so it stays a folder rather than a fat binary.
#    Portable flavor (app.manifest, requireAdministrator); the Store flavor is
#    produced by tools/build-store-package.ps1.
# ------------------------------------------------------------------------------
if (-not $SkipWinUI) {
    foreach ($arch in $architectures) {
        $out = Join-Path $OutputRoot ('winui\{0}' -f $arch.Arch)
        Invoke-DotnetPublish -Project 'src/Deltempo.App.WinUI/Deltempo.App.WinUI.csproj' -Rid $arch.Rid -OutDir $out -ExtraArgs @(('-p:Platform={0}' -f $arch.Arch))

        Assert-Architecture -Path (Join-Path $out 'Deltempo.WinUI.exe') -Expected $arch.Arch
    }
}

# ------------------------------------------------------------------------------
# 4. Provenance: checksums for the single-file binaries, manifest, README.
# ------------------------------------------------------------------------------
$singleFileArtifacts = @(
    @{ Head = 'wpf';   Arch = 'x64';   Path = 'wpf/x64/Deltempo.exe' }
    @{ Head = 'wpf';   Arch = 'arm64'; Path = 'wpf/arm64/Deltempo-arm64.exe' }
    @{ Head = 'cli';   Arch = 'x64';   Path = 'cli/x64/deltempo_cli.exe' }
    @{ Head = 'cli';   Arch = 'arm64'; Path = 'cli/arm64/deltempo_cli-arm64.exe' }
)

$lines = New-Object System.Collections.Generic.List[string]
$entries = New-Object System.Collections.Generic.List[object]

foreach ($item in $singleFileArtifacts) {
    $full = Join-Path $OutputRoot $item.Path
    if (-not (Test-Path $full)) { continue }

    $file = Get-Item $full
    $hash = (Get-FileHash $full -Algorithm SHA256).Hash.ToLowerInvariant()
    $lines.Add(('{0}  {1}' -f $hash, $item.Path))

    $entries.Add([ordered]@{
        head        = $item.Head
        arch        = $item.Arch
        path        = $item.Path
        bytes       = $file.Length
        sha256      = $hash
        peMachine   = (Get-PeMachine -Path $full)
    })
}

$winuiEntries = New-Object System.Collections.Generic.List[object]
foreach ($arch in $architectures) {
    $dir = Join-Path $OutputRoot ('winui\{0}' -f $arch.Arch)
    if (-not (Test-Path $dir)) { continue }

    $files = Get-ChildItem $dir -Recurse -File
    $winuiEntries.Add([ordered]@{
        head      = 'winui'
        arch      = $arch.Arch
        path      = ('winui/{0}' -f $arch.Arch)
        fileCount = $files.Count
        bytes     = ($files | Measure-Object Length -Sum).Sum
        exe       = ('winui/{0}/Deltempo.WinUI.exe' -f $arch.Arch)
        exeSha256 = (Get-FileHash (Join-Path $dir 'Deltempo.WinUI.exe') -Algorithm SHA256).Hash.ToLowerInvariant()
    })
}

[System.IO.File]::WriteAllText((Join-Path $OutputRoot 'checksums.sha256'), (($lines -join "`n") + "`n"), $utf8NoBom)

$manifest = [ordered]@{
    product         = 'Deltempo'
    version         = $version
    commit          = $commit
    workingTreeDirty = $dirty
    builtAtUtc      = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    dotnetSdk       = (& dotnet --version).Trim()
    heads           = @('wpf', 'cli', 'winui')
    singleFile      = $entries
    winui           = $winuiEntries
}

[System.IO.File]::WriteAllText(
    (Join-Path $OutputRoot 'manifest.json'),
    ($manifest | ConvertTo-Json -Depth 6),
    $utf8NoBom)

$readme = @"
# Deltempo build artifacts

Generated by ``scripts/build_all_archs.ps1`` - do not edit by hand.
This directory is gitignored; every file here is a build output.

    version : $version
    commit  : $commit$(if ($dirty) { ' (working tree had uncommitted changes)' } else { '' })
    built   : $($manifest.builtAtUtc)
    SDK     : $($manifest.dotnetSdk)

## Layout

    wpf/x64/Deltempo.exe               WPF GUI, self-contained single file (primary)
    wpf/arm64/Deltempo-arm64.exe       WPF GUI, native ARM64
    cli/x64/deltempo_cli.exe           CLI, self-contained single file
    cli/arm64/deltempo_cli-arm64.exe   CLI, native ARM64
    winui/x64/                         WinUI 3 shell, self-contained folder publish
    winui/arm64/                       WinUI 3 shell, native ARM64

``.pdb`` files next to the binaries match those exact builds.

``manifest.json`` carries per-artifact sizes, SHA-256 hashes and the verified PE
machine type. ``checksums.sha256`` covers the four single-file binaries and is
verifiable with ``Get-FileHash`` / ``sha256sum -c``.

## Not in this folder

- **Store .msix** - produced by ``tools/build-store-package.ps1`` once Partner
  Center has issued the real identity. A placeholder-identity package is
  intentionally never written here, because this folder is exactly where it
  could be mistaken for a submittable build.
- **MSIX for the WPF head** - ``scripts/build_msix.ps1``; the release pipeline
  owns that channel.

## Rebuild

    .\scripts\build_all_archs.ps1
    .\scripts\build_all_archs.ps1 -SkipWinUI      # faster: GUI + CLI only
"@

[System.IO.File]::WriteAllText((Join-Path $OutputRoot 'README.md'), $readme, $utf8NoBom)

Write-Host ""
Write-Host "SUCCESS: artifacts organized under $OutputRoot" -ForegroundColor Green
Get-ChildItem $OutputRoot -Directory | ForEach-Object {
    $files = Get-ChildItem $_.FullName -Recurse -File
    Write-Host ("  {0,-8} {1,5} files  {2,10:N1} MB" -f $_.Name, $files.Count, (($files | Measure-Object Length -Sum).Sum / 1MB))
}
