# AGENTS

## Overview
Coralph is a C#/.NET 10 CLI that runs a "Ralph loop" using the GitHub Copilot SDK.

## Core Commands
```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet format --verify-no-changes

# End-to-end
just ci
```

## Project Layout
- `src/Coralph` — main CLI application
- `src/Coralph.Tests` — xUnit tests
- `.githooks/` — local Git hooks (opt-in)
- `.github/workflows` — CI and release pipelines
- `logs/` — structured JSON log files (daily rotation)

## Development Notes
- Tests live in `src/Coralph.Tests` and follow the `*Tests.cs` naming convention.
- No environment variables are required for local development.

## Logging
Coralph uses Serilog for structured JSON logging.

- **Location**: `logs/coralph-{date}.log` (daily rotation, 7 days retention)
- **Format**: Compact JSON (one object per line)
- **Properties**: Application, Version, Model, Iteration (when applicable)

**Usage**:
```csharp
Log.Information("Starting iteration {Iteration}", i);
Log.Error(ex, "Iteration {Iteration} failed", i);
using (LogContext.PushProperty("Iteration", i)) { /* scoped property */ }
```

## Git Hooks (Opt-in)
Enable local hooks:
```bash
git config core.hooksPath .githooks
```

The pre-commit hook runs:
- `dotnet format --verify-no-changes`
- a large-file size check for staged files
