using System.Globalization;

namespace Coralph;

internal static class ArgParser
{
    internal static (LoopOptionsOverrides? Overrides, string? Error, bool PrintInitialConfig, string? ConfigFile) Parse(string[] args)
    {
        var overrides = new LoopOptionsOverrides();
        string? configFile = null;
        var printInitialConfig = false;

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "-h":
                case "--help":
                    return (null, null, printInitialConfig, configFile);

                case "--max-iterations":
                    if (!TryGetValue(args, ref i, out var it) ||
                        !int.TryParse(it, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxIterations) ||
                        maxIterations < 1)
                    {
                        return (null, "--max-iterations must be an integer >= 1", printInitialConfig, configFile);
                    }
                    overrides.MaxIterations = maxIterations;
                    break;

                case "--model":
                    if (!TryGetValue(args, ref i, out var model) || string.IsNullOrWhiteSpace(model))
                        return (null, "--model is required", printInitialConfig, configFile);
                    overrides.Model = model;
                    break;

                case "--prompt-file":
                    if (!TryGetValue(args, ref i, out var promptFile) || string.IsNullOrWhiteSpace(promptFile))
                        return (null, "--prompt-file is required", printInitialConfig, configFile);
                    overrides.PromptFile = promptFile;
                    break;

                case "--progress-file":
                    if (!TryGetValue(args, ref i, out var progressFile) || string.IsNullOrWhiteSpace(progressFile))
                        return (null, "--progress-file is required", printInitialConfig, configFile);
                    overrides.ProgressFile = progressFile;
                    break;

                case "--issues-file":
                    if (!TryGetValue(args, ref i, out var issuesFile) || string.IsNullOrWhiteSpace(issuesFile))
                        return (null, "--issues-file is required", printInitialConfig, configFile);
                    overrides.IssuesFile = issuesFile;
                    break;

                case "--refresh-issues":
                    overrides.RefreshIssues = true;
                    break;

                case "--repo":
                    if (!TryGetValue(args, ref i, out var r) || string.IsNullOrWhiteSpace(r))
                        return (null, "--repo is required", printInitialConfig, configFile);
                    overrides.Repo = r;
                    break;

                case "--cli-path":
                    if (!TryGetValue(args, ref i, out var cp) || string.IsNullOrWhiteSpace(cp))
                        return (null, "--cli-path is required", printInitialConfig, configFile);
                    overrides.CliPath = cp;
                    break;

                case "--cli-url":
                    if (!TryGetValue(args, ref i, out var cu) || string.IsNullOrWhiteSpace(cu))
                        return (null, "--cli-url is required", printInitialConfig, configFile);
                    overrides.CliUrl = cu;
                    break;

                case "--config":
                    if (!TryGetValue(args, ref i, out configFile) || string.IsNullOrWhiteSpace(configFile))
                        return (null, "--config is required", printInitialConfig, configFile);
                    break;

                case "--initial-config":
                    printInitialConfig = true;
                    break;

                default:
                    return (null, $"Unknown argument: {a}", printInitialConfig, configFile);
            }
        }

        return (overrides, null, printInitialConfig, configFile);
    }

    private static bool TryGetValue(string[] args, ref int i, out string value)
    {
        if (i + 1 >= args.Length)
        {
            value = string.Empty;
            return false;
        }
        i++;
        value = args[i];
        return true;
    }

    internal static void PrintUsage(TextWriter w)
    {
        w.WriteLine("Coralph - Ralph loop runner using GitHub Copilot SDK");
        w.WriteLine();
        w.WriteLine("Usage:");
        w.WriteLine("  dotnet run --project src/Coralph -- [options]");
        w.WriteLine();
        w.WriteLine("Options:");
        w.WriteLine("  -h, --help             Show help");
        w.WriteLine("  --max-iterations <n>   Max loop iterations (default: 10)");
        w.WriteLine("  --model <name>         Model (default: gpt-5.1-codex)");
        w.WriteLine("  --prompt-file <path>   Prompt file (default: prompt.md)");
        w.WriteLine("  --progress-file <path> Progress file (default: progress.txt)");
        w.WriteLine("  --issues-file <path>   Issues json file (default: issues.json)");
        w.WriteLine("  --refresh-issues       Refresh issues.json via `gh issue list`");
        w.WriteLine("  --repo <owner/name>    Optional repo override for gh");
        w.WriteLine("  --cli-path <path>      Optional: Copilot CLI executable path");
        w.WriteLine("  --cli-url <host:port>  Optional: connect to existing CLI server");
        w.WriteLine("  --config <path>        Optional: JSON config file (default: coralph.config.json)");
        w.WriteLine("  --initial-config       Writes default config json and exits");
        w.WriteLine();
        w.WriteLine("The loop stops early when the assistant output contains the sentinel: COMPLETE");
    }
}
