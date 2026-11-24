using System.Collections.Concurrent;
using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

/// <summary>
/// Temporary folders under one root a test deletes.
/// </summary>
internal sealed class TestTempDirectoryProvider : ITempDirectoryProvider, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "crestapps-package-temp-" + Guid.NewGuid().ToString("n"));

    public string Root => _root;

    public string GetRootDirectory()
    {
        Directory.CreateDirectory(_root);

        return _root;
    }

    public string CreateTempSubdirectory(string prefix = null)
    {
        var path = Path.Combine(GetRootDirectory(), (prefix ?? string.Empty) + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(path);

        return path;
    }

    public string GetTempFileName(string extension = null)
        => Path.Combine(GetRootDirectory(), Guid.NewGuid().ToString("n") + (string.IsNullOrEmpty(extension) ? string.Empty : "." + extension.TrimStart('.')));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

/// <summary>
/// The protected package store in memory, which can refuse every file as an antivirus scanner would.
/// </summary>
internal sealed class InMemoryPackageFiles : IProtectedFileStore<LearningPackageFiles>
{
    private int _next;

    public ConcurrentDictionary<string, (ProtectedFile File, byte[] Content)> Stored { get; } = new(StringComparer.Ordinal);

    public bool RefuseAll { get; set; }

    public List<string> Deleted { get; } = [];

    public async Task<ProtectedFile> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        if (RefuseAll)
        {
            throw new FileStoreException($"The file '{fileName}' was refused.");
        }

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        var id = "pkg" + Interlocked.Increment(ref _next);
        var file = new ProtectedFile(id, fileName, buffer.Length);

        Stored[id] = (file, buffer.ToArray());

        return file;
    }

    public Task<ProtectedFile> FindAsync(string fileId, CancellationToken cancellationToken = default)
        => Task.FromResult(fileId is not null && Stored.TryGetValue(fileId, out var stored) ? stored.File : null);

    public Task<Stream> OpenAsync(string fileId, CancellationToken cancellationToken = default)
        => Task.FromResult<Stream>(fileId is not null && Stored.TryGetValue(fileId, out var stored) ? new MemoryStream(stored.Content, writable: false) : null);

    public Task<bool> DeleteAsync(string fileId, CancellationToken cancellationToken = default)
    {
        Deleted.Add(fileId);

        return Task.FromResult(Stored.TryRemove(fileId, out _));
    }
}
