using System.Text;
using OrchardCore.FileStorage;
using OrchardCore.Media;

namespace CrestApps.OrchardCore.Tests.Agent.Media;

/// <summary>
/// A media store that keeps its folders and files in memory and records every change, so a media tool test can show
/// what a tool read and what it changed.
/// </summary>
internal sealed class InMemoryMediaFileStore : IMediaFileStore
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
    private readonly HashSet<string> _folders = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the changes the store was asked to make, in order: <c>create:path</c>, <c>replace:path</c>,
    /// <c>move:from&gt;to</c>, <c>copy:from&gt;to</c>, <c>delete:path</c>, <c>createFolder:path</c> and
    /// <c>deleteFolder:path</c>.
    /// </summary>
    public List<string> Changes { get; } = [];

    /// <summary>
    /// Gets or sets the time every file and folder was last changed.
    /// </summary>
    public DateTime LastModifiedUtc { get; set; } = new(2026, 11, 16, 9, 30, 0, DateTimeKind.Utc);

    /// <summary>
    /// Gets or sets the storage the store still permits, in bytes.
    /// </summary>
    public long? PermittedStorage { get; set; }

    /// <summary>
    /// Gets or sets the message of the error the store throws when a file is moved, to stand in for a store failure.
    /// </summary>
    public string MoveError { get; set; }

    public IFileStoreCapabilities Capabilities { get; } = new FileStoreCapabilities(hasHierarchicalNamespace: true, supportsAtomicMove: true);

    public string StorageName => "Memory";

    /// <summary>
    /// Adds a folder and its parent folders.
    /// </summary>
    /// <param name="path">The path of the folder.</param>
    /// <returns>The store.</returns>
    public InMemoryMediaFileStore AddFolder(string path)
    {
        var parts = path.Split('/');

        for (var count = 1; count <= parts.Length; count++)
        {
            _folders.Add(string.Join('/', parts[..count]));
        }

        return this;
    }

    /// <summary>
    /// Adds a file and its folders.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="content">The text of the file.</param>
    /// <returns>The store.</returns>
    public InMemoryMediaFileStore AddFile(string path, string content = "content")
        => AddFile(path, Encoding.UTF8.GetBytes(content));

    /// <summary>
    /// Adds a file and its folders.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="content">The bytes of the file.</param>
    /// <returns>The store.</returns>
    public InMemoryMediaFileStore AddFile(string path, byte[] content)
    {
        var folder = GetFolder(path);

        if (folder.Length > 0)
        {
            AddFolder(folder);
        }

        _files[path] = content;

        return this;
    }

    /// <summary>
    /// Reads the text of a stored file.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>The text, or <see langword="null"/> when there is no such file.</returns>
    public string ReadText(string path)
    {
        return _files.TryGetValue(path, out var content)
            ? Encoding.UTF8.GetString(content)
            : null;
    }

    public Task<IFileStoreEntry> GetFileInfoAsync(string path)
    {
        path = this.NormalizePath(path);

        return Task.FromResult<IFileStoreEntry>(_files.TryGetValue(path, out var content)
            ? new Entry(path, content.Length, LastModifiedUtc, false)
            : null);
    }

    public Task<IFileStoreEntry> GetDirectoryInfoAsync(string path)
    {
        path = this.NormalizePath(path) ?? string.Empty;

        return Task.FromResult<IFileStoreEntry>(path.Length == 0 || _folders.Contains(path)
            ? new Entry(path, 0, LastModifiedUtc, true)
            : null);
    }

    public async IAsyncEnumerable<IFileStoreEntry> GetDirectoryContentAsync(string path = null, bool includeSubDirectories = false)
    {
        await Task.CompletedTask;

        path = this.NormalizePath(path) ?? string.Empty;

        var folders = _folders
            .Where(folder => IsInFolder(folder, path, includeSubDirectories))
            .Order(StringComparer.Ordinal)
            .Select(folder => (IFileStoreEntry)new Entry(folder, 0, LastModifiedUtc, true));

        var files = _files
            .Where(file => IsInFolder(file.Key, path, includeSubDirectories))
            .OrderBy(file => file.Key, StringComparer.Ordinal)
            .Select(file => (IFileStoreEntry)new Entry(file.Key, file.Value.Length, LastModifiedUtc, false));

        foreach (var entry in folders.Concat(files).ToList())
        {
            yield return entry;
        }
    }

    public Task<bool> TryCreateDirectoryAsync(string path)
    {
        Changes.Add($"createFolder:{path}");

        return Task.FromResult(_folders.Add(path));
    }

    public Task<bool> TryDeleteFileAsync(string path)
    {
        Changes.Add($"delete:{path}");

        return Task.FromResult(_files.Remove(path));
    }

    public Task<bool> TryDeleteDirectoryAsync(string path)
    {
        Changes.Add($"deleteFolder:{path}");

        return Task.FromResult(_folders.Remove(path));
    }

    public Task MoveFileAsync(string oldPath, string newPath)
    {
        Changes.Add($"move:{oldPath}>{newPath}");

        if (MoveError is not null)
        {
            throw new FileStoreException(MoveError);
        }

        if (!_files.Remove(oldPath, out var content))
        {
            throw new FileStoreException($"Cannot move file '{oldPath}' because it does not exist.");
        }

        _files[newPath] = content;

        return Task.CompletedTask;
    }

    public Task CopyFileAsync(string srcPath, string dstPath)
    {
        Changes.Add($"copy:{srcPath}>{dstPath}");

        if (!_files.TryGetValue(srcPath, out var content))
        {
            throw new FileStoreException($"The file '{srcPath}' does not exist.");
        }

        _files[dstPath] = content;

        return Task.CompletedTask;
    }

    public Task<Stream> GetFileStreamAsync(string path)
    {
        if (!_files.TryGetValue(this.NormalizePath(path), out var content))
        {
            throw new FileStoreException($"Cannot get file stream because the file '{path}' does not exist.");
        }

        return Task.FromResult<Stream>(new MemoryStream(content, writable: false));
    }

    public Task<Stream> GetFileStreamAsync(IFileStoreEntry fileStoreEntry)
        => GetFileStreamAsync(fileStoreEntry.Path);

    public async Task<string> CreateFileFromStreamAsync(string path, Stream inputStream, bool overwrite = false)
    {
        var exists = _files.ContainsKey(path);

        if (exists && !overwrite)
        {
            throw new FileStoreException($"Cannot create file '{path}' because it already exists.");
        }

        using var buffer = new MemoryStream();

        await inputStream.CopyToAsync(buffer);

        Changes.Add(exists
            ? $"replace:{path}"
            : $"create:{path}");
        AddFile(path, buffer.ToArray());

        return path;
    }

    public Task<long?> GetPermittedStorageAsync()
        => Task.FromResult(PermittedStorage);

    public string MapPathToPublicUrl(string path)
        => $"/media/{path}";

    private static string GetFolder(string path)
    {
        var index = path.LastIndexOf('/');

        return index < 0
            ? string.Empty
            : path[..index];
    }

    private static bool IsInFolder(string path, string folder, bool includeSubDirectories)
    {
        var parent = GetFolder(path);

        if (string.Equals(parent, folder, StringComparison.Ordinal))
        {
            return true;
        }

        return includeSubDirectories && (folder.Length == 0 || parent.StartsWith($"{folder}/", StringComparison.Ordinal));
    }

    private sealed class Entry : IFileStoreEntry
    {
        public Entry(string path, long length, DateTime lastModifiedUtc, bool isDirectory)
        {
            Path = path;
            Name = System.IO.Path.GetFileName(path);
            DirectoryPath = GetFolder(path);
            Length = length;
            LastModifiedUtc = lastModifiedUtc;
            IsDirectory = isDirectory;
        }

        public string Path { get; }

        public string Name { get; }

        public string DirectoryPath { get; }

        public long Length { get; }

        public DateTime LastModifiedUtc { get; }

        public bool IsDirectory { get; }
    }
}
