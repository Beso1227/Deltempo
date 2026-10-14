# Contributing to Deltempo

Thank you for your interest in contributing to **Deltempo**! We welcome community contributions, bug fixes, and feature enhancements.

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
   #    project. The root WinTempCleaner.csproj excludes Core\, Tests\, Cli\ and
   #    Services\, so a per-project check silently covers only part of the solution
   #    and will pass while CI fails.
   dotnet format deltempo.sln --verify-no-changes --no-restore

   # 3. Build and run the automated test suite
   dotnet build Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release
   ./Tests/Deltempo.Tests/bin/Release/net10.0-windows/Deltempo.Tests.exe --filter "Category!=Benchmark"
   ```

   > **Why not `dotnet test`?** The suite uses xunit.v3, which runs on Microsoft.Testing.Platform. The .NET 10 SDK's `dotnet test` cannot drive it ("Testing with VSTest target is no longer supported"). Build once, then invoke the test executable directly. See [`docs/TESTING.md`](docs/TESTING.md).

4. **Submit a Pull Request**: Provide a clear explanation of your changes and motivation.

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
