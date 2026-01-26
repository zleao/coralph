# Coralph

A first cut of a “Ralph loop” runner implemented in C#/.NET 10 using the GitHub Copilot SDK.

## Prerequisites

- .NET SDK 10 preview
- GitHub CLI (`gh`) authenticated if you use `--refresh-issues`

## Run

```bash
# optional: refresh issues.json using gh
(dotnet run --project src/Coralph -- --refresh-issues --repo owner/name) || true

# run loop (default reads ./issues.json and uses ./coralph.config.json if present)
dotnet run --project src/Coralph -- --max-iterations 10

# create a config file with defaults (safe: refuses to overwrite existing file)
dotnet run --project src/Coralph -- --initial-config

# run loop using a config file (CLI flags override config values)
dotnet run --project src/Coralph -- --config coralph.config.json --max-iterations 5

# run loop using the bundled sample harness (no GitHub access needed)
dotnet run --project src/Coralph -- --issues-file issues.sample.json --max-iterations 10

# customize streaming output
dotnet run --project src/Coralph -- --max-iterations 5 --show-reasoning false
```

## Features

### Streaming Output Improvements
- **Visual styling**: Color-coded output for reasoning (cyan), assistant text (green), and tool execution (yellow)
- **Configuration**: Control display with `--show-reasoning` and `--colorized-output` flags
- **Mode tracking**: Automatic separation of reasoning vs. assistant vs. tool output

### Custom Tools
Built-in domain-specific tools available to the assistant:
- `list_open_issues`: Query issues from issues.json
- `get_progress_summary`: Retrieve recent progress entries
- `search_progress`: Search progress.txt for specific terms
```

## Build a distributable binary

```bash
# self-contained, single-file publish (adjust RID as needed: osx-arm64, osx-x64, linux-x64, win-x64)
dotnet publish src/Coralph -c Release -r osx-arm64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true

# run the published binary
./src/Coralph/bin/Release/net10.0/osx-arm64/publish/Coralph --max-iterations 5
```

Files used:
- `prompt.md` (instructions)
- `issues.json` (input; optional refresh via `gh`)
- `progress.txt` (append-only log)
- `coralph.config.json` (optional configuration overrides)

The loop stops early when the assistant outputs a line containing `COMPLETE`, or when issues.json has no open issues (prints `NO_OPEN_ISSUES`).
