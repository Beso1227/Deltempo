## Summary

<!-- What does this PR change and why? Link related issues with "Fixes #123". -->

## Change Type

- [ ] Bug fix (non-breaking change that fixes an issue)
- [ ] New feature (non-breaking change that adds functionality)
- [ ] Refactor (no behavior change)
- [ ] Documentation only

## Safety Invariants Checklist

Deltempo deletes files with administrator privileges. PRs touching cleanup or update code MUST preserve:

- [ ] Two-phase model intact: `SCAN → PLAN → PROTECT → REVALIDATE → CLEAN`
- [ ] `PathSecurity` canonicalization and root containment enforced on every destructive path
- [ ] Reparse point / junction rejection not weakened
- [ ] Pre-deletion TOCTOU revalidation (`CleanupExecutor`) not bypassed
- [ ] `ProtectionPolicy` whitelist unchanged or extended conservatively ("when in doubt, KEEP")
- [ ] Update verification chain intact (HTTPS, host allowlist, SHA-256, manifest signature)
- [ ] Shell integration remains opt-in; no new implicit host mutations

## Verification

- [ ] `dotnet build` succeeds (GUI + CLI, Release)
- [ ] `dotnet build Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release` then `./Tests/Deltempo.Tests/bin/Release/net10.0-windows/Deltempo.Tests.exe --filter "Category!=Benchmark"` passes (`dotnet test` cannot drive xunit.v3 on the .NET 10 SDK — see docs/TESTING.md §3.1)
- [ ] New logic is covered by xUnit tests
- [ ] No new compiler or analyzer warnings introduced
