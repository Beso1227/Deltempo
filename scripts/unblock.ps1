# Deltempo Instant Unblock & SmartScreen Resolver
# Removes the Zone.Identifier 'Mark of the Web' tag from downloaded executables

$exePath = Join-Path $PSScriptRoot "..\Deltempo.exe"
if (-not (Test-Path $exePath)) {
    $exePath = Join-Path $PSScriptRoot "Deltempo.exe"
}

if (Test-Path $exePath) {
    Unblock-File -Path $exePath
    Write-Host "[OK] Deltempo.exe has been unblocked. Windows SmartScreen warning removed." -ForegroundColor Green
} else {
    Write-Host "[!] Deltempo.exe not found in directory." -ForegroundColor Yellow
}
