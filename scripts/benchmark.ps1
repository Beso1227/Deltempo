# ==============================================================================
# Deltempo Cleaning-Engine Benchmark
# Runs the reproducible throughput benchmark against a synthetic sandbox.
# Usage:  pwsh -File scripts/benchmark.ps1
# ==============================================================================

$ErrorActionPreference = "Stop"
Set-Location "$PSScriptRoot\.."

Write-Host ">>> Building Release configuration..." -ForegroundColor Cyan
dotnet build Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release --nologo -v q

Write-Host ""
Write-Host ">>> Running cleaning-engine throughput benchmark..." -ForegroundColor Cyan
Write-Host "    (1,500 files x 8 KB across 50 subfolders, parallel deletion pool)" -ForegroundColor Gray
Write-Host ""

dotnet test Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release --no-build `
    --filter "Category=Benchmark" `
    --logger "console;verbosity=detailed"

Write-Host ""
Write-Host "TIP: Compare runs before/after engine changes with:" -ForegroundColor Yellow
Write-Host '    git stash && pwsh -File scripts/benchmark.ps1   # baseline' -ForegroundColor Gray
Write-Host '    git stash pop && pwsh -File scripts/benchmark.ps1  # candidate' -ForegroundColor Gray
