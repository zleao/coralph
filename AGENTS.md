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
Coralph uses Serilog for structured logging with JSON output.

### Log Files
- Location: `logs/coralph-{date}.log`
- Format: Compact JSON (one JSON object per line)
- Rotation: Daily, retaining 7 days

### Log Properties
Each log entry includes:
- `Application`: "Coralph"
- `Version`: Assembly version
- `Model`: Configured AI model
- `Iteration`: Current loop iteration (when in loop context)

### Logging Patterns
```csharp
// Informational events with structured properties
Log.Information("Starting iteration {Iteration} of {MaxIterations}", i, maxIterations);

// Errors with exception details
Log.Error(ex, "Iteration {Iteration} failed with error", i);

// Using LogContext for scoped properties
using (LogContext.PushProperty("Iteration", i))
{
    // All log entries in this scope include Iteration
}
```

## Git Hooks (Opt-in)
Enable local hooks:
```bash
git config core.hooksPath .githooks
```

The pre-commit hook runs:
- `dotnet format --verify-no-changes`
- a large-file size check for staged files
