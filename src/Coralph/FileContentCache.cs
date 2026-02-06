namespace Coralph;

internal sealed class FileContentCache
{
    private readonly Dictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    internal static FileContentCache Shared { get; } = new();

    internal async Task<FileReadResult> TryReadTextAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            Invalidate(fullPath);
            return FileReadResult.Missing;
        }

        var lastWriteUtc = File.GetLastWriteTimeUtc(fullPath);
        var length = new FileInfo(fullPath).Length;

        lock (_lock)
        {
            if (_entries.TryGetValue(fullPath, out var cached) &&
                cached.Length == length &&
                cached.LastWriteUtc == lastWriteUtc)
            {
                return new FileReadResult(true, cached.Content);
            }
        }

        string content;
        try
        {
            content = await File.ReadAllTextAsync(fullPath, ct);
        }
        catch (FileNotFoundException)
        {
            Invalidate(fullPath);
            return FileReadResult.Missing;
        }
        catch (DirectoryNotFoundException)
        {
            Invalidate(fullPath);
            return FileReadResult.Missing;
        }

        if (File.Exists(fullPath))
        {
            var updatedEntry = new CacheEntry(
                Length: new FileInfo(fullPath).Length,
                LastWriteUtc: File.GetLastWriteTimeUtc(fullPath),
                Content: content);

            lock (_lock)
            {
                _entries[fullPath] = updatedEntry;
            }
        }

        return new FileReadResult(true, content);
    }

    internal void Invalidate(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var fullPath = Path.GetFullPath(path);
        lock (_lock)
        {
            _entries.Remove(fullPath);
        }
    }

    internal readonly record struct FileReadResult(bool Exists, string Content)
    {
        internal static FileReadResult Missing { get; } = new(false, string.Empty);
    }

    private readonly record struct CacheEntry(long Length, DateTime LastWriteUtc, string Content);
}
