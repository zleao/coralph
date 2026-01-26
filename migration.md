# Technical PRD: Coralph Migration to TypeScript/Bun

## 1. Objective

Re-implement the `Coralph` CLI application (a "Ralph loop" runner using GitHub
Copilot SDK) using TypeScript and the Bun runtime. The goal is to achieve 1:1
feature parity with the .NET version while leveraging the Bun ecosystem for
faster startup times, built-in tooling, and access to the wider
Node.js/TypeScript library ecosystem.

## 2. Technical Stack

- **Runtime:** [Bun](https://bun.sh/) (latest stable)

- **Language:** TypeScript 5.x

- **CLI Framework:** `commander` (for argument parsing and help generation)

- **Terminal UI:** `chalk` (colors), `ora` (spinners), `boxen` (banners) —
  replacing `Spectre.Console` / `Figgle`.

- **Validation:** `zod` (for strict parsing of `coralph.config.json` and CLI
  args).

- **Copilot Integration:** `@modelcontextprotocol/sdk` or the Node.js equivalent
  of the `GitHub.Copilot.SDK` (assuming access to the Copilot Agent Extensions
  preview for Node).

- **Process Management:** `Bun.spawn` (native, low-overhead process spawning).

## 3. Architecture & Component Mapping

The application will move from an Object-Oriented C# structure to a
functional/modular TypeScript structure.

| **.NET Component (src/Coralph/)** | **TypeScript Equivalent (src/)** | **Responsibility**                                                 |
| --------------------------------- | -------------------------------- | ------------------------------------------------------------------ |
| `Program.cs`                      | `index.ts`                       | Entry point, CLI orchestration, main loop logic.                   |
| `LoopOptions.cs`                  | `types.ts` + `config.ts`         | Zod schemas for configuration and types.                           |
| `ArgParser.cs`                    | `cli.ts`                         | Definition of `commander` program, flags, and help.                |
| `CopilotRunner.cs`                | `agent/runner.ts`                | Manages Copilot session, streaming response, handling tool events. |
| `GhIssues.cs`                     | `utils/gh.ts`                    | Wrapper for `gh` CLI execution (`Bun.spawn`).                      |
| `prompt.md`                       | `assets/prompt.md`               | Static system prompt (loaded via `Bun.file`).                      |

## 4. Functional Requirements & Implementation Details

### 4.1. Configuration & Argument Parsing

- **Library:** `commander` + `zod`

- **Behavior:**
  - Must accept all existing flags: `--max-iterations`, `--model`,
    `--prompt-file`, `--progress-file`, `--issues-file`, `--refresh-issues`,
    `--repo`, `--generate-issues`, `--prd-file`, `--config`, `--initial-config`.

  - **Priority:** CLI Flags > `coralph.config.json` > Defaults.

  - **Validation:** Use Zod to validate that `MaxIterations` is >= 1 and
    required file paths exist (where applicable).

### 4.2. Main Loop Logic ("The Ralph Loop")

- **Logic:**
  1. Read `prompt.md` template.

  2. Read `issues.json` (or empty array if missing).

  3. Read `progress.txt` (or empty string).

  4. Construct the "Combined Prompt" (Source of Truth + Issues + Progress +
     Instructions).

  5. **Iteration:**
     - Send prompt to Copilot Agent.

     - Stream output to console (handling ANSI colors).

     - Capture output string.

     - Append result to `progress.txt` with timestamp and model version.

     - **Stop Condition:** Check for `<promise>COMPLETE</promise>` string in the
       output.

### 4.3. Copilot Agent Runner

- **Migration:** Replace C# `CopilotClient` with the TS equivalent.

- **Stream Handling:**
  - The .NET `AssistantMessageDeltaEvent` equivalent must stream text directly
    to `process.stdout`.

  - **Tool Usage:**
    - Intercept tool execution events (`ToolExecutionStart`,
      `ToolExecutionComplete`).

    - **Styling:** Replace `Spectre.Console` "Orange background" headers with
      `chalk.bgKeyword('orange').black('[Tool: name]')`.

    - **Summarization:** Port the `SummarizeToolOutput` logic (truncate > 6
      lines or > 800 chars) to a utility function.

### 4.4. GitHub Issues Integration

- **Implementation:** Use `Bun.spawn` to call the `gh` CLI.

- **Commands:**
  - `gh issue list --state open --json ...`: Fetch issues.

  - `gh issue create`: Used in the Generator mode.

- **Error Handling:** Capture `stderr` from the spawned process and throw
  readable errors if `gh` is not authenticated or fails.

### 4.5. Generator Mode (`--generate-issues`)

- **Logic:**
  1. Read PRD markdown file.

  2. Send "Architect/PM" system prompt to Copilot.

  3. Parse response to extract `gh issue create` commands from markdown code
     blocks (bash).

  4. Execute extracted commands sequentially.

- **Parsing:** Port the `ExtractGhIssueCommands` regex/logic from C# to TS.

## 5. Project Structure

Plaintext

```
coralph-ts/
├── package.json        # Dependencies (bun, commander, zod, chalk, etc.)
├── tsconfig.json       # Strict mode enabled
├── coralph.config.json # Default config
├── src/
│   ├── index.ts        # Main entry point
│   ├── cli.ts          # Commander setup
│   ├── config.ts       # Config loading & merging logic
│   ├── types.ts        # Zod schemas & interfaces
│   ├── agent/
│   │   └── runner.ts   # Copilot Client & Event handling
│   └── utils/
│       ├── gh.ts       # GitHub CLI wrapper
│       ├── text.ts     # String manipulation (truncation, formatting)
│       └── files.ts    # File I/O wrappers
├── test/               # Bun native tests
│   └── cli.test.ts
└── README.md
```

## 6. Migration Steps

1. **Scaffold:** `bun init` and install dependencies (`commander`, `zod`,
   `chalk`, `ora`).

2. **Config & CLI:** Implement `src/cli.ts` to parse args and `src/config.ts` to
   load JSON. Ensure `--initial-config` works.

3. **GH Wrapper:** Implement `src/utils/gh.ts` using `Bun.spawn` and verify it
   can fetch `issues.json`.

4. **Agent Integration:** Implement `src/agent/runner.ts`. Connect to the
   Copilot LSP/Agent. _Note: This is the most complex step ensuring stream
   parity._

5. **Loop Implementation:** specific logic in `src/index.ts` to tie the prompt
   construction and the runner loop together.

6. **Port Tools:** Move the output styling and summarization logic.

7. **Generator Mode:** Implement the PRD-to-Issues parsing logic.

8. **Binary Build:** Configure `bun build --compile` to generate a single-file
   executable (equivalent to the .NET self-contained publish).

## 7. Verification & Testing

- **Unit Tests:** Use `bun test`.
  - Port `ToolOutputStylingTests.cs` -> `test/styling.test.ts`.

  - Test Argument parsing logic.

- **Integration Test:** Run the loop against the provided `issues.sample.json`
  (mocking the Copilot response if necessary, or running a live "dry run").

- **Performance:** Verify startup time is faster than the .NET 10 preview build.
