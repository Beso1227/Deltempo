@echo off
rem Runs the Deltempo test suite under dotnet-coverage collection (CI + local).
rem Benchmark tests (Category=Benchmark) are excluded: they are run explicitly
rem via scripts/benchmark.ps1. dotnet-coverage treats the first argument after
rem `collect` as the child command, so this script gives it one simple command.
dotnet test Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release --no-build --filter "Category!=Benchmark" --logger "trx;LogFileName=deltempo_tests.trx" %*