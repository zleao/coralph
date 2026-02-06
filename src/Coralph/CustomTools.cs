using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Coralph;

internal static class CustomTools
{
    internal static AIFunction[] GetDefaultTools(string issuesFile, string progressFile, string generatedTasksFile)
    {
        return
        [
            AIFunctionFactory.Create(
                ([Description("Include closed issues in results")] bool? includeClosed) =>
                    ListOpenIssuesAsync(issuesFile, includeClosed ?? false),
                "list_open_issues",
                "List all open issues from issues.json with their number, title, body, and state"
            ),
            AIFunctionFactory.Create(
                ([Description("Include completed tasks in results")] bool? includeCompleted) =>
                    ListGeneratedTasksAsync(generatedTasksFile, includeCompleted ?? false),
                "list_generated_tasks",
                "List generated tasks from generated_tasks.json with their status"
            ),
            AIFunctionFactory.Create(
                ([Description("Number of recent entries to return")] int? count) =>
                    GetProgressSummaryAsync(progressFile, count ?? 5),
                "get_progress_summary",
                "Get recent progress entries from progress.txt"
            ),
            AIFunctionFactory.Create(
                ([Description("Search term to find in progress")] string searchTerm) =>
                    SearchProgressAsync(progressFile, searchTerm),
                "search_progress",
                "Search progress.txt for specific terms or phrases"
            ),
        ];
    }

    internal static async Task<object> ListOpenIssuesAsync(string issuesFile, bool includeClosed)
    {
        var issuesRead = await FileContentCache.Shared.TryReadTextAsync(issuesFile);
        if (!issuesRead.Exists)
        {
            return new { error = "issues.json not found", issues = Array.Empty<object>() };
        }

        var json = issuesRead.Content;
        using var doc = JsonDocument.Parse(json);
        var issues = new List<object>();

        foreach (var issue in doc.RootElement.EnumerateArray())
        {
            if (issue.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var state = issue.TryGetProperty("state", out var stateProp) && stateProp.ValueKind == JsonValueKind.String
                ? stateProp.GetString()
                : "open";

            if (!includeClosed && string.Equals(state, "closed", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var number = issue.TryGetProperty("number", out var numberProp) ? numberProp.GetInt32() : 0;
            var title = issue.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String
                ? titleProp.GetString()
                : string.Empty;
            var body = issue.TryGetProperty("body", out var bodyProp) && bodyProp.ValueKind == JsonValueKind.String
                ? bodyProp.GetString()
                : string.Empty;

            issues.Add(new { number, title, body, state });
        }

        return new { count = issues.Count, issues };
    }

    internal static async Task<object> ListGeneratedTasksAsync(string generatedTasksFile, bool includeCompleted)
    {
        var tasksRead = await FileContentCache.Shared.TryReadTextAsync(generatedTasksFile);
        if (!tasksRead.Exists)
        {
            return new { error = "generated_tasks.json not found", tasks = Array.Empty<object>() };
        }

        var json = tasksRead.Content;
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        JsonElement tasksArray;
        if (root.ValueKind == JsonValueKind.Array)
        {
            tasksArray = root;
        }
        else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("tasks", out var tasksProp) && tasksProp.ValueKind == JsonValueKind.Array)
        {
            tasksArray = tasksProp;
        }
        else
        {
            return new { error = "generated_tasks.json has an unexpected format", tasks = Array.Empty<object>() };
        }

        var tasks = new List<object>();
        foreach (var task in tasksArray.EnumerateArray())
        {
            if (task.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var status = task.TryGetProperty("status", out var statusProp) && statusProp.ValueKind == JsonValueKind.String
                ? statusProp.GetString() ?? "open"
                : "open";
            var normalizedStatus = status.Trim().ToLowerInvariant();

            if (!includeCompleted && (normalizedStatus == "done" || normalizedStatus == "completed" || normalizedStatus == "complete"))
            {
                continue;
            }

            var id = task.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String
                ? idProp.GetString()
                : string.Empty;
            var issueNumber = task.TryGetProperty("issueNumber", out var issueNumberProp) && issueNumberProp.TryGetInt32(out var parsedIssueNumber)
                ? parsedIssueNumber
                : 0;
            var title = task.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String
                ? titleProp.GetString()
                : string.Empty;
            var description = task.TryGetProperty("description", out var descriptionProp) && descriptionProp.ValueKind == JsonValueKind.String
                ? descriptionProp.GetString()
                : string.Empty;
            var origin = task.TryGetProperty("origin", out var originProp) && originProp.ValueKind == JsonValueKind.String
                ? originProp.GetString()
                : string.Empty;
            var order = task.TryGetProperty("order", out var orderProp) && orderProp.TryGetInt32(out var parsedOrder)
                ? parsedOrder
                : 0;

            tasks.Add(new { id, issueNumber, title, description, status = normalizedStatus, origin, order });
        }

        return new { count = tasks.Count, tasks };
    }

    internal static async Task<object> GetProgressSummaryAsync(string progressFile, int count)
    {
        var progressRead = await FileContentCache.Shared.TryReadTextAsync(progressFile);
        if (!progressRead.Exists)
        {
            return new { error = "progress.txt not found", entries = Array.Empty<string>() };
        }

        var content = progressRead.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            return new { message = "progress.txt is empty", entries = Array.Empty<string>() };
        }

        var entries = content.Split(new[] { "\n---\n", "\r\n---\r\n" }, StringSplitOptions.RemoveEmptyEntries)
            .TakeLast(count)
            .ToArray();

        return new { count = entries.Length, entries };
    }

    internal static async Task<object> SearchProgressAsync(string progressFile, string searchTerm)
    {
        var progressRead = await FileContentCache.Shared.TryReadTextAsync(progressFile);
        if (!progressRead.Exists)
        {
            return new { error = "progress.txt not found", matches = Array.Empty<string>() };
        }

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new { error = "searchTerm cannot be empty", matches = Array.Empty<string>() };
        }

        var content = progressRead.Content;
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var matches = lines.Where(line => line.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return new { searchTerm, matchCount = matches.Length, matches };
    }
}
