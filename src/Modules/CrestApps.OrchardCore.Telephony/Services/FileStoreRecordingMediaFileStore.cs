using System.Runtime.CompilerServices;
using CrestApps.Core.Telephony;
using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Binds the framework's recording backend contract to an Orchard <see cref="IFileStore"/>, so a deployment
/// keeps whichever file store it already configured -- the tenant-scoped local one, or a cloud one supplied by
/// another module -- underneath the encrypted recording store.
/// </summary>
public sealed class FileStoreRecordingMediaFileStore : IRecordingMediaFileStore
{
    private readonly IFileStore _fileStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileStoreRecordingMediaFileStore"/> class.
    /// </summary>
    /// <param name="fileStore">The file store the recordings are kept in.</param>
    public FileStoreRecordingMediaFileStore(IFileStore fileStore)
    {
        ArgumentNullException.ThrowIfNull(fileStore);

        _fileStore = fileStore;
    }

    /// <inheritdoc/>
    public Task WriteAsync(string name, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(content);

        return _fileStore.CreateFileFromStreamAsync(name, content, overwrite: true);
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenReadAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (await _fileStore.GetFileInfoAsync(name) is null)
        {
            return null;
        }

        return await _fileStore.GetFileStreamAsync(name);
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (await _fileStore.GetFileInfoAsync(name) is null)
        {
            // A recording that is not there is the state the caller asked for, so an erasure request that
            // arrives twice succeeds twice rather than failing the second time.
            return true;
        }

        return await _fileStore.TryDeleteFileAsync(name);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> ListAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var entry in _fileStore.GetDirectoryContentAsync(includeSubDirectories: false).WithCancellation(cancellationToken))
        {
            if (entry.IsDirectory)
            {
                continue;
            }

            yield return entry.Path;
        }
    }
}
