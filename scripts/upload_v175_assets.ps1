$token = "$env:GITHUB_TOKEN"
$repo = "Beso1227/Deltempo"
$tag = "v1.7.5"

$headers = @{
    "Authorization" = "token $token"
    "User-Agent" = "Deltempo-Release"
    "Accept" = "application/vnd.github.v3+json"
}

# 1. Get the release ID for tag v1.7.5
$release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/tags/$tag" -Headers $headers
Write-Host "Found Release: $($release.name) (ID: $($release.id))"

# 2. Upload asset helper
function Upload-Asset($filePath, $contentType) {
    $fileName = [System.IO.Path]::GetFileName($filePath)
    Write-Host "Uploading $fileName..."
    $fileBytes = [System.IO.File]::ReadAllBytes($filePath)
    $uploadUrl = "https://uploads.github.com/repos/$repo/releases/$($release.id)/assets?name=$fileName"
    
    $uploadHeaders = @{
        "Authorization" = "token $token"
        "User-Agent" = "Deltempo-Release"
        "Content-Type" = $contentType
    }
    
    $res = Invoke-RestMethod -Uri $uploadUrl -Method Post -Headers $uploadHeaders -Body $fileBytes
    Write-Host "Uploaded $fileName successfully (ID: $($res.id), Size: $($res.size))"
}

Upload-Asset "publish\Deltempo.exe" "application/vnd.microsoft.portable-executable"
Upload-Asset "publish\deltempo_cli.exe" "application/vnd.microsoft.portable-executable"
Upload-Asset "publish\checksums.sha256" "text/plain"

Write-Host "All assets uploaded to GitHub Release v1.7.5!" -ForegroundColor Green
