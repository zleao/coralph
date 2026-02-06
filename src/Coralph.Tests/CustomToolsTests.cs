using System.Text.Json;
using Coralph;

namespace Coralph.Tests;

public class CustomToolsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _issuesFile;
    private readonly string _progressFile;
    private readonly string _generatedTasksFile;

    public CustomToolsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"coralph-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        _issuesFile = Path.Combine(_tempDir, "issues.json");
        _progressFile = Path.Combine(_tempDir, "progress.txt");
        _generatedTasksFile = Path.Combine(_tempDir, "generated_tasks.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private static string Serialize(object obj) => JsonSerializer.Serialize(obj);

    [Fact]
    public void GetDefaultTools_ReturnsFourTools()
    {
        var tools = CustomTools.GetDefaultTools(_issuesFile, _progressFile, _generatedTasksFile);

        Assert.Equal(4, tools.Length);
    }

    [Fact]
    public void GetDefaultTools_ContainsListOpenIssues()
    {
        var tools = CustomTools.GetDefaultTools(_issuesFile, _progressFile, _generatedTasksFile);

        Assert.Contains(tools, t => t.Name == "list_open_issues");
    }

    [Fact]
    public void GetDefaultTools_ContainsListGeneratedTasks()
    {
        var tools = CustomTools.GetDefaultTools(_issuesFile, _progressFile, _generatedTasksFile);

        Assert.Contains(tools, t => t.Name == "list_generated_tasks");
    }

    [Fact]
    public void GetDefaultTools_ContainsGetProgressSummary()
    {
        var tools = CustomTools.GetDefaultTools(_issuesFile, _progressFile, _generatedTasksFile);

        Assert.Contains(tools, t => t.Name == "get_progress_summary");
    }

    [Fact]
    public void GetDefaultTools_ContainsSearchProgress()
    {
        var tools = CustomTools.GetDefaultTools(_issuesFile, _progressFile, _generatedTasksFile);

        Assert.Contains(tools, t => t.Name == "search_progress");
    }

    #region ListOpenIssuesAsync Tests

    [Fact]
    public async Task ListOpenIssuesAsync_WithMissingFile_ReturnsError()
    {
        var result = await CustomTools.ListOpenIssuesAsync(_issuesFile, false);

        var resultStr = Serialize(result);
        Assert.Contains("issues.json not found", resultStr);
    }

    [Fact]
    public async Task ListOpenIssuesAsync_WithValidIssues_ReturnsOpenIssues()
    {
        await File.WriteAllTextAsync(_issuesFile, """
            [
                {"number": 1, "title": "Bug", "body": "Fix it", "state": "open"},
                {"number": 2, "title": "Feature", "body": "Add it", "state": "closed"}
            ]
            """);

        var result = await CustomTools.ListOpenIssuesAsync(_issuesFile, false);

        var resultStr = Serialize(result);
        Assert.Contains("Bug", resultStr);
        Assert.DoesNotContain("Feature", resultStr);
    }

    [Fact]
    public async Task ListOpenIssuesAsync_WithIncludeClosed_ReturnsAllIssues()
    {
        await File.WriteAllTextAsync(_issuesFile, """
            [
                {"number": 1, "title": "Bug", "body": "Fix it", "state": "open"},
                {"number": 2, "title": "Feature", "body": "Add it", "state": "closed"}
            ]
            """);

        var result = await CustomTools.ListOpenIssuesAsync(_issuesFile, true);

        var resultStr = Serialize(result);
        Assert.Contains("Bug", resultStr);
        Assert.Contains("Feature", resultStr);
    }

    [Fact]
    public async Task ListOpenIssuesAsync_WithNoState_DefaultsToOpen()
    {
        await File.WriteAllTextAsync(_issuesFile, """
            [{"number": 1, "title": "No State Issue", "body": "Test"}]
            """);

        var result = await CustomTools.ListOpenIssuesAsync(_issuesFile, false);

        var resultStr = Serialize(result);
        Assert.Contains("No State Issue", resultStr);
    }

    #endregion

    #region ListGeneratedTasksAsync Tests

    [Fact]
    public async Task ListGeneratedTasksAsync_WithMissingFile_ReturnsError()
    {
        var result = await CustomTools.ListGeneratedTasksAsync(_generatedTasksFile, false);

        var resultStr = Serialize(result);
        Assert.Contains("generated_tasks.json not found", resultStr);
    }

    [Fact]
    public async Task ListGeneratedTasksAsync_WithValidTasks_ReturnsOnlyOpenByDefault()
    {
        await File.WriteAllTextAsync(_generatedTasksFile, """
            {
              "version": 1,
              "tasks": [
                {"id": "1-001", "issueNumber": 1, "title": "Open Task", "description": "Do it", "status": "open", "origin": "list", "order": 1},
                {"id": "1-002", "issueNumber": 1, "title": "Done Task", "description": "Done", "status": "done", "origin": "list", "order": 2}
              ]
            }
            """);

        var result = await CustomTools.ListGeneratedTasksAsync(_generatedTasksFile, false);

        var resultStr = Serialize(result);
        Assert.Contains("Open Task", resultStr);
        Assert.DoesNotContain("Done Task", resultStr);
    }

    [Fact]
    public async Task ListGeneratedTasksAsync_WithIncludeCompleted_ReturnsAllTasks()
    {
        await File.WriteAllTextAsync(_generatedTasksFile, """
            {
              "version": 1,
              "tasks": [
                {"id": "1-001", "issueNumber": 1, "title": "Open Task", "description": "Do it", "status": "open", "origin": "list", "order": 1},
                {"id": "1-002", "issueNumber": 1, "title": "Done Task", "description": "Done", "status": "done", "origin": "list", "order": 2}
              ]
            }
            """);

        var result = await CustomTools.ListGeneratedTasksAsync(_generatedTasksFile, true);

        var resultStr = Serialize(result);
        Assert.Contains("Open Task", resultStr);
        Assert.Contains("Done Task", resultStr);
    }

    #endregion

    #region GetProgressSummaryAsync Tests

    [Fact]
    public async Task GetProgressSummaryAsync_WithMissingFile_ReturnsError()
    {
        var result = await CustomTools.GetProgressSummaryAsync(_progressFile, 5);

        var resultStr = Serialize(result);
        Assert.Contains("progress.txt not found", resultStr);
    }

    [Fact]
    public async Task GetProgressSummaryAsync_WithEmptyFile_ReturnsEmpty()
    {
        await File.WriteAllTextAsync(_progressFile, "");

        var result = await CustomTools.GetProgressSummaryAsync(_progressFile, 5);

        var resultStr = Serialize(result);
        Assert.Contains("empty", resultStr);
    }

    [Fact]
    public async Task GetProgressSummaryAsync_WithEntries_ReturnsRecentEntries()
    {
        await File.WriteAllTextAsync(_progressFile, """
            Entry 1
            ---
            Entry 2
            ---
            Entry 3
            """);

        var result = await CustomTools.GetProgressSummaryAsync(_progressFile, 2);

        var resultStr = Serialize(result);
        Assert.Contains("Entry 2", resultStr);
        Assert.Contains("Entry 3", resultStr);
    }

    #endregion

    #region SearchProgressAsync Tests

    [Fact]
    public async Task SearchProgressAsync_WithMissingFile_ReturnsError()
    {
        var result = await CustomTools.SearchProgressAsync(_progressFile, "test");

        var resultStr = Serialize(result);
        Assert.Contains("progress.txt not found", resultStr);
    }

    [Fact]
    public async Task SearchProgressAsync_WithEmptySearchTerm_ReturnsError()
    {
        await File.WriteAllTextAsync(_progressFile, "Some content");

        var result = await CustomTools.SearchProgressAsync(_progressFile, "");

        var resultStr = Serialize(result);
        Assert.Contains("searchTerm cannot be empty", resultStr);
    }

    [Fact]
    public async Task SearchProgressAsync_WithMatchingTerm_ReturnsMatches()
    {
        await File.WriteAllTextAsync(_progressFile, """
            Line with bug fix
            Another line
            Bug related line
            """);

        var result = await CustomTools.SearchProgressAsync(_progressFile, "bug");

        var resultStr = Serialize(result);
        Assert.Contains("bug fix", resultStr);
        Assert.Contains("Bug related", resultStr);
    }

    [Fact]
    public async Task SearchProgressAsync_WithNoMatches_ReturnsZeroCount()
    {
        await File.WriteAllTextAsync(_progressFile, """
            Line one
            Line two
            """);

        var result = await CustomTools.SearchProgressAsync(_progressFile, "xyz123");

        var resultStr = Serialize(result);
        Assert.Contains("\"matchCount\":0", resultStr);
    }

    #endregion
}
