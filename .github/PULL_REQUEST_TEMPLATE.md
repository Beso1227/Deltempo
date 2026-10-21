## Summary

<!-- What does this PR change and why? Link related issues with "Fixes #123". -->

## Change Type

- [ ] Bug fix (non-breaking change that fixes an issue)
- [ ] New feature (non-breaking change that adds functionality)
- [ ] Refactor (no behavior change)
- [ ] Documentation only

## Safety Invariants Checklist

Deltempo deletes files with administrator privileges. PRs touching cleanup, safety, or update code MUST preserve:

- [ ] Two-phase model intact: `SCAN → PLAN → PROTECT → REVALIDATE → CLEAN`
- [ ] `PathSecurity` canonicalization and root containment enforced on every destructive path
- [ ] Reparse point / junction rejection not weakened
- [ ] Pre-deletion TOCTOU revalidation (`CleanupExecutor`) not bypassed
- [ ] `ProtectionPolicy` **denylist** not weakened — `IsProtected` is a denylist, not a whitelist, and every addition is a refusal, never an exception. When in doubt, refuse
- [ ] A new cleanup scope is genuinely disposable — see the [refusal table](../README.md#21-what-deltempo-refuses-to-delete) before adding anything that looks like scratch. Most "cache" paths are load-bearing (MSI source caches, per-user .NET SDK, bare driver vendor roots, `cargo registry\src`, Audacity recovery)
- [ ] Anything refused is refused **twice**: removed from the scope resolver *and* denied by `ProtectionPolicy`
- [ ] Recursive deletes still consult `ProtectionPolicy.TryGetProtectedDescendant` — a per-file guard does not protect a subtree removed by a single `Directory.Delete`
- [ ] Update verification chain intact (HTTPS, host allowlist, SHA-256 against the release asset)
- [ ] Shell integration remains opt-in; no new implicit host mutations

## Verification

Run these exactly as CI does, in this order. The format gate is the one most often
skipped, and skipping it locally is how seven trailing-newline failures once shipped.

- [ ] `dotnet build deltempo.sln -c Release` succeeds (GUI + CLI)
- [ ] `dotnet format deltempo.sln --verify-no-changes --no-restore` is clean.
      **Run it against the solution, never against a single project** — the root
      The solution holds five projects (the WPF app, Core, the CLI, the WinUI shell and the
      test project), so a per-project run silently covers only part of the solution and reports clean
      while CI fails.
- [ ] `dotnet build tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release` then `./Tests/Deltempo.Tests/bin/Release/net10.0-windows/Deltempo.Tests.exe --filter "Category!=Benchmark"` passes
      (`dotnet test` cannot drive xunit.v3 on the .NET 10 SDK — see docs/TESTING.md §3.1)
- [ ] New logic is covered by xUnit tests
- [ ] No new compiler or analyzer warnings introduced
- [ ] Documentation updated if this changes a command, a safety claim, a scope, or the published test count — `DocumentationConsistencyTests` will fail the build otherwise

## Known gap (not a blocker)

`main` currently has **no required pull-request review**, so CODEOWNERS is not
enforced. If you are reviewing someone else's PR, your review is the only gate
this project has.
