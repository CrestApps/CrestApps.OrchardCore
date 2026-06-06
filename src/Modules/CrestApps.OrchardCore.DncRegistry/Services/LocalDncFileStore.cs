using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.DncRegistry.Services;

/// <summary>
/// Wraps the tenant-local file store used for Local DNC Registry uploads.
/// </summary>
public sealed class LocalDncFileStore : ILocalDncFileStore
{
    private readonly IFileStore _fileStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalDncFileStore"/> class.
    /// </summary>
    /// <param name="fileStore">The inner file store.</param>
    public LocalDncFileStore(IFileStore fileStore)
    {
        _fileStore = fileStore;
    }

    /// <inheritdoc/>
    public Task CopyFileAsync(string srcPath, string dstPath)
        => _fileStore.CopyFileAsync(srcPath, dstPath);

    /// <inheritdoc/>
    public Task<string> CreateFileFromStreamAsync(string path, Stream inputStream, bool overwrite = false)
        => _fileStore.CreateFileFromStreamAsync(path, inputStream, overwrite);

    /// <inheritdoc/>
    public IAsyncEnumerable<IFileStoreEntry> GetDirectoryContentAsync(string path = null, bool includeSubDirectories = false)
        => _fileStore.GetDirectoryContentAsync(path, includeSubDirectories);

    /// <inheritdoc/>
    public Task<IFileStoreEntry> GetDirectoryInfoAsync(string path)
        => _fileStore.GetDirectoryInfoAsync(path);

    /// <inheritdoc/>
    public Task<IFileStoreEntry> GetFileInfoAsync(string path)
        => _fileStore.GetFileInfoAsync(path);

    /// <inheritdoc/>
    public Task<Stream> GetFileStreamAsync(string path)
        => _fileStore.GetFileStreamAsync(path);

    /// <inheritdoc/>
    public Task<Stream> GetFileStreamAsync(IFileStoreEntry fileStoreEntry)
        => _fileStore.GetFileStreamAsync(fileStoreEntry);

    /// <inheritdoc/>
    public Task MoveFileAsync(string oldPath, string newPath)
        => _fileStore.MoveFileAsync(oldPath, newPath);

    /// <inheritdoc/>
    public Task<bool> TryCreateDirectoryAsync(string path)
        => _fileStore.TryCreateDirectoryAsync(path);

    /// <inheritdoc/>
    public Task<bool> TryDeleteDirectoryAsync(string path)
        => _fileStore.TryDeleteDirectoryAsync(path);

    /// <inheritdoc/>
    public Task<bool> TryDeleteFileAsync(string path)
        => _fileStore.TryDeleteFileAsync(path);
}
