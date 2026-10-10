using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.ContentTransfer;

public static class ContentTransferFileStoreExtensions
{
    /// <summary>
    /// Opens a stored file as a stream that can seek. The .xlsx reader needs to seek, and a file store that is not
    /// on the local disk, such as Azure Blob Storage, returns a forward-only network stream. Such a stream is copied
    /// to a temporary local file that is deleted when the returned stream is disposed.
    /// </summary>
    public static async Task<Stream> OpenSeekableReadStreamAsync(
        this IContentTransferFileStore fileStore,
        IFileStoreEntry fileStoreEntry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStore);
        ArgumentNullException.ThrowIfNull(fileStoreEntry);

        var stream = await fileStore.GetFileStreamAsync(fileStoreEntry);

        if (stream.CanSeek)
        {
            return stream;
        }

        await using (stream)
        {
            var tempStream = new FileStream(
                Path.GetTempFileName(),
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.DeleteOnClose | FileOptions.Asynchronous);

            try
            {
                await stream.CopyToAsync(tempStream, cancellationToken);
                tempStream.Position = 0;

                return tempStream;
            }
            catch
            {
                await tempStream.DisposeAsync();

                throw;
            }
        }
    }
}
