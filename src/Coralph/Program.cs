using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Coralph;
using Figgle;
using Microsoft.Extensions.Configuration;

var (overrides, err, initialConfig, configFile, showHelp) = ArgParser.Parse(args);
if (overrides is null)
{
    if (err is not null)
    {
        Console.Error.WriteLine(err);
        Console.Error.WriteLine();
    }

    var output = err is null ? Console.Out : Console.Error;
    ArgParser.PrintUsage(output);
    return showHelp && err is null ? 0 : 2;
}

if (initialConfig)
{
    var path = configFile ?? LoopOptions.ConfigurationFileName;
    if (File.Exists(path))
    {
        Console.Error.WriteLine($"Refusing to overwrite existing config file: {path}");
        return 1;
    }

    var defaultPayload = new Dictionary<string, LoopOptions>
    {
        [LoopOptions.ConfigurationSectionName] = new LoopOptions()
    };
    var json = JsonSerializer.Serialize(defaultPayload, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(path, json, CancellationToken.None);
    Console.WriteLine($"Wrote default configuration to {path}");
    return 0;
}

var opt = LoadOptions(overrides, configFile);

var banner = FiggleFonts.Standard.Render("Coralph");
Console.WriteLine(banner.TrimEnd());
Console.WriteLine($"Coralph {GetVersionLabel()} | Model: {opt.Model}");

var ct = CancellationToken.None;

if (opt.GenerateIssues)
{
    return await RunIssueGeneratorAsync(opt, ct);
}

if (opt.RefreshIssues)
{
    Console.WriteLine("Refreshing issues...");
    var issuesJson = await GhIssues.FetchOpenIssuesJsonAsync(opt.Repo, ct);
    await File.WriteAllTextAsync(opt.IssuesFile, issuesJson, ct);
}

var promptTemplate = await File.ReadAllTextAsync(opt.PromptFile, ct);
var issues = File.Exists(opt.IssuesFile)
    ? await File.ReadAllTextAsync(opt.IssuesFile, ct)
    : "[]";
var progress = File.Exists(opt.ProgressFile)
    ? await File.ReadAllTextAsync(opt.ProgressFile, ct)
    : string.Empty;
var minimumIterations = Math.Min(opt.MaxIterations, Math.Max(1, CountIssues(issues)));

for (var i = 1; i <= opt.MaxIterations; i++)
{
    Console.WriteLine($"\n=== Iteration {i}/{opt.MaxIterations} ===\n");

    var combinedPrompt = BuildCombinedPrompt(promptTemplate, issues, progress);

    string output;
    try
    {
        output = await CopilotRunner.RunOnceAsync(opt, combinedPrompt, ct);
    }
    catch (Exception ex)
    {
        output = $"ERROR: {ex.GetType().Name}: {ex.Message}";
        Console.Error.WriteLine(output);
    }

    var entry = $"\n\n---\n# Iteration {i} ({DateTimeOffset.UtcNow:O})\n\nModel: {opt.Model}\n\n{output}\n";
    await File.AppendAllTextAsync(opt.ProgressFile, entry, ct);

    progress += entry;

    if (ContainsComplete(output) && i >= minimumIterations)
    {
        Console.WriteLine("\nCOMPLETE detected, stopping.\n");
        break;
    }
}

return 0;

static LoopOptions LoadOptions(LoopOptionsOverrides overrides, string? configFile)
{
    var path = configFile ?? LoopOptions.ConfigurationFileName;
    if (!Path.IsPathRooted(path))
        path = Path.Combine(AppContext.BaseDirectory, path);
    var options = new LoopOptions();

    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile(path, optional: true, reloadOnChange: false)
            .Build();

        config.GetSection(LoopOptions.ConfigurationSectionName).Bind(options);
    }

    ApplyOverrides(options, overrides);
    return options;
}

static void ApplyOverrides(LoopOptions target, LoopOptionsOverrides overrides)
{
    if (overrides.MaxIterations is { } max) target.MaxIterations = max;
    if (!string.IsNullOrWhiteSpace(overrides.Model)) target.Model = overrides.Model;
    if (!string.IsNullOrWhiteSpace(overrides.PromptFile)) target.PromptFile = overrides.PromptFile;
    if (!string.IsNullOrWhiteSpace(overrides.ProgressFile)) target.ProgressFile = overrides.ProgressFile;
    if (!string.IsNullOrWhiteSpace(overrides.IssuesFile)) target.IssuesFile = overrides.IssuesFile;
    if (!string.IsNullOrWhiteSpace(overrides.PrdFile)) target.PrdFile = overrides.PrdFile;
    if (overrides.RefreshIssues is { } refresh) target.RefreshIssues = refresh;
    if (!string.IsNullOrWhiteSpace(overrides.Repo)) target.Repo = overrides.Repo;
    if (overrides.GenerateIssues is { } generateIssues) target.GenerateIssues = generateIssues;
    if (!string.IsNullOrWhiteSpace(overrides.CliPath)) target.CliPath = overrides.CliPath;
    if (!string.IsNullOrWhiteSpace(overrides.CliUrl)) target.CliUrl = overrides.CliUrl;
}

static string BuildCombinedPrompt(string promptTemplate, string issuesJson, string progress)
{
    var sb = new StringBuilder();

    sb.AppendLine("You are running inside a loop. Use the files and repository as your source of truth.");
    sb.AppendLine("Stop condition: when everything is done, output EXACTLY: <promise>COMPLETE</promise>.");
    sb.AppendLine();

    sb.AppendLine("# ISSUES_JSON");
    sb.AppendLine("```json");
    sb.AppendLine(issuesJson.Trim());
    sb.AppendLine("```");
    sb.AppendLine();

    sb.AppendLine("# PROGRESS_SO_FAR");
    sb.AppendLine("```text");
    sb.AppendLine(string.IsNullOrWhiteSpace(progress) ? "(empty)" : progress.Trim());
    sb.AppendLine("```");
    sb.AppendLine();

    sb.AppendLine("# INSTRUCTIONS");
    sb.AppendLine(promptTemplate.Trim());
    sb.AppendLine();

    sb.AppendLine("# OUTPUT_RULES");
    sb.AppendLine("- If you are done, output EXACTLY: <promise>COMPLETE</promise>");
    sb.AppendLine("- Otherwise, output what you changed and what you will do next iteration.");

    return sb.ToString();
}

static async Task<int> RunIssueGeneratorAsync(LoopOptions opt, CancellationToken ct)
{
    if (string.IsNullOrWhiteSpace(opt.PrdFile))
    {
        Console.Error.WriteLine("ERROR: --prd-file is required when using --generate-issues.");
        return 2;
    }

    var prdPath = opt.PrdFile;
    if (!Path.IsPathRooted(prdPath))
    {
        prdPath = Path.Combine(Directory.GetCurrentDirectory(), prdPath);
    }

    if (!File.Exists(prdPath))
    {
        Console.Error.WriteLine($"PRD file not found: {prdPath}");
        return 1;
    }

    var prdContent = await File.ReadAllTextAsync(prdPath, ct);
    if (string.IsNullOrWhiteSpace(prdContent))
    {
        Console.Error.WriteLine("PRD file is empty.");
        return 1;
    }

    var prompt = BuildPrdPrompt(prdContent);

    string output;
    try
    {
        output = await CopilotRunner.RunOnceAsync(opt, prompt, ct);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
        return 1;
    }

    if (string.IsNullOrWhiteSpace(output))
    {
        Console.Error.WriteLine("Copilot returned empty output.");
        return 1;
    }

    Console.WriteLine(output);

    var commands = ExtractGhIssueCommands(output);
    if (commands.Count == 0)
    {
        Console.Error.WriteLine("No `gh issue create` commands found.");
        return 1;
    }

    Console.WriteLine($"\nCreating {commands.Count} issue(s)...");
    foreach (var command in commands)
    {
        var args = NormalizeGhArgs(command, opt.Repo);
        Console.WriteLine($"gh {args}");
        var (exitCode, stdout, stderr) = await RunGhAsync(args, ct);
        if (!string.IsNullOrWhiteSpace(stdout))
        {
            Console.WriteLine(stdout.TrimEnd());
        }

        if (exitCode != 0)
        {
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                Console.Error.WriteLine(stderr.TrimEnd());
            }

            Console.Error.WriteLine($"`gh` failed (exit {exitCode}).");
            return exitCode;
        }
    }

    return 0;
}

static string BuildPrdPrompt(string prdContent)
{
    var sb = new StringBuilder();
    sb.AppendLine("You are the Lead Architect and Product Manager for Coralph, a C#/.NET 10 console app using the GitHub Copilot SDK.");
    sb.AppendLine("Goal: refine the PRD input into a concise PRD and a granular issue plan.");
    sb.AppendLine("Output format:");
    sb.AppendLine("## PRD: [Feature Name]");
    sb.AppendLine("Objective, Technical Implementation (reference Coralph: ArgParser.cs, LoopOptions.cs, Program.cs, CopilotRunner.cs), Verification.");
    sb.AppendLine();
    sb.AppendLine("## Implementation Plan");
    sb.AppendLine("Provide GitHub CLI commands as a bash code block:");
    sb.AppendLine("```bash");
    sb.AppendLine("gh issue create --title \"...\" --body \"Line 1\\nLine 2\" --label \"enhancement\"");
    sb.AppendLine("```");
    sb.AppendLine();
    sb.AppendLine("Rules:");
    sb.AppendLine("- First issue must be a tracer bullet (skeleton end-to-end slice).");
    sb.AppendLine("- Each issue adds one small, specific increment (no epics).");
    sb.AppendLine("- Use single-line gh commands; escape newlines in --body with \\n.");
    sb.AppendLine("- Use label \"documentation\" only for docs-only issues; otherwise \"enhancement\".");
    sb.AppendLine("- Output only the PRD and Implementation Plan (no extra commentary).");
    sb.AppendLine();
    sb.AppendLine("# PRD_INPUT");
    sb.AppendLine("```markdown");
    sb.AppendLine(prdContent.Trim());
    sb.AppendLine("```");
    return sb.ToString();
}

static List<string> ExtractGhIssueCommands(string output)
{
    var normalized = output.Replace("\r\n", "\n");
    var lines = normalized.Split('\n');
    var fenced = ExtractGhIssueCommandsFromLines(lines, onlyInsideFence: true);
    return fenced.Count > 0 ? fenced : ExtractGhIssueCommandsFromLines(lines, onlyInsideFence: false);
}

static List<string> ExtractGhIssueCommandsFromLines(string[] lines, bool onlyInsideFence)
{
    var commands = new List<string>();
    var insideFence = false;
    string? current = null;

    foreach (var rawLine in lines)
    {
        var line = rawLine.Trim();
        if (line.StartsWith("```", StringComparison.Ordinal))
        {
            insideFence = !insideFence;
            if (!insideFence && current is not null)
            {
                commands.Add(current);
                current = null;
            }
            continue;
        }

        if (onlyInsideFence && !insideFence)
        {
            continue;
        }

        if (current is not null)
        {
            current = $"{current} {line.TrimEnd('\\').Trim()}".Trim();
            if (!line.EndsWith("\\", StringComparison.Ordinal))
            {
                commands.Add(current);
                current = null;
            }
            continue;
        }

        if (line.StartsWith("gh issue create ", StringComparison.Ordinal))
        {
            if (line.EndsWith("\\", StringComparison.Ordinal))
            {
                current = line.TrimEnd('\\').Trim();
            }
            else
            {
                commands.Add(line);
            }
        }
    }

    if (current is not null)
    {
        commands.Add(current);
    }

    return commands;
}

static string NormalizeGhArgs(string command, string? repo)
{
    var args = command.Trim();
    if (args.StartsWith("gh ", StringComparison.OrdinalIgnoreCase))
    {
        args = args[3..].Trim();
    }

    if (!string.IsNullOrWhiteSpace(repo) && !HasRepoArg(args))
    {
        args = $"{args} --repo {repo}";
    }

    return args;
}

static bool HasRepoArg(string args)
{
    return args.Contains("--repo ", StringComparison.OrdinalIgnoreCase)
           || args.Contains("--repo=", StringComparison.OrdinalIgnoreCase)
           || args.Contains(" -R ", StringComparison.OrdinalIgnoreCase)
           || args.StartsWith("-R ", StringComparison.OrdinalIgnoreCase);
}

static async Task<(int ExitCode, string Stdout, string Stderr)> RunGhAsync(string args, CancellationToken ct)
{
    var psi = new ProcessStartInfo("gh", args)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };

    using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start `gh`");
    var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
    var stderrTask = process.StandardError.ReadToEndAsync(ct);

    await process.WaitForExitAsync(ct);
    var stdout = await stdoutTask;
    var stderr = await stderrTask;

    return (process.ExitCode, stdout, stderr);
}

static bool ContainsComplete(string output)
{
    if (output.Contains("<promise>COMPLETE</promise>", StringComparison.OrdinalIgnoreCase))
        return true;

    // Back-compat with older sentinel
    return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Any(l => string.Equals(l, "COMPLETE", StringComparison.OrdinalIgnoreCase));
}

static int CountIssues(string issuesJson)
{
    if (string.IsNullOrWhiteSpace(issuesJson))
        return 0;

    try
    {
        using var doc = JsonDocument.Parse(issuesJson);
        return doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.GetArrayLength() : 0;
    }
    catch (JsonException)
    {
        return 0;
    }
}

static string GetVersionLabel()
{
    var assembly = Assembly.GetExecutingAssembly();
    var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    if (!string.IsNullOrWhiteSpace(info))
        return info;
    return assembly.GetName().Version?.ToString() ?? "unknown";
}
