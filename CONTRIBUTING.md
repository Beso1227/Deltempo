# Contributing to Deltempo

Thank you for your interest in contributing to **Deltempo**! We welcome community contributions, bug fixes, and feature enhancements.

Everyone participating is expected to follow the [Code of Conduct](CODE_OF_CONDUCT.md).
If you need help using the project rather than changing it, see [SUPPORT.md](SUPPORT.md).
Security vulnerabilities should never be opened as a public issue — see [SECURITY.md](SECURITY.md).

## How to Contribute

1. **Fork & Clone the Repository**: Click "Fork" on GitHub, then clone your copy and create a branch:

   ```bash
   git clone https://github.com/<your-username>/Deltempo.git
   cd Deltempo
   git checkout -b feature/your-feature-name
   ```

2. **Make Your Changes**: Keep changes clean, modular, and adhering to modern C# / WPF practices.

3. **Run the full verification gate** (this is exactly what CI runs, in the same order):

   ```powershell
   # 1. Build the whole solution in Release
   dotnet build deltempo.sln -c Release

   # 2. Verify code style. Run this against deltempo.sln - never against a single
   #    project. The solution holds the WPF app, Core, the CLI, the WinUI shell and the
   #    test project, so a per-project check silently covers only part of the solution
   #    and will pass while CI fails.
   dotnet format deltempo.sln --verify-no-changes --no-restore

   # 3. Build and run the automated test suite
   dotnet build Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release
   ./Tests/Deltempo.Tests/bin/Release/net10.0-windows/Deltempo.Tests.exe --filter "Category!=Benchmark"
   ```

   > **Why not `dotnet test`?** The suite uses xunit.v3, which runs on Microsoft.Testing.Platform. The .NET 10 SDK's `dotnet test` cannot drive it ("Testing with VSTest target is no longer supported"). Build once, then invoke the test executable directly. See [`docs/TESTING.md`](docs/TESTING.md).

4. **Submit a Pull Request**: Provide a clear explanation of your changes and motivation.

## Review Process — Please Read

**`main` currently has no required pull-request review, and CODEOWNERS is not enforced.**
GitHub's CODEOWNERS file has no effect unless branch protection requires a code-owner
review, and that requirement is not switched on. In practice this means:

- Your PR may be merged without anyone reviewing it.
- The ownership annotations in `.github/CODEOWNERS` are advisory, not a gate.
- The one status check that *is* required is **Build, Test & Quality Gate**.

This is a known gap, not an oversight. It is deliberately left unapplied because this
project has exactly one maintainer and GitHub does not allow you to approve your own
PR — enabling the requirement would block the maintainer from shipping. The
alternative, requiring the **CodeQL** check as well, has no such downside. The full
reasoning is recorded in `.github/settings.yml` under the branch protection block.

If you are reviewing someone else's PR here, **your review is the only gate this
project has.** That makes careful review unusually valuable here, particularly for
anything touching `Core/Safety/`, `Core/Cleaning/`, the cache resolvers, or the
release workflow.

## Coding Guidelines

- Follow standard C# naming conventions and XML doc comments.
- Keep the UI responsive by using `async/await` for all file I/O.
- Always preserve parent root folders during deletion operations.
- Ensure error handling gracefully catches locked file exceptions (`IOException`, `UnauthorizedAccessException`).

## Things That Will Fail Your PR

These are enforced by CI, and each has caught a real defect on this repository:

- **`dotnet format` against a single project.** The root project excludes the nested `Core`, `Services`, `Cli` and `Tests` folders, so this is strictly weaker than the solution-level gate. Seven files once shipped with a trailing-newline failure that a per-project check reported as clean.
- **Bumping the version anywhere but `Directory.Build.props`.** Version, `FileVersion` and `AssemblyVersion` are centralized there; individual `.csproj` files must not override them.
- **Changing the published test count without changing the suite.** `DocumentationConsistencyTests` derives the real test count by reflection and fails if the README badge disagrees — and it checks the badge, the prose, and all eight translations.
- **Adding a new cleanup scope without asking whether it is disposable.** See the [refusal table in the README](README.md#21-what-deltempo-refuses-to-delete). Several paths that look exactly like scratch (`%ProgramData%\Package Cache`, `%LOCALAPPDATA%\Microsoft\dotnet`, bare `C:\NVIDIA` roots) are load-bearing, and adding them as targets would delete things users cannot recover. New exclusions are denied twice: removed from the resolver *and* refused by `ProtectionPolicy`.
- **Editing a translation independently.** `README.*.md` must stay structurally in sync with `README.md` — same headings, same table rows, same code blocks.
