param (
    [string]$Tag = "v1.8.0",
    [string]$Repo = "Beso1227/Deltempo",
    [string]$Token = $env:GITHUB_TOKEN
)

if ([string]::IsNullOrWhiteSpace($Token)) {
    # Fallback to origin remote token if present
    $remoteUrl = git config --get remote.origin.url
    if ($remoteUrl -match "https://([^@]+)@github\.com") {
        $Token = $Matches[1]
    }
}

if ([string]::IsNullOrWhiteSpace($Token)) {
    Write-Error "GitHub token not found in env:GITHUB_TOKEN or git remote."
    exit 1
}

$headers = @{
    "Authorization" = "token $Token"
    "User-Agent" = "Deltempo-Release"
    "Accept" = "application/vnd.github.v3+json"
}

Write-Host "Fetching Release for tag $Tag..." -ForegroundColor Cyan
$release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/tags/$Tag" -Headers $headers
Write-Host "Found Release: $($release.name) (ID: $($release.id))" -ForegroundColor Green

function Upload-Asset($filePath, $contentType) {
    if (-not (Test-Path $filePath)) {
        Write-Warning "File not found: $filePath, skipping."
        return
    }
    $fileName = [System.IO.Path]::GetFileName($filePath)
    
    # Check if asset already exists and delete it before re-uploading
    $existing = $release.assets | Where-Object { $_.name -eq $fileName }
    if ($existing) {
        Write-Host "Asset $fileName already exists (ID: $($existing.id)), deleting old asset..." -ForegroundColor Yellow
        Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/assets/$($existing.id)" -Method Delete -Headers $headers
    }

    Write-Host "Uploading $fileName..."
    $fileBytes = [System.IO.File]::ReadAllBytes($filePath)
    $uploadUrl = "https://uploads.github.com/repos/$Repo/releases/$($release.id)/assets?name=$fileName"
    
    $uploadHeaders = @{
        "Authorization" = "token $Token"
        "User-Agent" = "Deltempo-Release"
        "Content-Type" = $contentType
    }
    
    $res = Invoke-RestMethod -Uri $uploadUrl -Method Post -Headers $uploadHeaders -Body $fileBytes
    Write-Host "Uploaded $fileName successfully (Size: $($res.size) bytes)" -ForegroundColor Green
}

Upload-Asset "publish\Deltempo.exe" "application/vnd.microsoft.portable-executable"
Upload-Asset "publish\deltempo_cli.exe" "application/vnd.microsoft.portable-executable"
Upload-Asset "publish\checksums.sha256" "text/plain"

Write-Host "All assets uploaded to GitHub Release $Tag!" -ForegroundColor Green
