param (
    [string]$Tag = "v1.8.0",
    [string]$ReleaseName = "1.8.0",
    [string]$Repo = "Beso1227/Deltempo"
)

$ErrorActionPreference = "Stop"

$token = $env:GITHUB_TOKEN
if ([string]::IsNullOrWhiteSpace($token)) {
    $remoteUrl = git config --get remote.origin.url
    if ($remoteUrl -match "https://([^@]+)@github\.com") {
        $token = $Matches[1]
    }
}

if ([string]::IsNullOrWhiteSpace($token)) {
    Write-Error "GitHub token not found. Please set `$env:GITHUB_TOKEN."
    exit 1
}

$headers = @{
    "Authorization" = "token $token"
    "User-Agent" = "Deltempo-Release"
    "Accept" = "application/vnd.github.v3+json"
}

$checksumContent = (Get-Content "dist\checksums.sha256" -Raw).Trim()

$bodyText = "## Deltempo v1.8.0 — UI/UX Overhaul, Deep Multi-Drive Root Leftovers & Windows RestartManager`n`n" +
"Deltempo v1.8.0 delivers an elevated visual overhaul, deep root residual detection across all fixed storage drives, intelligent orphan application cleanup, and native Windows RestartManager integration for zero-reboot uninstallation.`n`n" +
"---`n`n" +
"### Highlights in v1.8.0`n`n" +
"- 🎨 **Frosted Glass UI/UX Redesign:**`n" +
"  - Modernized title bar navigation island with unified pill-style segmented tool switchers.`n" +
"  - Elevated double-bezel cards with hairline top glass highlights and refined drop shadows.`n" +
"  - 34pt high-visibility tabular figures for Hero, Drive C:, and RAM telemetry.`n" +
"  - Tactile interactive filter chips and safety status badges.`n`n" +
"- 🛡️ **Multi-Drive Deep Root Residual Cleaner:**`n" +
"  - Expanded filesystem trace scanning across all mounted fixed drives (C:, D:, etc.) including Root, Program Files, ProgramData, AppData Local, AppData Roaming, and AppData LocalLow.`n" +
"  - Comprehensive registry remnant sweeps across 32-bit and 64-bit hive paths.`n`n" +
"- 🔍 **Orphaned & Broken Application Heuristics:**`n" +
"  - Automatic detection of abandoned program installations with missing registry keys, corrupted uninstall strings, or broken executable paths.`n" +
"  - Safe 1-click purge with quarantine vault backup protection.`n`n" +
"- ⚡ **Windows RestartManager Zero-Reboot Engine:**`n" +
"  - P/Invoke integration with native Win32 RestartManager (RmStartSession, RmRegisterResources, RmGetList, RmShutdown).`n" +
"  - Gracefully terminates or prompts locking processes before uninstallation or leftover cleanup to prevent forced system reboots.`n`n" +
"- 📦 **Quarantine Vault Architecture:**`n" +
"  - Transactional pre-purge ZIP backups saved to LocalAppData with instant 1-click restoration.`n`n" +
"- ✅ **Verification & Quality:**`n" +
"  - 640 automated tests passing with 100% success rate.`n" +
"  - Compiled under TreatWarningsAsErrors with zero compiler warnings.`n`n" +
"---`n`n" +
"### SHA-256 Checksums`n``````n$checksumContent`n``````n"

# Step 1: Create as draft
Write-Host "Creating draft release for $Tag..." -ForegroundColor Cyan
$postData = @{
    tag_name = $Tag
    target_commitish = "main"
    name = $ReleaseName
    body = $bodyText
    draft = $true
    prerelease = $false
} | ConvertTo-Json

$draftRelease = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases" -Method Post -Headers $headers -Body $postData -ContentType "application/json; charset=utf-8"
$releaseId = $draftRelease.id
Write-Host "Draft Release created: ID $releaseId" -ForegroundColor Green

# Step 2: Upload assets
function Upload-Asset($filePath, $contentType) {
    if (-not (Test-Path $filePath)) {
        Write-Error "File not found: $filePath"
        return
    }
    $fileName = [System.IO.Path]::GetFileName($filePath)
    Write-Host "Uploading $fileName ($contentType)..." -ForegroundColor Cyan
    $fileBytes = [System.IO.File]::ReadAllBytes($filePath)
    $uploadUrl = "https://uploads.github.com/repos/$Repo/releases/$releaseId/assets?name=$fileName"
    
    $uploadHeaders = @{
        "Authorization" = "token $token"
        "User-Agent" = "Deltempo-Release"
        "Content-Type" = $contentType
    }
    
    $res = Invoke-RestMethod -Uri $uploadUrl -Method Post -Headers $uploadHeaders -Body $fileBytes
    Write-Host "Uploaded $fileName successfully (Size: $($res.size) bytes)" -ForegroundColor Green
}

Upload-Asset "dist\Deltempo.exe" "application/vnd.microsoft.portable-executable"
Upload-Asset "dist\deltempo_cli.exe" "application/vnd.microsoft.portable-executable"
Upload-Asset "dist\checksums.sha256" "text/plain"

# Step 3: Publish release (draft = false)
Write-Host "Publishing release $Tag..." -ForegroundColor Cyan
$publishData = @{
    draft = $false
    make_latest = "true"
} | ConvertTo-Json

$publishedRelease = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/$releaseId" -Method Patch -Headers $headers -Body $publishData -ContentType "application/json; charset=utf-8"

Write-Host "Release $Tag successfully published!" -ForegroundColor Green
Write-Host "Release URL: $($publishedRelease.html_url)" -ForegroundColor Green
