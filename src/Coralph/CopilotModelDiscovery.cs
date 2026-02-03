using GitHub.Copilot.SDK;
using Serilog;

namespace Coralph;

internal static class CopilotModelDiscovery
{
    internal static async Task<IReadOnlyList<ModelInfo>> ListModelsAsync(LoopOptions opt, CancellationToken ct)
    {
        var clientOptions = new CopilotClientOptions
        {
            Cwd = Directory.GetCurrentDirectory(),
        };

        if (!string.IsNullOrWhiteSpace(opt.CliPath)) clientOptions.CliPath = opt.CliPath;
        if (!string.IsNullOrWhiteSpace(opt.CliUrl)) clientOptions.CliUrl = opt.CliUrl;
        if (!string.IsNullOrWhiteSpace(opt.CopilotToken)) clientOptions.GithubToken = opt.CopilotToken;

        await using var client = new CopilotClient(clientOptions);
        var started = false;

        try
        {
            await client.StartAsync();
            started = true;
            return await client.ListModelsAsync(ct);
        }
        finally
        {
            if (started)
            {
                try
                {
                    await client.StopAsync();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to stop Copilot client");
                }
            }
        }
    }

    internal static void WriteModels(IEnumerable<ModelInfo> models)
    {
        ConsoleOutput.WriteLine("Available models:");
        ConsoleOutput.WriteLine("id\tname\tvision\tmax_context_tokens\tmax_prompt_tokens\tbilling_multiplier\tpolicy_state");

        foreach (var model in models.OrderBy(m => m.Id, StringComparer.OrdinalIgnoreCase))
        {
            var id = model.Id ?? string.Empty;
            var name = model.Name ?? string.Empty;
            var vision = model.Capabilities?.Supports?.Vision == true ? "yes" : "no";
            var maxContext = model.Capabilities?.Limits?.MaxContextWindowTokens;
            var maxPrompt = model.Capabilities?.Limits?.MaxPromptTokens;
            var billing = model.Billing?.Multiplier;
            var policy = model.Policy?.State ?? string.Empty;

            ConsoleOutput.WriteLine(
                $"{id}\t{name}\t{vision}\t{FormatNullable(maxContext)}\t{FormatNullable(maxPrompt)}\t{FormatNullable(billing)}\t{policy}");
        }
    }

    private static string FormatNullable(int? value) => value?.ToString() ?? "-";

    private static string FormatNullable(double? value)
    {
        return value.HasValue ? value.Value.ToString("0.###") : "-";
    }
}
