# ==============================================================================
# Deltempo Cleaning-Engine Benchmark
# Runs the reproducible throughput benchmark against a synthetic sandbox.
# Usage:  pwsh -File scripts/benchmark.ps1
# ==============================================================================

$ErrorActionPreference = "Stop"
Set-Location "$PSScriptRoot\.."

Write-Host ">>> Building Release configuration..." -ForegroundColor Cyan
dotnet build tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release --nologo -v q

Write-Host ""
Write-Host ">>> Running cleaning-engine throughput benchmark..." -ForegroundColor Cyan
Write-Host "    (1,500 files x 8 KB across 50 subfolders, parallel deletion pool)" -ForegroundColor Gray
Write-Host ""

# xunit.v3 runs on Microsoft.Testing.Platform. On the .NET 10 SDK `dotnet test`
# cannot drive it ("Testing with VSTest target is no longer supported"), so the
# test executable is invoked directly, exactly as .github/workflows/ci.yml does.
$testExe = "tests\Deltempo.Tests\bin\Release\net10.0-windows\Deltempo.Tests.exe"
if (-not (Test-Path $testExe)) {
    Write-Error "Test executable not found at $testExe - build failed?"
    exit 1
}

& $testExe --filter "Category=Benchmark"
if ($LASTEXITCODE -ne 0) { Write-Error "Benchmark run failed with exit code $LASTEXITCODE"; exit $LASTEXITCODE }

Write-Host ""
Write-Host "TIP: Compare runs before/after engine changes with:" -ForegroundColor Yellow
Write-Host '    git stash && pwsh -File scripts/benchmark.ps1   # baseline' -ForegroundColor Gray
Write-Host '    git stash pop && pwsh -File scripts/benchmark.ps1  # candidate' -ForegroundColor Gray
