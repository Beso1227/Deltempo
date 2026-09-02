# Contributing to Deltempo

Thank you for your interest in contributing to **Deltempo**! We welcome community contributions, bug fixes, and feature enhancements.

## How to Contribute

1. **Fork the Repository**: Click "Fork" on GitHub to create your copy.
2. **Clone & Create a Branch**:
   ```bash
   git checkout -b feature/your-feature-name
   ```
3. **Make Your Changes**: Keep changes clean, modular, and adhering to modern C# / WPF practices.
4. **Run Verification Tests**:
   ```bash
   dotnet build
   .\bin\Debug\net10.0-windows\WinTempCleaner.exe --test
   ```
5. **Submit a Pull Request**: Provide a clear explanation of your changes and motivation.

## Coding Guidelines
- Follow standard C# naming conventions and XML doc comments.
- Keep the UI responsive by using `async/await` for all file I/O.
- Always preserve parent root folders during deletion operations.
- Ensure error handling gracefully catches locked file exceptions (`IOException`, `UnauthorizedAccessException`).
