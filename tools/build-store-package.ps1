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
# Output: packaging/Store/out/<arch>/*.msix (unsigned; the Store resigns
# on submission, matching /p:AppxPackageSigningEnabled=false in the
# documented Store workflow). Partner Center accepts the .msix directly.

param(
    # Publish folder to package. Defaults to the Store-flavor publish output.
    # If it is empty and -SkipPublish is not set, the script publishes first.
    [string]$PublishDir = "",

    # Reuse an existing publish folder as-is.
    [switch]$SkipPublish
)

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

# --- 1. Ensure the Store-flavor publish payload exists.
if (-not $PublishDir) {
    $PublishDir = Join-Path $repoRoot `
        "src\Deltempo.App.WinUI\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish"
}
if (-not (Test-Path (Join-Path $PublishDir "Deltempo.WinUI.exe"))) {
    if ($SkipPublish) {
        throw "No publish payload at $PublishDir; remove -SkipPublish to publish first."
    }
    dotnet publish (Join-Path $repoRoot "src\Deltempo.App.WinUI\Deltempo.App.WinUI.csproj") `
        -c Release -r win-x64 --self-contained true -p:DeltempoMSIX=true
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)." }
}

# --- 2. Fail-closed tool resolution: makeappx from SDK BuildTools.
$buildToolsRoot = Get-ChildItem `
    (Join-Path $env:USERPROFILE ".nuget\packages\microsoft.windows.sdk.buildtools") `
    -Directory -ErrorAction SilentlyContinue |
    Sort-Object Name | Select-Object -Last 1
if ($null -eq $buildToolsRoot) {
    throw "Microsoft.Windows.SDK.BuildTools is not in the NuGet cache; run dotnet restore first."
}
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
Get-ChildItem $staging -Recurse -Include *.pdb -File |
    Remove-Item -Force
Copy-Item (Join-Path $repoRoot "src\Deltempo.App.WinUI\packaging\Store\Package.appxmanifest") `
    (Join-Path $staging "AppxManifest.xml")
New-Item -ItemType Directory -Force -Path (Join-Path $staging "Assets\Store") | Out-Null
Copy-Item (Join-Path $repoRoot "src\Deltempo.App.WinUI\Assets\Store\*.png") `
    (Join-Path $staging "Assets\Store") -Force

# --- 4. Read the identity version for the output file name.
$manifestText = [IO.File]::ReadAllText((Join-Path $repoRoot "src\Deltempo.App.WinUI\packaging\Store\Package.appxmanifest"))
$version = [regex]::Match($manifestText, 'Version\s*=\s*"([^"]+)"').Groups[1].Value
$identity = [regex]::Match($manifestText, 'Name\s*=\s*"([^"]+)"').Groups[1].Value
$output = Join-Path $outDir ("{0}_{1}_x64.msix" -f $identity, $version)

# --- 5. Pack. makeappx validates the manifest (schema, references, sizes).
& $makeappx.FullName pack /o /p $output /d $staging | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "makeappx pack failed ($LASTEXITCODE). Fix the manifest errors above; do not ship an .msix that only existed because validation was skipped."
}
Remove-Item $staging -Recurse -Force

Get-ChildItem $outDir | ForEach-Object { "{0}  {1} bytes" -f $_.Name, $_.Length }
