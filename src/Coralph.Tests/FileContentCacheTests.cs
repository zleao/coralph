using Coralph;

namespace Coralph.Tests;

public sealed class FileContentCacheTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _filePath;

    public FileContentCacheTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"coralph-file-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
        _filePath = Path.Combine(_tempDirectory, "sample.txt");
    }

    [Fact]
    public async Task TryReadTextAsync_WithMissingFile_ReturnsMissing()
    {
        var cache = new FileContentCache();

        var result = await cache.TryReadTextAsync(_filePath);

        Assert.False(result.Exists);
        Assert.Equal(string.Empty, result.Content);
    }

    [Fact]
    public async Task TryReadTextAsync_WhenFileChanges_RefreshesCache()
    {
        var cache = new FileContentCache();
        await File.WriteAllTextAsync(_filePath, "initial");
        File.SetLastWriteTimeUtc(_filePath, DateTime.UnixEpoch.AddSeconds(1));

        var initial = await cache.TryReadTextAsync(_filePath);
        Assert.True(initial.Exists);
        Assert.Equal("initial", initial.Content);

        await File.WriteAllTextAsync(_filePath, "updated");
        File.SetLastWriteTimeUtc(_filePath, DateTime.UnixEpoch.AddSeconds(2));

        var updated = await cache.TryReadTextAsync(_filePath);
        Assert.True(updated.Exists);
        Assert.Equal("updated", updated.Content);
    }

    [Fact]
    public async Task Invalidate_ForcesReloadEvenWithSameMetadata()
    {
        var cache = new FileContentCache();
        await File.WriteAllTextAsync(_filePath, "alpha");
        var timestamp = DateTime.UnixEpoch.AddSeconds(10);
        File.SetLastWriteTimeUtc(_filePath, timestamp);

        var first = await cache.TryReadTextAsync(_filePath);
        Assert.True(first.Exists);
        Assert.Equal("alpha", first.Content);

        await File.WriteAllTextAsync(_filePath, "bravo");
        File.SetLastWriteTimeUtc(_filePath, timestamp);

        var stale = await cache.TryReadTextAsync(_filePath);
        Assert.True(stale.Exists);
        Assert.Equal("alpha", stale.Content);

        cache.Invalidate(_filePath);
        var refreshed = await cache.TryReadTextAsync(_filePath);
        Assert.True(refreshed.Exists);
        Assert.Equal("bravo", refreshed.Content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}
