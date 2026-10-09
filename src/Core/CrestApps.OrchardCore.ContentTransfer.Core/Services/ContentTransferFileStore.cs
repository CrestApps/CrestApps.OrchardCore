using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.ContentTransfer.Services;

public sealed class ContentTransferFileStore : IContentTransferFileStore
{
    private readonly IFileStore _fileStore;
    private readonly IFileStore _legacyFileStore;

    public ContentTransferFileStore(IFileStore fileStore)
        : this(fileStore, legacyFileStore: null)
    {
    }

    /// <summary>
    /// Initializes a store that also reads and deletes files from where they were saved before the storage location
    /// changed, so imports and exports that started before the change can still finish. New files are always written
    /// to <paramref name="fileStore"/>.
    /// </summary>
    public ContentTransferFileStore(IFileStore fileStore, IFileStore legacyFileStore)
    {
        ArgumentNullException.ThrowIfNull(fileStore);

        _fileStore = fileStore;
        _legacyFileStore = legacyFileStore;
    }

    public IFileStoreCapabilities Capabilities
        => _fileStore.Capabilities;

    public Task CopyFileAsync(string srcPath, string dstPath)
        => _fileStore.CopyFileAsync(srcPath, dstPath);

    public Task<string> CreateFileFromStreamAsync(string path, Stream inputStream, bool overwrite = false)
        => _fileStore.CreateFileFromStreamAsync(path, inputStream, overwrite);

    public IAsyncEnumerable<IFileStoreEntry> GetDirectoryContentAsync(string path = null, bool includeSubDirectories = false)
        => _fileStore.GetDirectoryContentAsync(path, includeSubDirectories);

    public Task<IFileStoreEntry> GetDirectoryInfoAsync(string path)
        => _fileStore.GetDirectoryInfoAsync(path);

    public async Task<IFileStoreEntry> GetFileInfoAsync(string path)
    {
        var fileInfo = await _fileStore.GetFileInfoAsync(path);

        if (fileInfo != null || _legacyFileStore == null)
        {
            return fileInfo;
        }

        return await _legacyFileStore.GetFileInfoAsync(path);
    }

    public async Task<Stream> GetFileStreamAsync(string path)
        => await (await GetStoreHoldingFileAsync(path)).GetFileStreamAsync(path);

    public async Task<Stream> GetFileStreamAsync(IFileStoreEntry fileStoreEntry)
        => await (await GetStoreHoldingFileAsync(fileStoreEntry.Path)).GetFileStreamAsync(fileStoreEntry);

    public Task MoveFileAsync(string oldPath, string newPath)
        => _fileStore.MoveFileAsync(oldPath, newPath);

    public Task<bool> TryCreateDirectoryAsync(string path)
        => _fileStore.TryCreateDirectoryAsync(path);

    public Task<bool> TryDeleteDirectoryAsync(string path)
        => _fileStore.TryDeleteDirectoryAsync(path);

    public async Task<bool> TryDeleteFileAsync(string path)
    {
        var deleted = await _fileStore.TryDeleteFileAsync(path);

        if (_legacyFileStore != null)
        {
            deleted |= await _legacyFileStore.TryDeleteFileAsync(path);
        }

        return deleted;
    }

    private async Task<IFileStore> GetStoreHoldingFileAsync(string path)
    {
        if (_legacyFileStore == null || await _fileStore.GetFileInfoAsync(path) != null)
        {
            return _fileStore;
        }

        return await _legacyFileStore.GetFileInfoAsync(path) != null
            ? _legacyFileStore
            : _fileStore;
    }
}
