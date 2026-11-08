# Builds the Store-package (.msix) for the Deltempo WinUI flavor without
# Visual Studio: dotnet publish (Store flavor) -> staging -> makeappx pack.
#
# Why this exists (PUBLISHING_MICROSOFT_STORE.md section 3): the single-
# project MSIX SDK route needs msbuild's AppxPackage targets, which ship
# with Visual Studio - dotnet publish completed green while emitting no
# package, twice. But makeappx.exe and signtool.exe come in the
# Microsoft.Windows.SDK.BuildTools NuGet package the repo already depends
# on, so the package can be produced deterministically on this machine,
# on CI, and on any contributor's box without installing VS.
#
# Output: packaging/Store/out/<Identity>_<Version>_<arch>.msix (unsigned; the
# Store resigns on submission, matching /p:AppxPackageSigningEnabled=false in
# the documented Store workflow). Partner Center accepts the .msix directly.
#
# Identity and architecture are build inputs, not repo state: the committed
# Package.appxmanifest is a template with visible placeholders, and this script
# rewrites the STAGED copy. That way a real identity can never be mistaken for
# the current one, and the same script serves the CI dry run, a local test
# build, and the actual submission.
#
#   # CI / local dry run before the Partner Center account exists:
#   .\tools\build-store-package.ps1 -AllowPlaceholder
#
#   # Real submission build (values copied from Partner Center):
#   .\tools\build-store-package.ps1 -IdentityName 1234Publisher.Deltempo `
#       -Publisher "CN=1A2B3C4D-..." -PublisherDisplayName "Your Name"
#
#   # Local install test (see tools/test-store-package.ps1, which passes these):
#   .\tools\build-store-package.ps1 -IdentityName DeltempoStoreTest `
#       -Publisher "CN=Deltempo Store Test"

param(
    # Publish folder to package. Defaults to the Store-flavor publish output for
    # -Architecture. If it is empty and -SkipPublish is not set, the script
    # publishes first.
    [string]$PublishDir = "",

    # Reuse an existing publish folder as-is.
    [switch]$SkipPublish,

    # Partner Center identity: the Name shown on the app-identity page. Defaults
    # to the template placeholder, which requires -AllowPlaceholder.
    [string]$IdentityName = "DeltempoWinUIPreview",

    # Partner Center Publisher, exactly as shown (starts with "CN="). The
    # package Publisher and the reserved identity must match or the upload is
    # rejected - never guess this value.
    [string]$Publisher = "CN=Placeholder-Publisher",

    # Product name shown in the Store and in the package. Not the identity Name.
    [string]$DisplayName = "Deltempo",

    # Publisher display name (the seller name on the listing).
    [string]$PublisherDisplayName = "Deltempo",

    # Architecture of the payload. The package MUST declare one: an omitted
    # ProcessorArchitecture means "neutral", which would offer an x64-only
    # payload to ARM64 devices.
    [ValidateSet("x64", "arm64")]
    [string]$Architecture = "x64",

    # CI produces a placeholder-identity artifact before the Partner Center
    # account exists. It must be requested explicitly - a silently placeholder
    # package is exactly the green build that must never be uploaded.
    [switch]$AllowPlaceholder
)

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

# Windows PowerShell 5.1 does not load System.Xml.Linq by default, and the
# staged-manifest rewrite needs XDocument. No-op on PowerShell 7.
Add-Type -AssemblyName System.Xml.Linq

# --- 1. Ensure the Store-flavor publish payload exists.
if (-not $PublishDir) {
    $PublishDir = Join-Path $repoRoot `
        ("src\Deltempo.App.WinUI\bin\Release\net10.0-windows10.0.19041.0\win-{0}\publish" -f $Architecture)
}
if (-not (Test-Path (Join-Path $PublishDir "Deltempo.WinUI.exe"))) {
    if ($SkipPublish) {
        throw "No publish payload at $PublishDir; remove -SkipPublish to publish first."
    }
    dotnet publish (Join-Path $repoRoot "src\Deltempo.App.WinUI\Deltempo.App.WinUI.csproj") `
        -c Release -r ("win-{0}" -f $Architecture) --self-contained true -p:DeltempoMSIX=true
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)." }
}

# --- 2. Fail-closed tool resolution: makeappx from SDK BuildTools.
#
# Resolve the NuGet global-packages root by asking MSBuild for
# NuGetPackageFolders: it is the root the very SDK on this machine resolved for
# this project, so it tracks wherever the packages were actually placed -
# including the setup-dotnet cache redirection. CI ground truth, 2026-11-01:
# with `cache: true`, setup-dotnet restores the package tarball with
# `tar -P -C <workspace>`, and the graph lands outside every candidate the
# original heuristics could guess (NUGET_PACKAGES, `dotnet nuget locals`
# output, %USERPROFILE%\.nuget\packages, even the workspace .nuget mirror) -
# which is why every such guess missed while restore and publish succeeded.
# The heuristics stay as fallbacks (listed in the throw so a future failure
# names every path tried), but the property query comes first because it
# cannot disagree with the restore that already ran in this job.
$packageRoots = @()
$msbuildFolders = dotnet msbuild (Join-Path $repoRoot "src\Deltempo.App.WinUI\Deltempo.App.WinUI.csproj") `
    -getProperty:NuGetPackageFolders -nologo 2>$null
if ($LASTEXITCODE -eq 0 -and $msbuildFolders) {
    # Resolved roots, semicolon separated.
    $packageRoots += (($msbuildFolders -join "`n") -split ';' | Where-Object { $_ })
}
if ($env:NUGET_PACKAGES) { $packageRoots += $env:NUGET_PACKAGES }
$locals = dotnet nuget locals global-packages --list 2>$null
if ($LASTEXITCODE -eq 0 -and $locals) {
    # Output format: "global-packages: <path>" - strip the label.
    $packageRoots += (($locals | Select-Object -First 1) -replace '^global-packages:\s*', '')
}
$packageRoots += (Join-Path $env:USERPROFILE ".nuget\packages")
$packageRoots += (Join-Path $repoRoot ".nuget\packages")

$buildToolsRoot = $null
$usedRoot = $null
foreach ($root in ($packageRoots | Where-Object { $_ } | Select-Object -Unique)) {
    $found = Get-ChildItem `
        (Join-Path $root "microsoft.windows.sdk.buildtools") `
        -Directory -ErrorAction SilentlyContinue |
        Sort-Object Name | Select-Object -Last 1
    if ($null -ne $found) { $buildToolsRoot = $found; $usedRoot = $root; break }
}
if ($null -eq $buildToolsRoot) {
    $tried = ($packageRoots | Where-Object { $_ } | Select-Object -Unique) -join ', '
    throw "Microsoft.Windows.SDK.BuildTools was not found in any NuGet global-packages location ($tried); run dotnet restore first."
}
Write-Host "SDK BuildTools resolved from $usedRoot ($($buildToolsRoot.Name))."
$makeappx = Get-ChildItem (Join-Path $buildToolsRoot.FullName "bin") -Recurse -Filter "makeappx.exe" |
    Where-Object { $_.Directory.Name -eq "x64" } | Select-Object -First 1
if ($null -eq $makeappx) {
    throw "makeappx.exe not found under $($buildToolsRoot.FullName)."
}

# --- 3. Stage the package layout: AppxManifest.xml at the root (that is its
#        required name inside a package), logos, no PDBs (symbols are an
#        optional submission add-on, never runtime payload).
$outDir = Join-Path $repoRoot "packaging\Store\out"
$staging = Join-Path $env:TEMP "deltempo-winui-store-staging"
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Force -Path $outDir, $staging | Out-Null

Copy-Item (Join-Path $PublishDir "*") $staging -Recurse -Force
# Symbols are an optional submission add-on, never runtime payload. Logs are
# run artifacts too: shell-startup.log is written next to the exe, so a publish
# folder that has been launched once carries one - it must not ship.
Get-ChildItem $staging -Recurse -File -Include *.pdb, *.log |
    Remove-Item -Force
Copy-Item (Join-Path $repoRoot "src\Deltempo.App.WinUI\packaging\Store\Package.appxmanifest") `
    (Join-Path $staging "AppxManifest.xml")
New-Item -ItemType Directory -Force -Path (Join-Path $staging "Assets\Store") | Out-Null
Copy-Item (Join-Path $repoRoot "src\Deltempo.App.WinUI\Assets\Store\*.png") `
    (Join-Path $staging "Assets\Store") -Force

# Fail closed on the app PRI: a publish folder built by an older csproj
# (before StageAppPriIntoPublish) lacks Deltempo.WinUI.pri, and the packaged
# app would crash with XamlParseException on first launch - verified
# 2026-10-30. The publish-stage target adds it; abort if it is still absent.
if (-not (Test-Path (Join-Path $staging "Deltempo.WinUI.pri"))) {
    throw "Staging folder has no Deltempo.WinUI.pri; the package would crash on boot. Republish with the current csproj."
}

# --- 4. Identity of record: rewrite the STAGED manifest only.
#
# The committed template keeps its placeholders on purpose. Everything below
# touches the staging copy, so the repo can never carry a real identity that
# has drifted from Partner Center.
$stagedManifest = Join-Path $staging "AppxManifest.xml"

# The package version comes from the same AssemblyVersion the shell's footer
# shows, so the package and the UI cannot disagree.
$version = [regex]::Match(
    [IO.File]::ReadAllText((Join-Path $repoRoot "Directory.Build.props")),
    '<AssemblyVersion>([\d.]+)</AssemblyVersion>').Groups[1].Value
if ($version -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw "AssemblyVersion in Directory.Build.props is not a 4-part MSIX version (got '$version')."
}

if (-not $AllowPlaceholder) {
    if ($IdentityName -eq "DeltempoWinUIPreview" -or $Publisher -eq "CN=Placeholder-Publisher") {
        throw ("Refusing to pack a placeholder identity. Reserve the app name in Partner Center " +
            "(docs/PUBLISHING_MICROSOFT_STORE.md section 1), then pass -IdentityName and -Publisher " +
            "exactly as the app-identity page shows them. Use -AllowPlaceholder only for a CI dry run.")
    }
    if ($Publisher -notmatch '^CN=') {
        throw "Publisher must be the full Partner Center value, which starts with 'CN=' (got '$Publisher')."
    }
}

$doc = [System.Xml.Linq.XDocument]::Load(
    $stagedManifest, [System.Xml.Linq.LoadOptions]::PreserveWhitespace)
$fns = [System.Xml.Linq.XNamespace]::Get("http://schemas.microsoft.com/appx/manifest/foundation/windows10")
$uapns = [System.Xml.Linq.XNamespace]::Get("http://schemas.microsoft.com/appx/manifest/uap/windows10")

$identityNode = $doc.Root.Element($fns + "Identity")
if ($null -eq $identityNode) { throw "Staged AppxManifest.xml has no Package/Identity element." }
$identityNode.SetAttributeValue("Name", $IdentityName)
$identityNode.SetAttributeValue("Publisher", $Publisher)
$identityNode.SetAttributeValue("Version", $version)
$identityNode.SetAttributeValue("ProcessorArchitecture", $Architecture)

foreach ($node in $doc.Descendants($fns + "DisplayName")) { $node.Value = $DisplayName }
foreach ($node in $doc.Descendants($fns + "PublisherDisplayName")) { $node.Value = $PublisherDisplayName }
foreach ($node in $doc.Descendants($uapns + "VisualElements")) { $node.SetAttributeValue("DisplayName", $DisplayName) }

$doc.Save($stagedManifest)

Write-Host ("Identity: Name={0} Publisher={1} Version={2} Architecture={3}" -f `
    $IdentityName, $Publisher, $version, $Architecture)

# --- 5. The output name follows the identity and architecture, never a literal.
$output = Join-Path $outDir ("{0}_{1}_{2}.msix" -f $IdentityName, $version, $Architecture)

# One run produces one artifact. Leaving a previous run's package next to this
# one is how a placeholder-identity build ends up uploaded by mistake.
Get-ChildItem $outDir -Filter *.msix -File -ErrorAction SilentlyContinue |
    Remove-Item -Force

# --- 6. Pack. makeappx validates the manifest (schema, references, sizes).
& $makeappx.FullName pack /o /p $output /d $staging | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "makeappx pack failed ($LASTEXITCODE). Fix the manifest errors above; do not ship an .msix that only existed because validation was skipped."
}
Remove-Item $staging -Recurse -Force

Get-ChildItem $outDir | ForEach-Object { "{0}  {1} bytes" -f $_.Name, $_.Length }
