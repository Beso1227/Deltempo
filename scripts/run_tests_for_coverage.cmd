@echo off
rem Runs the Deltempo test suite under dotnet-coverage collection (CI + local).
rem Benchmark tests (Category=Benchmark) are excluded: they are run explicitly
rem via scripts/benchmark.ps1.
rem
rem xunit.v3 runs on Microsoft.Testing.Platform (MTP). On the .NET 10 SDK
rem `dotnet test` cannot drive it - the VSTest bridge is rejected outright with
rem "Testing with VSTest target is no longer supported" - so the test
rem application is built once and invoked directly, matching
rem .github/workflows/ci.yml. dotnet-coverage treats the first argument after
rem `collect` as the child command, so it is given one simple command.
setlocal
set "TEST_EXE=Tests\Deltempo.Tests\bin\Release\net10.0-windows\Deltempo.Tests.exe"

dotnet build Tests\Deltempo.Tests\Deltempo.Tests.csproj -c Release --nologo -v q || exit /b 1
if not exist "%TEST_EXE%" (
    echo Test executable not found at %TEST_EXE% - build failed?
    exit /b 1
)

dotnet-coverage collect "%TEST_EXE% --filter Category!=Benchmark" -f cobertura -o Tests\Deltempo.Tests\TestResults\coverage.cobertura.xml %*
endlocal
