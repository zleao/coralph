# Coralph Holistic Review

## 1. .NET Code Review & C# 14 / .NET 10 Modernization

### 1.1 Project Overview

Coralph is a .NET 10 console application (CLI tool) that implements a Ralph loop runner using the GitHub Copilot SDK. The codebase is well-structured with clear separation of concerns across approximately 20 files in the main project and comprehensive unit tests.

**Key Components:**
- **Program.cs**: Main entry point with iteration loop orchestration
- **CopilotRunner.cs** & **CopilotSessionRunner.cs**: Copilot SDK integration for one-shot and multi-turn sessions
- **DockerSandbox.cs**: Docker container orchestration for isolated execution
- **TaskBacklog.cs**: PRD task generation from GitHub issues
- **CustomTools.cs**: Custom AI function tools for reading issues/progress
- **PermissionPolicy.cs**: Permission handling for tool execution
- **EventStreamWriter.cs**: JSON event streaming for observability
- **FileContentCache.cs**: File caching with invalidation support
- Various utility classes for CLI parsing, logging, console output, diagnostics

**Architecture Patterns:**
- Static utility classes for most functionality
- Factory pattern for tool creation
- Builder pattern for Docker command construction
- Event-driven architecture for Copilot session events
- Repository pattern (file-based) for issues and progress

### 1.2 C# 14 Feature Opportunities

C# 14 introduces extension members and the `field` keyword that can modernize this codebase. Here are specific recommendations:

#### Opportunity 1: Use `field` keyword in properties with custom logic

**Current code (ConsoleOutput.cs:11-12):**
```csharp
internal static IAnsiConsole Out => _outConsole ??= CreateConsole(Console.Out, Console.IsOutputRedirected);
internal static IAnsiConsole Error => _errorConsole ??= CreateConsole(Console.Error, Console.IsErrorRedirected);
```

**C# 14 approach:**
```csharp
internal static IAnsiConsole Out
{
    get => field ??= CreateConsole(Console.Out, Console.IsOutputRedirected);
    set => field = value;
}

internal static IAnsiConsole Error
{
    get => field ??= CreateConsole(Console.Error, Console.IsErrorRedirected);
    set => field = value;
}
```

**Benefits:** Direct access to compiler-generated backing fields without manual field declarations, cleaner syntax.

#### Opportunity 2: Extension properties for FileReadResult

**Current code (FileContentCache.cs:83-86):**
```csharp
internal readonly record struct FileReadResult(bool Exists, string Content)
{
    internal static FileReadResult Missing { get; } = new(false, string.Empty);
}
```

**C# 14 extension properties:**
```csharp
internal readonly record struct FileReadResult(bool Exists, string Content)
{
    internal static FileReadResult Missing { get; } = new(false, string.Empty);
}

internal static class FileReadResultExtensions
{
    public static bool IsMissing(this FileReadResult result) => !result.Exists;
    public static bool IsEmpty(this FileReadResult result) => result.Exists && string.IsNullOrWhiteSpace(result.Content);
    public static int ContentLength(this FileReadResult result) => result.Exists ? result.Content.Length : 0;
}
```

**Benefits:** More fluent API for working with file read results without cluttering the record definition.

#### Opportunity 3: Collection expressions (already using some, but can be expanded)

**Current code (LoopOptions.cs:27-28):**
```csharp
public string[] ToolAllow { get; set; } = [];
public string[] ToolDeny { get; set; } = [];
```

**Good usage!** Already using collection expressions. Expand to other areas:

**Current code (DockerSandbox.cs:318):**
```csharp
if (value.IndexOfAny([' ', '"']) >= 0)
```

**Already using inline collection expressions!** The codebase is already adopting C# 14 features where applicable.

#### Opportunity 4: Primary constructors for simple classes

**Current code (EventStreamWriter.cs:25-37):**
```csharp
internal EventStreamWriter(TextWriter writer, string sessionId, int version = SchemaVersion, bool leaveOpen = true, bool flushEachEvent = false)
{
    _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
    Version = version;
    LeaveOpen = leaveOpen;
    _flushEachEvent = flushEachEvent;

    _jsonOptions = new JsonSerializerOptions
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
```

**C# 14 primary constructor approach:**
```csharp
internal sealed class EventStreamWriter(
    TextWriter writer,
    string sessionId,
    int version = SchemaVersion,
    bool leaveOpen = true,
    bool flushEachEvent = false)
{
    private readonly TextWriter _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    private readonly object _lock = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private readonly bool _flushEachEvent = flushEachEvent;
    private long _sequence;
    private int _pendingWritesSinceFlush;

    internal string SessionId { get; } = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
    internal int Version { get; } = version;
    internal bool LeaveOpen { get; } = leaveOpen;

    // ... rest of class
}
```

**Benefits:** Reduces boilerplate, clearer parameter-to-field mapping.

#### Opportunity 5: Static extension methods for utilities

**Current code (PromptHelpers.cs:165-168):**
```csharp
private static string TrimMarkdownWrapper(string value)
{
    return value.Trim('`', '*', '_');
}
```

**C# 14 extension approach:**
```csharp
internal static class StringMarkdownExtensions
{
    public static string TrimMarkdownWrapper(this string value) => value.Trim('`', '*', '_');
    public static string NormalizeWhitespace(this string value) => WhitespaceRegex.Replace(value, " ").Trim();
}
```

**Usage:**
```csharp
var cleaned = line.TrimMarkdownWrapper();
```

**Benefits:** More discoverable API, better IDE IntelliSense, clearer intent.

### 1.3 .NET 10 Improvements

.NET 10 brings significant performance improvements and new APIs that can benefit this codebase.

#### Improvement 1: Leverage improved JSON serialization options

**Current code (EventStreamWriter.cs:33-36):**
```csharp
_jsonOptions = new JsonSerializerOptions
{
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};
```

**Add .NET 10 strict settings:**
```csharp
_jsonOptions = new JsonSerializerOptions
{
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    AllowTrailingCommas = false,
    ReadCommentHandling = JsonCommentHandling.Disallow,
    PropertyNameCaseInsensitive = false  // Be explicit for security
};
```

**Benefits:** Stricter JSON parsing prevents malformed input, improved security posture.

#### Improvement 2: Use PipeReader for streaming JSON (for large event streams)

**Current approach** writes to TextWriter directly. For large event streams, .NET 10's PipeReader support in System.Text.Json could improve performance:

```csharp
// Future enhancement for EventStreamWriter
internal async ValueTask EmitAsync(string type, IDictionary<string, object?>? fields = null, CancellationToken ct = default)
{
    var payload = BuildPayload(type, fields);
    await JsonSerializer.SerializeAsync(_pipe.Writer.AsStream(), payload, _jsonOptions, ct);
}
```

**Benefits:** Better memory efficiency for high-throughput event streams, reduced allocations.

#### Improvement 3: Hardware acceleration for regex-heavy operations

**Current code (TaskBacklog.cs:14-34):**
```csharp
private static readonly Regex ChecklistLineRegex = new(
    @"^\s*[-*+]\s*\[(?<done>[ xX])\]\s+(?<text>.+)$",
    RegexOptions.Compiled);
```

**.NET 10 with source generators:**
```csharp
[GeneratedRegex(@"^\s*[-*+]\s*\[(?<done>[ xX])\]\s+(?<text>.+)$", RegexOptions.None)]
private static partial Regex ChecklistLineRegex();

[GeneratedRegex(@"^\s{0,3}#{2,4}\s+(?<title>.+?)\s*$", RegexOptions.None)]
private static partial Regex HeadingLineRegex();
```

**Benefits:** Compile-time regex generation, better performance, AOT-friendly.

#### Improvement 4: Optimize async patterns with ConfigureAwait

**Current code (DockerSandbox.cs:208-232):**
```csharp
await process.WaitForExitAsync(ct);
await Task.WhenAll(stdoutTask, stderrTask);
```

**Add ConfigureAwait for library code:**
```csharp
await process.WaitForExitAsync(ct).ConfigureAwait(false);
await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
```

**Benefits:** Avoids unnecessary context switches, better performance in library scenarios.

#### Improvement 5: Use SearchValues<T> for char lookups (.NET 8+, optimized in .NET 10)

**Current code (DockerSandbox.cs:318):**
```csharp
if (value.IndexOfAny([' ', '"']) >= 0)
```

**Optimized with SearchValues:**
```csharp
private static readonly SearchValues<char> QuoteSearchChars = SearchValues.Create([' ', '"']);

// Usage:
if (value.AsSpan().IndexOfAny(QuoteSearchChars) >= 0)
```

**Benefits:** .NET 10 has hardware-accelerated SearchValues with AVX10.2/SVE support, significant perf gain.

### 1.4 Code Quality Assessment

#### Naming Conventions ✅ Excellent
- Classes use PascalCase
- Methods use PascalCase
- Private fields use _camelCase prefix
- Constants use PascalCase
- Consistent and descriptive naming throughout

#### SOLID Principles Assessment

**Single Responsibility Principle (SRP):** ✅ Good
- Most classes have a single, well-defined responsibility
- `Program.cs` orchestrates but doesn't implement business logic
- Utility classes (PromptHelpers, CustomTools) have focused concerns
- **Minor concern:** `CopilotSessionEventRouter` handles both event routing and state management (could be split)

**Open/Closed Principle (OCP):** ⚠️ Moderate
- `PermissionPolicy` uses strategy pattern with allow/deny rules (good)
- **Improvement opportunity:** Make tool discovery extensible (currently hardcoded in `CustomTools.GetDefaultTools`)
- **Improvement opportunity:** Event stream formatters could be pluggable

**Liskov Substitution Principle (LSP):** ✅ Good
- Minimal inheritance usage (mostly composition)
- Record structs and sealed classes prevent inappropriate subclassing

**Interface Segregation Principle (ISP):** ✅ Good
- Uses focused interfaces from dependencies (IAnsiConsole, TextWriter)
- Internal classes don't expose unnecessary members

**Dependency Inversion Principle (DIP):** ⚠️ Moderate
- Good: Uses `IAnsiConsole` abstraction
- Good: `TextWriter` abstraction for EventStreamWriter
- **Concern:** Direct file system access throughout (no abstraction for testability)
- **Concern:** Static utility classes make unit testing harder
- **Recommendation:** Introduce `IFileSystem` abstraction for better testability

#### Error Handling

**Good practices:**
- Comprehensive try-catch blocks in critical paths
- Proper exception logging with Serilog
- Diagnostic information on Copilot CLI failures
- Graceful degradation (e.g., Banner fallback to plain text)

**Improvement opportunities:**
```csharp
// Current (Program.cs:342-357):
static async Task CommitProgressIfNeededAsync(string progressFile, CancellationToken ct)
{
    if (!File.Exists(progressFile))
        return;
    // ... git operations without error handling
}

// Recommended:
static async Task CommitProgressIfNeededAsync(string progressFile, CancellationToken ct)
{
    if (!File.Exists(progressFile))
        return;

    try
    {
        // ... git operations
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Failed to auto-commit progress file {ProgressFile}", progressFile);
        // Don't fail the entire run if commit fails
    }
}
```

#### Nullability ✅ Excellent
- Nullable reference types enabled
- Proper null checks with `??` operators
- Good use of null-conditional operators (`?.`)
- Validation at boundaries (parameter null checks)

### 1.5 Architecture Review

#### Overall Structure: ✅ Well-Organized

**Layering:**
```
Program.cs (Orchestration)
    ├── CopilotRunner / CopilotSessionRunner (SDK Integration)
    ├── DockerSandbox (Isolation)
    ├── TaskBacklog (Domain Logic)
    ├── CustomTools (AI Functions)
    ├── PermissionPolicy (Security)
    ├── EventStreamWriter (Observability)
    └── Utilities (ArgParser, PromptHelpers, etc.)
```

**Strengths:**
- Clear separation between orchestration and implementation
- Domain logic (TaskBacklog) isolated from infrastructure
- Good use of static classes for stateless utilities
- Event-driven architecture for Copilot session handling

**Improvement Opportunities:**

1. **Introduce a Service Layer:**

```csharp
// Current: Direct instantiation in Program.cs
var customTools = CustomTools.GetDefaultTools(opt.IssuesFile, opt.ProgressFile, opt.GeneratedTasksFile);

// Recommended: Service abstraction
internal interface IToolService
{
    AIFunction[] GetTools();
}

internal sealed class CustomToolService(LoopOptions options) : IToolService
{
    public AIFunction[] GetTools() => CustomTools.GetDefaultTools(
        options.IssuesFile,
        options.ProgressFile,
        options.GeneratedTasksFile);
}
```

2. **Extract Configuration Management:**

```csharp
// Create a ConfigurationService
internal sealed class ConfigurationService
{
    public static LoopOptions LoadOptions(string? configFile, LoopOptionsOverrides overrides)
    {
        var path = ResolveConfigPath(configFile);
        var options = LoadFromFile(path);
        PromptHelpers.ApplyOverrides(options, overrides);
        return options;
    }

    private static LoopOptions LoadFromFile(string path) { /* ... */ }
    private static string ResolveConfigPath(string? configFile) { /* ... */ }
}
```

3. **Repository Pattern for File Access:**

```csharp
internal interface IIssueRepository
{
    Task<string> GetIssuesJsonAsync(CancellationToken ct);
    Task RefreshIssuesAsync(CancellationToken ct);
}

internal sealed class GitHubIssueRepository(LoopOptions options) : IIssueRepository
{
    public async Task<string> GetIssuesJsonAsync(CancellationToken ct)
    {
        var result = await FileContentCache.Shared.TryReadTextAsync(options.IssuesFile, ct);
        return result.Exists ? result.Content : "[]";
    }

    public async Task RefreshIssuesAsync(CancellationToken ct)
    {
        var json = await GhIssues.FetchOpenIssuesJsonAsync(options.Repo, ct);
        await File.WriteAllTextAsync(options.IssuesFile, json, ct);
        FileContentCache.Shared.Invalidate(options.IssuesFile);
    }
}
```

#### Component Boundaries

**Strong boundaries:**
- Docker sandbox completely isolated from host environment
- Event streaming decoupled from execution logic
- Copilot SDK wrapper abstracts session management

**Weak boundaries:**
- File system access scattered throughout
- Configuration loading mixed with business logic in Program.cs
- Banner display logic in Banner.cs but invoked from Program.cs

### 1.6 Performance Considerations

#### Async Patterns: ✅ Generally Good

**Good practices:**
- Proper async/await usage throughout
- CancellationToken support in async methods
- Avoids async void (except for event handlers conceptually)

**Optimizations:**

1. **Batch file operations:**

```csharp
// Current: Sequential reads
var progressRead = await fileCache.TryReadTextAsync(opt.ProgressFile, ct);
var issuesRead = await fileCache.TryReadTextAsync(opt.IssuesFile, ct);

// Optimized: Parallel reads
var (progressRead, issuesRead) = await (
    fileCache.TryReadTextAsync(opt.ProgressFile, ct),
    fileCache.TryReadTextAsync(opt.IssuesFile, ct)
);
```

2. **Use Span<T> for string manipulation:**

```csharp
// Current (TaskBacklog.cs:720-727):
private static string Truncate(string value, int maxLength)
{
    if (value.Length <= maxLength)
        return value;
    return value[..maxLength].TrimEnd();
}

// Optimized with Span:
private static string Truncate(ReadOnlySpan<char> value, int maxLength)
{
    if (value.Length <= maxLength)
        return value.ToString();
    return value[..maxLength].TrimEnd().ToString();
}
```

#### Memory Allocation

**Good practices:**
- String builders for concatenation (DockerSandbox, TaskBacklog)
- Object pooling via FileContentCache
- Streaming for large outputs (DockerSandbox.ReadProcessStreamAsync)

**Optimization opportunities:**

1. **Use ArrayPool for temporary buffers:**

```csharp
// Current (DockerSandbox.cs:236):
var chunk = new char[4096];

// Optimized:
var chunk = ArrayPool<char>.Shared.Rent(4096);
try
{
    // ... use chunk
}
finally
{
    ArrayPool<char>.Shared.Return(chunk);
}
```

2. **Consider System.IO.Pipelines for streaming:**

The DockerSandbox already streams output well, but could use Pipelines for even better performance:

```csharp
internal static async Task<string> RunIterationAsync(...)
{
    var pipe = new Pipe();
    var writeTask = WriteToDockerAsync(pipe.Writer, ...);
    var readTask = ReadFromPipeAsync(pipe.Reader, ...);
    await Task.WhenAll(writeTask, readTask);
}
```

#### Caching Strategy: ✅ Good

**FileContentCache implementation:**
- Cache invalidation on file changes
- Checks both timestamp and length
- Thread-safe with lock
- Singleton pattern for global cache

**Improvement:**
- Add cache size limits to prevent unbounded growth
- Consider LRU eviction policy
- Add telemetry for cache hit/miss rates

```csharp
internal sealed class FileContentCache
{
    private readonly Dictionary<string, CacheEntry> _entries = new();
    private readonly object _lock = new();
    private const int MaxCacheEntries = 100;  // Add limit

    internal async Task<FileReadResult> TryReadTextAsync(string path, CancellationToken ct = default)
    {
        // ... existing logic

        // Add LRU eviction
        lock (_lock)
        {
            if (_entries.Count >= MaxCacheEntries)
            {
                var oldest = _entries.OrderBy(e => e.Value.LastAccessUtc).First();
                _entries.Remove(oldest.Key);
            }

            _entries[fullPath] = updatedEntry with { LastAccessUtc = DateTime.UtcNow };
        }
    }
}
```

#### EventStreamWriter Batching: ✅ Excellent

The batching implementation (FlushBatchSize = 32) is well-designed:
- Reduces I/O overhead
- Immediate flush for critical events
- Configurable flush strategy

### 1.7 Test Coverage Analysis

#### Current Test Coverage

**Tested components:**
- ✅ ArgParser (comprehensive)
- ✅ PromptHelpers (comprehensive)
- ✅ CopilotRunner (basic)
- ✅ Logging (basic)
- ✅ EventStreamWriter (comprehensive)
- ✅ FileContentCache (comprehensive)
- ✅ CustomTools (comprehensive)
- ❌ TaskBacklog (missing - needs tests!)

**Untested components:**
- ❌ CopilotSessionRunner (no tests)
- ❌ CopilotSessionEventRouter (no tests)
- ❌ DockerSandbox (no tests)
- ❌ PermissionPolicy (no tests)
- ❌ Banner (no tests)
- ❌ ConsoleOutput (no tests)
- ❌ CopilotDiagnostics (no tests)
- ❌ CopilotModelDiscovery (no tests)
- ❌ GhIssues (no tests)
- ❌ AzBoards (no tests)

#### Critical Test Gaps

**Priority 1: TaskBacklog.cs**
This is complex domain logic with no tests!

```csharp
// Recommended tests:
public class TaskBacklogTests
{
    [Fact]
    public void BuildBacklogJson_WithChecklistIssue_ExtractsTasksFromChecklist()
    {
        var issuesJson = """
        [
            {
                "number": 1,
                "title": "Feature request",
                "body": "- [ ] Implement authentication\n- [ ] Add logging\n- [x] Setup project"
            }
        ]
        """;

        var backlog = TaskBacklog.BuildBacklogJson(issuesJson);
        var parsed = JsonSerializer.Deserialize<TaskBacklogResponse>(backlog);

        Assert.Equal(3, parsed.Tasks.Count);
        Assert.Equal("Implement authentication", parsed.Tasks[0].Title);
        Assert.Equal("open", parsed.Tasks[0].Status);
        Assert.Equal("done", parsed.Tasks[2].Status);
    }

    [Fact]
    public void EnsureBacklogAsync_WhenContentUnchanged_DoesNotRewrite()
    {
        // Test idempotency
    }

    [Fact]
    public void BuildBacklogJson_PreservesExistingTaskStatus()
    {
        // Test status preservation across regeneration
    }
}
```

**Priority 2: PermissionPolicy**

```csharp
public class PermissionPolicyTests
{
    [Fact]
    public async Task HandleAsync_WithAllowRule_ApprovesRequest()
    {
        var opt = new LoopOptions { ToolAllow = ["bash"] };
        var policy = new PermissionPolicy(opt, null);
        var request = new PermissionRequest { Kind = "bash" };

        var result = await policy.HandleAsync(request, new PermissionInvocation());

        Assert.Equal("approved", result.Kind);
    }

    [Fact]
    public async Task HandleAsync_WithDenyRule_DeniesRequest()
    {
        var opt = new LoopOptions { ToolDeny = ["bash*"] };
        var policy = new PermissionPolicy(opt, null);
        var request = new PermissionRequest { Kind = "bash_execute" };

        var result = await policy.HandleAsync(request, new PermissionInvocation());

        Assert.NotEqual("approved", result.Kind);
    }
}
```

**Priority 3: DockerSandbox**

```csharp
public class DockerSandboxTests
{
    [Fact]
    public async Task CheckDockerAsync_WhenDockerNotInstalled_ReturnsFailure()
    {
        // Mock Process.Start to return non-zero exit code
    }

    [Fact]
    public void MapPathToContainer_WithRepoPath_ReturnsRelativePath()
    {
        // Test path mapping logic
    }
}
```

#### Test Infrastructure Recommendations

1. **Add integration tests:**

```csharp
[Collection("Integration")]
public class CopilotIntegrationTests
{
    [Fact(Skip = "Requires Copilot CLI")]
    public async Task RunOnceAsync_WithValidPrompt_ReturnsOutput()
    {
        // Integration test with real Copilot CLI
    }
}
```

2. **Add test helpers:**

```csharp
internal static class TestHelpers
{
    public static LoopOptions CreateTestOptions() => new()
    {
        Model = "test-model",
        MaxIterations = 1,
        PromptFile = "test-prompt.md",
        ProgressFile = "test-progress.txt",
        IssuesFile = "test-issues.json"
    };

    public static string CreateTempFile(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        return path;
    }
}
```

3. **Add property-based tests for parsing:**

```csharp
public class TaskBacklogPropertyTests
{
    [Property]
    public void Slugify_ProducesValidSlug(NonEmptyString input)
    {
        var slug = TaskBacklog.Slugify(input.Get);

        Assert.All(slug, c => Assert.True(char.IsLetterOrDigit(c) || c == '-'));
        Assert.DoesNotContain("--", slug);
    }
}
```

### 1.8 Summary of Recommendations

#### High Priority (Do First)

1. **Add unit tests for TaskBacklog.cs** - Critical untested domain logic
2. **Migrate to `[GeneratedRegex]` attributes** - Better performance, AOT-ready
3. **Use `field` keyword in properties** - Modernize to C# 14
4. **Add PermissionPolicy tests** - Security-critical code needs coverage
5. **Configure SearchValues for char lookups** - Performance win in .NET 10

#### Medium Priority (Next Sprint)

6. **Introduce IFileSystem abstraction** - Better testability
7. **Add ConfigureAwait(false)** - Optimize async paths
8. **Use primary constructors** - Reduce boilerplate
9. **Extract ConfigurationService** - Better SRP compliance
10. **Add integration tests** - End-to-end validation

#### Low Priority (Future Improvements)

11. **Use PipeReader for event streaming** - Optimize large streams
12. **Add LRU eviction to FileContentCache** - Prevent unbounded growth
13. **Use ArrayPool for temporary buffers** - Reduce allocations
14. **Create extension properties** - More fluent APIs
15. **Add telemetry for cache metrics** - Observability improvement

#### Code Examples for Quick Wins

**Quick Win 1: Generated Regex (5 min)**

```csharp
// TaskBacklog.cs - Replace:
private static readonly Regex ChecklistLineRegex = new(
    @"^\s*[-*+]\s*\[(?<done>[ xX])\]\s+(?<text>.+)$",
    RegexOptions.Compiled);

// With:
[GeneratedRegex(@"^\s*[-*+]\s*\[(?<done>[ xX])\]\s+(?<text>.+)$")]
private static partial Regex ChecklistLineRegex();
```

**Quick Win 2: SearchValues (5 min)**

```csharp
// Add to DockerSandbox.cs:
private static readonly SearchValues<char> QuoteChars = SearchValues.Create([' ', '"']);

// Update Quote method:
private static string Quote(string value)
{
    if (string.IsNullOrEmpty(value))
        return "\"\"";
    if (value.AsSpan().IndexOfAny(QuoteChars) >= 0)
        return $"\"{value.Replace("\"", "\\\"")}\"";
    return value;
}
```

**Quick Win 3: Field keyword (10 min)**

```csharp
// ConsoleOutput.cs - Replace backing fields:
private static IAnsiConsole? _outConsole;
private static IAnsiConsole? _errorConsole;

internal static IAnsiConsole Out => _outConsole ??= CreateConsole(...);

// With:
internal static IAnsiConsole Out
{
    get => field ??= CreateConsole(Console.Out, Console.IsOutputRedirected);
    set => field = value;
}
```

### References & Sources

#### C# 14 Features
- [What's new in C# 14 | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)
- [C# 14 - Exploring extension members - .NET Blog](https://devblogs.microsoft.com/dotnet/csharp-exploring-extension-members/)
- [Extension Properties: C# 14's Game-Changing Feature for Cleaner Code](https://www.daveabrock.com/2025/12/05/extension-properties-c-14s-game-changing-feature-for-cleaner-code/)
- [Introducing C# 14 - .NET Blog](https://devblogs.microsoft.com/dotnet/introducing-csharp-14/)

#### .NET 10 Features
- [What's new in .NET 10 | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)
- [Announcing .NET 10 - .NET Blog](https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/)
- [.NET 10 Arrives with AI Integration, Performance Boosts, and New Tools -- Visual Studio Magazine](https://visualstudiomagazine.com/articles/2025/11/12/net-10-arrives-with-ai-integration-performance-boosts-and-new-tools.aspx)
- [The New Features and Enhancements in .NET 10](https://www.codemag.com/Article/2507051/The-New-Features-and-Enhancements-in-.NET-10)

---

## 2. Documentation Audit

### 2.1 Documentation Inventory

**Core Documentation Files:**
- `README.md` (451 lines) - Installation, quick start, features, architecture overview
- `CONTRIBUTING.md` (86 lines) - Contribution workflow, commit conventions, pre-commit hooks
- `CHANGELOG.md` (105 lines) - Version history and release notes
- `AGENTS.md` (51 lines) - Development notes for agent integration
- `docs/architecture.md` (157 lines) - Architecture diagrams and component descriptions
- `docs/using-with-other-repos.md` (387 lines) - Adapter guide for other tech stacks
- `docs/plans/*.md` (2 files) - Planning documents for bug fixes
- `.github/copilot-instructions.md` (40 lines) - Workflow expectations for Claude Code
- `.github/pull_request_template.md` (7 lines) - Minimal PR template
- `.github/scripts/validate-copilot-instructions.md` - Referenced but not documented

**Configuration Files (Code-as-Documentation):**
- `coralph.config.json` - Configuration schema with defaults
- `justfile` - Build and release automation with embedded help
- `Dockerfile.copilot` - Docker sandbox setup documentation

### 2.2 README.md Analysis

**Strengths:**
- Clear Ralph loop explanation with 5-step workflow
- Comprehensive CLI examples covering all major features
- Good feature coverage: Azure DevOps integration, streaming output, Docker sandbox, custom tools, permission policy
- Configuration reference for coralph.config.json
- Release pipeline documentation with local changelog generation

**Missing Chapters:**
1. **Configuration Reference** (partial) - coralph.config.json schema only partially documented. Missing:
   - `GeneratedTasksFile` option not documented in README
   - `CopilotConfigPath`, `CopilotToken`, `CliPath`, `CliUrl` documented only in examples
   - Default values for all options not in one place
   - Configuration layering (CLI > config file > defaults) not clearly explained

2. **CLI Arguments Reference** - Comprehensive but scattered:
   - Should have a single reference table of all flags
   - Missing descriptions for: `--generated-tasks-file`, `--list-models-json`
   - Missing details on flag validation rules (e.g., `--max-iterations` >= 1)

3. **Custom Tools Documentation** - Listed but underdocumented:
   - `list_open_issues` - no parameters documented
   - `list_generated_tasks` - no parameters documented
   - `get_progress_summary` - says "Retrieve recent progress" but doesn't explain count parameter
   - `search_progress` - searchTerm parameter not documented
   - No examples of tool output format

4. **Permission Policy** - Only 3 lines:
   - How rules are matched (prefix matching with `*`) mentioned but not explained with examples
   - Deny precedence over allow not clearly stated
   - No examples of `--tool-allow` and `--tool-deny` usage
   - No list of valid permission kinds or tool names

5. **Task Backlog Feature** - Completely undocumented:
   - `generated_tasks.json` not explained in README
   - Automatic task generation from PRD-style issues not documented
   - How `list_generated_tasks` tool works not explained
   - Task status management not covered

6. **Docker Sandbox** - Documented but incomplete:
   - Pre-installation requirements listed but scattered across examples
   - The sandbox enables .NET roll-forward mentioned but not why it matters
   - Custom image building covered but Copilot CLI setup in Docker not well explained

7. **Event Stream Format** - Good example but missing:
   - Complete list of event types documented in README but not all are self-evident
   - No guidance on consuming/parsing the JSONL stream
   - Schema validation not mentioned

8. **File Management** - .gitignore guidance only in using-with-other-repos.md:
   - Should be in main README with note about issues.json, progress.txt, generated_tasks.json
   - No guidance on generated_tasks.json lifecycle

### 2.3 Undocumented Features

**Features found in code but not documented in README:**

1. **FileContentCache** (`src/Coralph/FileContentCache.cs`)
   - In-memory caching of file reads with timestamp/size validation
   - Used by CustomTools and TaskBacklog
   - Performance optimization not documented anywhere
   - No mention that repeated file reads are cached

2. **EventStreamWriter** (`src/Coralph/EventStreamWriter.cs`)
   - Batched flushing with `FlushBatchSize = 32` and `ImmediateFlushTypes`
   - Session lifecycle tracking with sequence numbers
   - Not explained in README's event stream section
   - Immediate flush types (session, turn_end, agent_end, event_error) undocumented

3. **Task Backlog Generation** (`src/Coralph/TaskBacklog.cs`)
   - Extracts tasks from checklist items in issues
   - Extracts tasks from ## section headings in large issues (>3000 chars)
   - Generates up to 25 tasks per issue
   - Generic headings filtered (overview, background, context, etc.)
   - This feature NOT mentioned in README at all

4. **CopilotSessionRunner** (`src/Coralph/CopilotSessionRunner.cs`)
   - Appears to be a separate session management component
   - Not mentioned in architecture diagram or docs
   - Purpose and relationship to CopilotRunner unclear

5. **Azure DevOps Integration** (README mentions, but missing details):
   - `AzBoards.cs` handles HTML description parsing
   - Work item type mapping (PBIs, Bugs, Tasks) not documented
   - WIQL query format not explained
   - Error handling for auth failures not covered

6. **Logging System** (`src/Coralph/Logging.cs`)
   - Mentioned in AGENTS.md as Serilog-based
   - Daily rotation with 7-day retention documented
   - But NOT mentioned in README at all
   - Path pattern `logs/coralph-{date}.log` only in AGENTS.md

7. **Banner Component** (`src/Coralph/Banner.cs`)
   - Animated ASCII banner display
   - Version labeling
   - Displayed on startup but conditional logic not documented
   - README doesn't explain when banner shows vs. hides

8. **CopilotModelDiscovery** (`src/Coralph/CopilotModelDiscovery.cs`)
   - `--list-models` and `--list-models-json` flags documented in README
   - But how model discovery works not explained
   - Default model behavior unclear (what is "GPT-5.1-Codex"?)
   - No guidance on choosing models

9. **CopilotDiagnostics** (`src/Coralph/CopilotDiagnostics.cs`)
   - Mentioned in code but not documented
   - Handles diagnostic output on Copilot errors
   - Users may not know how to interpret errors

10. **PromptHelpers** (`src/Coralph/PromptHelpers.cs`)
    - Combines multiple files into single prompt
    - Not documented as a user-facing feature
    - Prompt assembly logic not explained

### 2.4 Inconsistencies

1. **Model Default Values:**
   - `LoopOptions.cs:9` shows default model as `"GPT-5.1-Codex"`
   - `coralph.config.json:4` shows `"claude-sonnet-4.5"`
   - `ArgParser.cs:21` describes it as `"(default: GPT-5.1-Codex)"`
   - README.md examples use various models but don't explain defaults
   - **Issue**: Three different defaults in different places; unclear which is authoritative

2. **File Paths:**
   - `LoopOptions.cs` shows relative path defaults (e.g., `"prompt.md"`)
   - README and docs suggest files are in repo root
   - But actual code uses `Path.GetFullPath()` to resolve them
   - **Issue**: Relative vs. absolute path handling not explained

3. **Docker Sandbox Documentation:**
   - README.md line 239 says default is `mcr.microsoft.com/devcontainers/dotnet:10.0`
   - `LoopOptions.cs:34` confirms this
   - But `coralph.config.json:16` also has this as default
   - Example at README.md:127 shows a different image `ghcr.io/devcontainers/dotnet:10.0`
   - **Issue**: Multiple image references may confuse users

4. **Custom Tools Permission Kinds:**
   - README lists tools: `list_open_issues`, `list_generated_tasks`, `get_progress_summary`, `search_progress`
   - Example shows `--tool-allow list_open_issues,list_generated_tasks,get_progress_summary,search_progress`
   - But `PermissionPolicy.cs` doesn't validate tool names
   - **Issue**: Which permission kinds are valid not clearly documented

5. **Progress File Format:**
   - README.md says "append-only log of completed work and learnings"
   - `.github/copilot-instructions.md:32` says entries follow "structured format in prompt.md"
   - But prompt.md not shown in docs
   - No example of actual progress entry format provided

6. **Issue Refresh Behavior:**
   - README.md says `--refresh-issues` fetches issues
   - But doesn't say if it OVERWRITES or MERGES with existing issues.json
   - Code doesn't show in first 200 lines of Program.cs

### 2.5 Proposed New Documents

**Priority 1: High Value, High Impact**

1. **CLI Reference Guide** (`docs/cli-reference.md`)
   - Complete table of all CLI flags with:
     - Full flag name and short form (if any)
     - Type and validation rules
     - Default value
     - Example usage
     - Related flags
   - Currently scattered across README, ArgParser, and LoopOptions
   - **Size**: ~150 lines

2. **Configuration Guide** (`docs/configuration.md`)
   - Comprehensive coralph.config.json reference
   - JSON schema with all properties
   - Configuration layering explanation
   - How to use environment variables (if supported)
   - Example configurations for common scenarios
   - **Size**: ~100 lines
   - **References**: LoopOptions.cs, coralph.config.json

3. **Custom Tools API Reference** (`docs/custom-tools.md`)
   - Tool definitions with signatures
   - Input parameters and types
   - Output format and examples
   - Permission requirements
   - Use cases and examples
   - **Size**: ~80 lines
   - **References**: CustomTools.cs

**Priority 2: Important, Moderate Impact**

4. **Task Backlog Feature Guide** (`docs/task-backlog.md`)
   - What is task backlog and why it exists
   - How automatic task extraction works
   - Checklist detection from issues
   - PRD-style issue handling
   - Limitations (max 25 tasks per issue)
   - Using `list_generated_tasks` tool
   - **Size**: ~120 lines
   - **References**: TaskBacklog.cs

5. **Permission Policy Guide** (`docs/permission-policy.md`)
   - How permission checking works
   - Prefix matching with `*` wildcards
   - Deny precedence rule
   - Valid permission kinds and tool names
   - CLI flag examples: `--tool-allow`, `--tool-deny`
   - Common scenarios and recipes
   - **Size**: ~90 lines
   - **References**: PermissionPolicy.cs

6. **Architecture Deep Dive** (Extend `docs/architecture.md`)
   - Existing architecture.md is good but missing:
     - FileContentCache optimization strategy
     - EventStreamWriter batching and sequence numbers
     - Task backlog generation algorithm
     - Prompt assembly process (PromptHelpers)
   - **Current**: 157 lines, **Expected**: +100 lines

7. **Logging Guide** (`docs/logging.md`)
   - Structured logging with Serilog
   - Log file locations and rotation policy
   - Log format and how to parse
   - Accessing logs for debugging
   - Properties included in logs
   - **Size**: ~60 lines
   - **References**: Logging.cs, AGENTS.md

**Priority 3: Nice-to-Have, Lower Impact**

8. **Model Selection Guide** (`docs/model-selection.md`)
   - What models are available via --list-models
   - How to choose the right model
   - Model behavior differences
   - Token limits and cost considerations
   - **Size**: ~50 lines
   - **References**: CopilotModelDiscovery.cs

9. **Docker Sandbox Setup** (Extend Docker section in README)
   - Pre-requisites checklist
   - Copilot CLI installation in Docker images
   - Mounting local Copilot config
   - Token-based authentication setup
   - Troubleshooting common Docker issues
   - **Size**: ~80 lines
   - **References**: Dockerfile.copilot, DockerSandbox.cs

10. **Azure DevOps Integration Guide** (New or extend README)
    - Setting up Azure CLI
    - WIQL queries
    - Work item type mapping
    - Authentication setup
    - **Size**: ~70 lines
    - **References**: AzBoards.cs

### 2.6 Summary of Recommendations

**Immediate Actions (Critical):**
1. Create `docs/cli-reference.md` - Users need authoritative list of all flags
2. Create `docs/configuration.md` - Resolve model default inconsistency (currently 3 values)
3. Update README.md to document `GeneratedTasksFile` and task backlog feature
4. Fix `coralph.config.json` to use actual default model (currently shows claude-sonnet-4.5 but code defaults to GPT-5.1-Codex)

**Short-term (High Value):**
1. Create `docs/custom-tools.md` - Tools are underdocumented relative to their importance
2. Create `docs/task-backlog.md` - Major feature completely absent from README
3. Extend `docs/architecture.md` with FileContentCache, EventStreamWriter, and TaskBacklog sections
4. Create `docs/permission-policy.md` - Permission system only has 3 lines in README

**Medium-term (Important but Lower Urgency):**
1. Extend README with .gitignore guidance for generated_tasks.json
2. Create `docs/logging.md` - Currently only in AGENTS.md, not discoverable from README
3. Update `.github/copilot-instructions.md` to reference new docs (especially CLI reference, custom tools)
4. Create examples directory with sample prompt.md files for Python, JavaScript, Go (referenced but missing)

**Low Priority (Nice-to-Have):**
1. Create `docs/model-selection.md` for users choosing between available models
2. Extend Docker documentation with troubleshooting section
3. Create `docs/azure-devops.md` with detailed setup instructions

**Consistency Fixes Needed:**
1. **Model Defaults**: Decide whether default is GPT-5.1-Codex or claude-sonnet-4.5
   - Update LoopOptions.cs, coralph.config.json, ArgParser.cs to match
   - Document this choice in CLI reference

2. **File References**: Clarify which files are created automatically vs. required
   - issues.json - can be empty [], created by --refresh-issues
   - progress.txt - created if missing
   - generated_tasks.json - created if missing
   - prompt.md - required, must exist
   - coralph.config.json - optional

3. **Permission Kinds**: Create authoritative list of valid kinds and tool names
   - Extract from code, document in permission-policy.md
   - Validate in ArgParser or add comment explaining what's valid

**Coverage Assessment:**
- README covers ~70% of features
- Architecture documented in separate file (good)
- Custom tools ~30% documented
- Task backlog 0% documented (major gap)
- Permission policy ~20% documented
- Docker sandbox ~60% documented
- Logging 5% documented (only in AGENTS.md)
- CLI flags 80% documented (scattered across examples)

## 3. Prompt Analysis & Optimization

### 3.1 Prompt Inventory

| # | Location | Type | Purpose | Lines |
|---|----------|------|---------|-------|
| 1 | `prompt.md` | Main system prompt | Loop workflow: issue parsing, task breakdown, selection, execution, commit, close | 140 |
| 2 | `examples/go-prompt.md` | Language template | Go project adapter: toolchain, structure, feedback loops | 52 |
| 3 | `examples/javascript-prompt.md` | Language template | JS/TS project adapter: npm toolchain, structure, feedback loops | 50 |
| 4 | `examples/python-prompt.md` | Language template | Python project adapter: pytest/flake8/black, structure, feedback loops | 50 |
| 5 | `examples/rust-prompt.md` | Language template | Rust project adapter: Cargo toolchain, structure, feedback loops | 50 |
| 6 | `.github/copilot-instructions.md` | Agent context | Repo context, workflow expectations, build commands, conventions | 40 |
| 7 | `AGENTS.md` | Agent context | Overview, core commands, project layout, logging reference | 51 |
| 8 | `PromptHelpers.cs:11-41` | Embedded prompt assembly | Builds combined prompt from template + issues + progress + tasks | 30 (code) |
| 9 | `PromptHelpers.cs:15-16` | Inline system instructions | Two-line "loop context" preamble injected before all data | 2 (inline) |
| 10 | `CustomTools.cs:17-36` | Tool descriptions | Four AI tool descriptions used by the Copilot SDK | 4 (descriptions) |
| 11 | `TaskBacklog.cs` | Implicit prompt contract | Generates `GENERATED_TASKS_JSON` that prompt.md references | 784 (code) |

### 3.2 Main System Prompt Analysis (`prompt.md`)

#### 3.2.1 Clarity — Rating: B+

**Strengths:**
- Clear sentinel values: `NO_OPEN_ISSUES`, `ALL_TASKS_COMPLETE`, `HANG_ON_A_SECOND`, `COMPLETE` (lines 9, 39, 64, 133)
- Explicit task prioritization order with 4 tiers (lines 21-36)
- The "tracer bullet" concept is well-explained with a TL;DR (lines 27-33)
- Output rules section (lines 129-139) provides a strict checklist of 4 conditions for COMPLETE

**Weaknesses:**
- **Ambiguous "iteration" scope**: The prompt says "work on ONE generated task per iteration" (line 131) but also says "ONLY WORK ON A SINGLE GENERATED TASK PER ITERATION" (line 121). The repetition is good for emphasis but "iteration" is never formally defined — it's assumed the LLM understands this equals one invocation of the loop.
- **Conflicting completion signals**: Lines 122-123 say "After completing one issue, DO NOT output COMPLETE" but line 133 uses `<promise>COMPLETE</promise>`. The distinction between completing one *task* vs one *issue* vs *all work* is muddled. The prompt conflates "issue" and "task" in several places.
- **`HANG_ON_A_SECOND` is informal**: Line 64 introduces a signal `HANG_ON_A_SECOND` but there is no corresponding handler in `PromptHelpers.TryGetTerminalSignal()` (lines 88-129 of PromptHelpers.cs). This signal is never parsed by the code — it's a dead instruction.

#### 3.2.2 Structure — Rating: A-

**Strengths:**
- Logical section progression: ISSUES → TASK BREAKDOWN → TASK SELECTION → PRE-FLIGHT CHECK → EXPLORATION → EXECUTION → FEEDBACK LOOPS → PROGRESS → COMMIT → CLOSE THE ISSUE → FINAL RULES → OUTPUT_RULES
- Each section has a clear heading with `#` markdown
- Sections are appropriately sized (2-10 lines each)

**Weaknesses:**
- **FINAL RULES and OUTPUT_RULES are separate sections with overlapping content**: Lines 119-128 and 129-139 both address when to stop. Merging them would reduce confusion.
- **Missing "ROLE" section**: The prompt jumps straight into `# ISSUES` without establishing who the assistant is or what the overall goal is. The role context is instead injected by `PromptHelpers.cs:15-16` as a brief preamble, but it's minimal.

#### 3.2.3 Effectiveness — Rating: B

**Strengths:**
- The 4-condition checklist for COMPLETE (lines 133-137) is excellent — it prevents premature termination
- Pre-flight check (lines 44-53) prevents re-working closed issues
- Feedback loops (lines 74-77) enforce build/test before commit

**Weaknesses:**
- **No error recovery guidance**: If `dotnet build` fails, the prompt doesn't say what to do. Should the LLM fix the error? Skip the task? Output a specific signal?
- **No token/context awareness**: The prompt doesn't warn about context window limits. The `EXPLORATION` section (line 56-57) says "fill your context window with relevant information" which could lead to the model reading too many files.
- **The `<promise>COMPLETE</promise>` tag is fragile**: `PromptHelpers.TryGetTerminalSignal()` checks for this specific XML-like tag (line 95), but models might output variations like `<promise> COMPLETE </promise>` or `COMPLETE` without the tag. The code also checks bare `COMPLETE` (line 109) as a fallback, making the `<promise>` tag redundant.

#### 3.2.4 Logical Flow — Rating: A-

The flow follows a natural development workflow:
1. Understand what needs doing (ISSUES, TASK BREAKDOWN)
2. Pick what to work on (TASK SELECTION)
3. Verify it's still needed (PRE-FLIGHT CHECK)
4. Understand the code (EXPLORATION)
5. Do the work (EXECUTION)
6. Verify the work (FEEDBACK LOOPS)
7. Record what happened (PROGRESS)
8. Save the work (COMMIT)
9. Close the loop (CLOSE THE ISSUE)

**Issue**: The PROGRESS step comes before COMMIT, but the prompt says to "Include progress.txt in your commit" (line 96). This means the LLM must write to progress.txt, then stage it, then commit. The ordering is correct in the prompt but the instruction at line 96 could be clearer about staging order.

#### 3.2.5 Guard Rails — Rating: B-

**Present:**
- Single task per iteration (lines 121, 131)
- Don't re-work completed issues (line 124)
- Don't make unnecessary commits (line 126)
- Must verify 4 conditions before COMPLETE (lines 133-137)

**Missing:**
- No guard against destructive operations (e.g., `git push --force`, `rm -rf`)
- No guard against modifying files outside the repo
- No instruction to avoid committing secrets or sensitive data
- No instruction about what to do if tests fail
- No timeout or scope limit on the EXPLORATION phase

### 3.3 Language-Specific Prompt Templates (`examples/*.md`)

#### 3.3.1 Pattern Consistency — Rating: A

All four language templates follow an identical structure:
1. `## Repo context` — Language and toolchain
2. `## Build and test` — Commands with labels
3. `## Project structure` — ASCII tree diagram
4. `## Feedback loops` — Numbered pre-commit checks
5. `## Coding standards` — 4-5 bullet points
6. `## Changes and logging` — Single line referencing prompt.md

This consistency is excellent for maintainability and for users creating their own templates.

#### 3.3.2 Effectiveness — Rating: B+

**Strengths:**
- Each template correctly identifies the canonical tools for its ecosystem
- Feedback loops are ordered appropriately (tests → lint → build for JS; tests → build → format → lint for Go)
- Coding standards are idiomatic (e.g., "table-driven tests" for Go, "avoid `unwrap()` in production" for Rust)

**Weaknesses:**
- **No parameterization**: These are static files. They don't have placeholders for project name, module structure, or custom commands. A user must manually edit every template.
- **Outdated tooling references**: The JavaScript template references `.eslintrc.js` (line 28) — ESLint v9+ uses `eslint.config.js` flat config. Python template references `flake8` and `black` (line 6) — modern projects often use `ruff` which replaces both.
- **Missing CI/CD integration**: None of the templates mention CI pipelines, which is relevant for the commit/push flow.

#### 3.3.3 Cross-Template Observations

- All templates end with the same `## Changes and logging` section referencing `prompt.md`. This creates a dependency on prompt.md existing in the target repo.
- The Go template includes `golangci-lint` with a conditional "(if available)" (line 39), which is good defensive design. Other templates don't have similar conditionals.
- None of the templates include a `## Role` or `## Goal` section — they assume composition with prompt.md.

### 3.4 Embedded Prompts in Code

#### 3.4.1 `PromptHelpers.BuildCombinedPrompt()` — The Prompt Assembly Engine

**Location**: `PromptHelpers.cs:11-41`

This method assembles the final prompt sent to the Copilot SDK by concatenating:
1. A two-line system preamble (lines 15-16)
2. `ISSUES_JSON` in a fenced code block (lines 19-22)
3. `GENERATED_TASKS_JSON` in a fenced code block (lines 24-28)
4. `PROGRESS_SO_FAR` in a fenced code block (lines 30-34)
5. `INSTRUCTIONS` — the content of prompt.md (lines 36-38)

**Analysis:**

- **Preamble is too terse**: The two-line preamble (lines 15-16) is:
  ```
  You are running inside a loop. Use the files and repository as your source of truth.
  Ignore any pre-existing uncommitted changes in the working tree - focus only on the issues listed below.
  ```
  This provides minimal role context. It doesn't tell the model what kind of assistant it is, what its capabilities are, or what the overall system is. Modern prompt engineering recommends a richer system identity.

- **Data before instructions is suboptimal**: The assembled prompt puts ISSUES_JSON, GENERATED_TASKS_JSON, and PROGRESS_SO_FAR *before* the INSTRUCTIONS. Best practices suggest putting instructions first (or using system messages) so the model understands the framework before processing data. However, for models that process linearly, having data first and instructions after can work as a form of "here's the context, now here's what to do with it." This is a defensible choice but not ideal.

- **JSON inside markdown fences**: Wrapping JSON in `` ```json `` blocks is good for clarity but adds tokens. For large issue sets, this overhead is minimal relative to the JSON itself.

- **Empty fallback for generated tasks**: Line 27 provides a structured empty fallback `{"version":1,"sourceIssueCount":0,"tasks":[]}` which is excellent — it prevents the model from hallucinating a task list.

#### 3.4.2 Custom Tool Descriptions (`CustomTools.cs`)

**Location**: `CustomTools.cs:13-37`

| Tool | Description (in code) | Rating |
|------|----------------------|--------|
| `list_open_issues` | "List all open issues from issues.json with their number, title, body, and state" | A — Clear, specifies output fields |
| `list_generated_tasks` | "List generated tasks from generated_tasks.json with their status" | B — Doesn't mention all returned fields (id, issueNumber, title, description, origin, order) |
| `get_progress_summary` | "Get recent progress entries from progress.txt" | B- — Doesn't explain what "recent" means (default: last 5) |
| `search_progress` | "Search progress.txt for specific terms or phrases" | A- — Clear intent, parameter description good |

**Parameter descriptions are minimal**:
- `list_open_issues` has `[Description("Include closed issues in results")]` — good
- `get_progress_summary` has `[Description("Number of recent entries to return")]` — good but doesn't mention default value
- The descriptions don't mention output format, which means the LLM may not know what to expect from tool results

#### 3.4.3 `PromptHelpers.TryGetTerminalSignal()` — Signal Detection

**Location**: `PromptHelpers.cs:88-129`

This method parses the model's output for terminal signals. The signal detection order:
1. `<promise>COMPLETE</promise>` (exact XML tag, case-insensitive)
2. Bare `COMPLETE` on its own line
3. `ALL_TASKS_COMPLETE` on its own line
4. `NO_OPEN_ISSUES` on its own line

**Analysis:**
- The `TrimMarkdownWrapper()` helper (line 165-168) strips backticks, asterisks, and underscores — this handles cases where the model wraps signals in markdown formatting.
- **Missing signal**: `HANG_ON_A_SECOND` from prompt.md line 64 is NOT detected here. This means if the model outputs this signal, nothing happens — the loop continues as if no signal was sent. This is either a bug or the instruction is vestigial.
- **Signal hierarchy**: `<promise>COMPLETE</promise>` is checked first (line 95) and short-circuits before line-by-line parsing. This means if COMPLETE appears inside an XML tag anywhere in the output (even in a code block discussing the prompt), it will trigger termination. This is a false-positive risk.

### 3.5 Copilot Instructions Analysis (`.github/copilot-instructions.md`)

**Rating: A-**

**Strengths:**
- Well-structured with clear sections: Repo context, Workflow expectations, Build and test, High-level architecture, Run loops, Changes and logging, Key conventions
- Architecture overview (lines 21-24) gives a concise layer-by-layer breakdown
- Build commands include both `dotnet` and `just` alternatives
- The "Key conventions" section (lines 35-39) establishes important invariants:
  - `prompt.md` is source of truth
  - `progress.txt` is append-only
  - Direct push to `main` is expected
  - Test naming convention `*Tests.cs`

**Weaknesses:**
- **Line 7**: "The loop stops early when the assistant outputs a line containing `COMPLETE`" — this is an oversimplification. The actual logic checks for `<promise>COMPLETE</promise>`, bare `COMPLETE`, `ALL_TASKS_COMPLETE`, and `NO_OPEN_ISSUES`. This instruction could mislead a developer working on the signal detection code.
- **Missing `GENERATED_TASKS_JSON` context**: The instructions mention `prompt.md`, `issues.json`, `progress.txt`, and `coralph.config.json` (line 5) but don't mention `generated_tasks.json` as a file dependency, even though `TaskBacklog.cs` generates it and `prompt.md` references it heavily.
- **No mention of `CopilotSessionRunner`**: The architecture section (lines 21-24) lists `CopilotRunner.cs` but not `CopilotSessionRunner.cs`, which is a parallel/alternative session management pattern.

### 3.6 Cross-Prompt Consistency

#### 3.6.1 Terminology

| Term | `prompt.md` | `copilot-instructions.md` | `AGENTS.md` | Language templates |
|------|------------|--------------------------|-------------|-------------------|
| "task" | Used for generated sub-items from issues | Not used | Not used | Not used |
| "issue" | GitHub issue to work on | Mentioned as context file | Not used | Not used |
| "iteration" | One loop cycle | Not used explicitly | Not used | Not used |
| "tracer bullet" | Defined and explained | Referenced ("Prefer small, end-to-end tracer bullets") | Not used | Not used |
| "feedback loops" | Section header for build/test | Not used (uses "Build and test") | Not used (uses "Core Commands") | Section header |

**Key inconsistency**: The concept of "feedback loops" is used in `prompt.md` and all language templates, but `.github/copilot-instructions.md` calls the same concept "Build and test". `AGENTS.md` calls it "Core Commands". This is a terminology fragmentation that could confuse an LLM operating across these files.

#### 3.6.2 Command References

| Command | `prompt.md` | `copilot-instructions.md` | `AGENTS.md` |
|---------|------------|--------------------------|-------------|
| `dotnet build` | Yes (line 76) | Yes, with Release flag | Yes, with Release flag |
| `dotnet test` | Yes (line 77) | Yes, with Release flag | Yes, with Release flag |
| `dotnet format` | Not mentioned | Yes (`--verify-no-changes`) | Yes (`--verify-no-changes`) |
| `just build` | Not mentioned | Yes (alternative) | Not mentioned |
| `just test` | Not mentioned | Yes (alternative) | Not mentioned |
| `just ci` | Not mentioned | Not mentioned | Yes |

**Issue**: `prompt.md` only references `dotnet build` and `dotnet test` but not `dotnet format`. The copilot-instructions.md and AGENTS.md both include format checking. Since the pre-commit hook runs `dotnet format --verify-no-changes`, the prompt should include this as a feedback loop step to prevent commit failures.

#### 3.6.3 Signal Word Consistency

The prompt uses several different "stop" signals:
- `COMPLETE` / `<promise>COMPLETE</promise>` — all work is done
- `ALL_TASKS_COMPLETE` — all generated tasks done
- `NO_OPEN_ISSUES` — no issues to process
- `HANG_ON_A_SECOND` — work is larger than expected (not parsed by code)

The distinction between `COMPLETE` and `ALL_TASKS_COMPLETE` is unclear. Both seem to mean "nothing left to do." Looking at the code:
- `COMPLETE` and `ALL_TASKS_COMPLETE` both cause `TryGetTerminalSignal()` to return true
- But `ContainsComplete()` (line 131-135) only checks for `COMPLETE`, not `ALL_TASKS_COMPLETE`
- The loop in `Program.cs` uses `TryGetTerminalSignal()` (line 311), so both signals stop the loop
- This means `ALL_TASKS_COMPLETE` and `COMPLETE` are functionally identical from the loop's perspective

### 3.7 Optimization Proposals

#### Proposal 1: Add a Role Preamble to `PromptHelpers.BuildCombinedPrompt()`

**Problem**: The current preamble (PromptHelpers.cs:15-16) is too terse. The model has no sense of identity or capabilities.

**Before** (PromptHelpers.cs:15-16):
```csharp
sb.AppendLine("You are running inside a loop. Use the files and repository as your source of truth.");
sb.AppendLine("Ignore any pre-existing uncommitted changes in the working tree - focus only on the issues listed below.");
```

**After**:
```csharp
sb.AppendLine("You are an autonomous software engineering agent running inside the Coralph loop.");
sb.AppendLine("Your job is to pick the next open task, implement it, verify it builds and passes tests, commit, and close the issue.");
sb.AppendLine("Use the repository files as your source of truth. Ignore any pre-existing uncommitted changes in the working tree.");
sb.AppendLine("Focus only on the issues and generated tasks provided below.");
```

**Rationale**: A clearer role description improves task adherence and reduces drift. The additional ~30 tokens are negligible relative to the issues JSON.

#### Proposal 2: Move Instructions Before Data in Assembled Prompt

**Problem**: The current assembly order puts data (ISSUES_JSON, GENERATED_TASKS_JSON, PROGRESS_SO_FAR) before INSTRUCTIONS. This means the model reads potentially thousands of tokens of JSON before understanding what to do with it.

**Before** (PromptHelpers.cs:11-41):
```
[Preamble]
# ISSUES_JSON
[json]
# GENERATED_TASKS_JSON
[json]
# PROGRESS_SO_FAR
[text]
# INSTRUCTIONS
[prompt.md content]
```

**After**:
```
[Preamble]
# INSTRUCTIONS
[prompt.md content]
# ISSUES_JSON
[json]
# GENERATED_TASKS_JSON
[json]
# PROGRESS_SO_FAR
[text]
```

**Rationale**: The model processes instructions first and can then apply them as a framework when reading the data. This is the "instruction → context → action" pattern recommended in modern prompt engineering. Note: This is a moderate confidence recommendation — the current order may work well for models that benefit from having context before instructions. Testing both orderings is recommended.

#### Proposal 3: Remove or Implement `HANG_ON_A_SECOND`

**Problem**: `prompt.md` line 64 introduces `HANG_ON_A_SECOND` as a signal, but `PromptHelpers.TryGetTerminalSignal()` never checks for it. It's dead prompt space.

**Option A — Remove it** (prompt.md:63-64):

**Before**:
```markdown
If you find that the task is larger than you expected (for instance, requires a
refactor first), output "HANG_ON_A_SECOND".
```

**After**:
```markdown
If you find that the task is larger than you expected (for instance, requires a
refactor first), break it into smaller chunks and only complete that chunk.
```

**Option B — Implement it** (PromptHelpers.cs, add after line 119):
```csharp
if (line.Equals("HANG_ON_A_SECOND", StringComparison.OrdinalIgnoreCase))
{
    signal = "HANG_ON_A_SECOND";
    return true;
}
```

**Rationale**: Option A is recommended. The signal serves no purpose if not parsed, and the instruction on lines 66-67 already tells the model what to do (break into smaller chunks). Adding a signal just to detect "I'm pausing" doesn't help the loop — the loop will continue regardless.

#### Proposal 4: Unify FINAL RULES and OUTPUT_RULES

**Problem**: Lines 119-128 (`# FINAL RULES`) and lines 129-139 (`# OUTPUT_RULES`) cover overlapping territory — both address when to stop and what to output.

**Before** (prompt.md:119-139):
```markdown
# FINAL RULES

- ONLY WORK ON A SINGLE GENERATED TASK PER ITERATION
- After completing one issue, DO NOT output COMPLETE - instead, the loop will
  continue and you will work on the next open issue in the next iteration
- Do NOT re-work already completed/closed issues
- Do NOT make unnecessary commits (like updating progress.txt for work already
  logged)
- If nothing needs to be done, output "ALL_TASKS_COMPLETE" and stop

# OUTPUT_RULES

- Work on ONE generated task per iteration. Make real changes to files.
- After making changes, summarize what you did and what remains.
- Only output <promise>COMPLETE</promise> when ALL of these are true:
  1. You made changes in THIS iteration (not just reviewed code)
  2. EVERY task in GENERATED_TASKS_JSON is done (not just the current one)
  3. There is genuinely no remaining work across ALL issues
  4. progress.txt has been updated AND committed (verify with `git status`)
- If you completed one issue but others remain open, do NOT output COMPLETE
- If unsure whether to output COMPLETE, do NOT output it - continue working.
```

**After** (merged into a single `# COMPLETION RULES` section):
```markdown
# COMPLETION RULES

**Per-iteration constraints:**
- Work on exactly ONE generated task per iteration
- Make real changes to files — do not just review code
- Do NOT re-work already completed/closed issues
- Do NOT make unnecessary commits

**After completing a task:**
- Summarize what you did and what remains
- If other tasks remain open, do NOT output COMPLETE — the loop continues

**Terminal signals (use exactly one when appropriate):**
- Output "ALL_TASKS_COMPLETE" if no tasks remain from open issues
- Output <promise>COMPLETE</promise> ONLY when ALL of these are true:
  1. You made changes in THIS iteration
  2. EVERY task in GENERATED_TASKS_JSON is done
  3. There is genuinely no remaining work across ALL issues
  4. progress.txt has been updated AND committed (verify with `git status`)
- If unsure whether to output COMPLETE, do NOT — continue working
```

**Rationale**: Consolidation eliminates redundancy (the "one task per iteration" rule appears twice) and groups related instructions. The bold sub-headings create scannable structure.

#### Proposal 5: Add `dotnet format` to Feedback Loops in `prompt.md`

**Problem**: The pre-commit hook runs `dotnet format --verify-no-changes`, but `prompt.md` only lists `dotnet build` and `dotnet test` as feedback loops. This means the model may commit code that fails the pre-commit hook.

**Before** (prompt.md:74-77):
```markdown
# FEEDBACK LOOPS

Before committing, run the feedback loops:

- `dotnet build` to run the build
- `dotnet test` to run the tests
```

**After**:
```markdown
# FEEDBACK LOOPS

Before committing, run the feedback loops:

1. `dotnet build` — build must succeed
2. `dotnet test` — all tests must pass
3. `dotnet format --verify-no-changes` — code must be formatted
```

**Rationale**: Aligns prompt.md with the pre-commit hook and with `.github/copilot-instructions.md` which already includes `dotnet format`. Using numbered list and em-dash descriptions matches the language template style.

#### Proposal 6: Improve Custom Tool Descriptions

**Problem**: Tool descriptions in `CustomTools.cs` don't specify output format or default parameter values.

**Before** (CustomTools.cs:28-30):
```csharp
AIFunctionFactory.Create(
    ([Description("Number of recent entries to return")] int? count) =>
        GetProgressSummaryAsync(progressFile, count ?? 5),
    "get_progress_summary",
    "Get recent progress entries from progress.txt"
),
```

**After**:
```csharp
AIFunctionFactory.Create(
    ([Description("Number of recent entries to return (default: 5)")] int? count) =>
        GetProgressSummaryAsync(progressFile, count ?? 5),
    "get_progress_summary",
    "Get the most recent progress entries from progress.txt. Returns a JSON object with count and entries array."
),
```

**Apply similar pattern to all tools:**

| Tool | Current Description | Proposed Description |
|------|-------------------|---------------------|
| `list_open_issues` | "List all open issues from issues.json with their number, title, body, and state" | "List open issues from issues.json. Returns JSON with count and array of {number, title, body, state}." |
| `list_generated_tasks` | "List generated tasks from generated_tasks.json with their status" | "List generated tasks from generated_tasks.json. Returns JSON with count and array of {id, issueNumber, title, description, status, origin, order}." |
| `get_progress_summary` | "Get recent progress entries from progress.txt" | "Get the most recent progress entries from progress.txt. Returns JSON with count and entries array." |
| `search_progress` | "Search progress.txt for specific terms or phrases" | "Search progress.txt for matching lines. Returns JSON with searchTerm, matchCount, and matches array." |

**Rationale**: When the model knows the output format, it can plan its next steps without trial-and-error. Including default values in parameter descriptions reduces unnecessary tool calls.

#### Proposal 7: Add Error Recovery Guidance to EXECUTION Section

**Problem**: The prompt doesn't tell the model what to do when builds or tests fail.

**Before** (prompt.md:59-67):
```markdown
# EXECUTION

Complete the task.

If you find that the task is larger than you expected (for instance, requires a
refactor first), output "HANG_ON_A_SECOND".

Then, find a way to break it into smaller chunks and only do that chunk (i.e.
complete the smaller refactor).
```

**After**:
```markdown
# EXECUTION

Complete the task.

If you find that the task is larger than you expected (for instance, requires a
refactor first), break it into smaller chunks and only do that chunk (i.e.
complete the smaller refactor).

**If something goes wrong:**
- Build failure: Fix the compilation error before continuing
- Test failure: Fix the failing test or your code — do not skip tests
- Merge conflict: Resolve the conflict, do not force-push
- If the error is outside your control, mark the task as `blocked` in
  `generated_tasks.json` and move to the next task
```

**Rationale**: Without error recovery instructions, the model may loop endlessly on a broken build, output misleading signals, or make destructive decisions.

#### Proposal 8: Standardize Terminology Across All Prompt Files

**Problem**: The same concept is called different things in different files (see Section 3.6.1).

**Proposed standard vocabulary:**

| Concept | Standard Term | Retire |
|---------|--------------|--------|
| Pre-commit verification steps | "Feedback loops" | "Build and test", "Core Commands" |
| One cycle of the Coralph loop | "Iteration" | "loop", "cycle", "turn" |
| A sub-unit of work from an issue | "Task" (from generated_tasks.json) | "chunk", "item" |
| A GitHub/AzDo work item | "Issue" | "work item" (except in AzDo-specific docs) |

**Action**: Update `.github/copilot-instructions.md` section "Build and test" to "Feedback loops" and update `AGENTS.md` section "Core Commands" to "Feedback loops" (or add a cross-reference).

#### Proposal 9: Simplify Terminal Signals to Just Two

**Problem**: Four different terminal signals (`COMPLETE`, `ALL_TASKS_COMPLETE`, `NO_OPEN_ISSUES`, `HANG_ON_A_SECOND`) create cognitive load and code complexity. In practice, `ALL_TASKS_COMPLETE` and `COMPLETE` are functionally identical (both stop the loop).

**Proposed simplification:**

| Current Signal | Proposed | Rationale |
|---------------|----------|-----------|
| `<promise>COMPLETE</promise>` | Keep | Explicit "all done" marker |
| `ALL_TASKS_COMPLETE` | Merge into `COMPLETE` | Functionally identical |
| `NO_OPEN_ISSUES` | Keep | Different entry condition (no issues at all) |
| `HANG_ON_A_SECOND` | Remove | Never parsed, instruction already covers behavior |

Update prompt.md to use only `COMPLETE` and `NO_OPEN_ISSUES`. Update `PromptHelpers.TryGetTerminalSignal()` to keep `ALL_TASKS_COMPLETE` as an alias for backward compatibility.

#### Proposal 10: Add Exploration Guidance

**Problem**: The EXPLORATION section (prompt.md:54-57) is the vaguest section in the entire prompt — only two lines that say "Explore the repo and fill your context window."

**Before** (prompt.md:54-57):
```markdown
# EXPLORATION

Explore the repo and fill your context window with relevant information that
will allow you to complete the task.
```

**After**:
```markdown
# EXPLORATION

Before making changes, understand the relevant code:

1. Read the files most likely affected by the task
2. Check for existing patterns (naming conventions, error handling, test style)
3. Look at recent commits related to the same area (`git log --oneline -10 -- <path>`)
4. Keep exploration focused — read only what's needed for this specific task
```

**Rationale**: Concrete exploration steps prevent the model from either under-exploring (making changes without understanding context) or over-exploring (reading the entire codebase and exhausting context).

### 3.8 Summary of Recommendations

**Priority 1 — High Impact, Low Effort (fix now):**

1. **Add `dotnet format --verify-no-changes` to feedback loops in prompt.md** — Prevents pre-commit hook failures. 1-line change. (Proposal 5)
2. **Remove or implement `HANG_ON_A_SECOND`** — Dead instruction creates confusion. Recommend removal. 2-line change. (Proposal 3)
3. **Merge FINAL RULES and OUTPUT_RULES** — Eliminate redundancy, improve scannability. (Proposal 4)

**Priority 2 — High Impact, Moderate Effort:**

4. **Improve custom tool descriptions** — Include output format and default values. 4 string changes in CustomTools.cs. (Proposal 6)
5. **Add error recovery guidance to EXECUTION** — Prevents model from getting stuck on failures. ~8 lines added to prompt.md. (Proposal 7)
6. **Expand EXPLORATION section** — Replace vague instruction with concrete steps. ~6 lines. (Proposal 10)
7. **Enrich the system preamble** — Give the model a clearer identity. 4-line change in PromptHelpers.cs. (Proposal 1)

**Priority 3 — Moderate Impact, Higher Effort:**

8. **Standardize terminology across prompt files** — Align "Feedback loops" naming across copilot-instructions.md, AGENTS.md, and prompt.md. (Proposal 8)
9. **Consider moving instructions before data in assembled prompt** — Test both orderings to see which produces better task adherence. (Proposal 2)
10. **Simplify terminal signals** — Reduce from 4 to 2 signals, keeping backward compatibility aliases. (Proposal 9)

**Priority 4 — Low Impact, Good Practice:**

11. **Add `generated_tasks.json` to copilot-instructions.md** file dependencies list
12. **Add `CopilotSessionRunner.cs` to architecture section** in copilot-instructions.md
13. **Update language templates** with modern tooling (ESLint flat config, ruff for Python)
14. **Add conditional "(if available)" qualifiers** to language template tools that may not be installed
