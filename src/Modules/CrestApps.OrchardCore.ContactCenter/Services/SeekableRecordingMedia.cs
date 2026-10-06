namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Makes a recording seekable for playback. The recording media store decrypts forward only, but an audio player
/// seeks with HTTP byte ranges, which need a stream that can seek and knows its length.
/// </summary>
/// <remarks>
/// The decrypted audio is copied into a temporary file that is deleted when the returned stream is closed, so it
/// lives only as long as the response that streams it. This works the same whichever store holds the recording (local
/// files or cloud blobs), because it only reads the store's decrypted stream. Each range request decrypts the
/// recording again; a call recording is a few megabytes, which the store's 64 KB frames decrypt in milliseconds.
/// </remarks>
internal static class SeekableRecordingMedia
{
    private const int BufferSize = 81920;

    /// <summary>
    /// Copies a decrypted recording into a temporary file and returns it positioned at the start.
    /// </summary>
    /// <param name="source">The decrypted recording. It is disposed once copied.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A seekable stream over the recording; disposing it deletes the temporary file.</returns>
    public static async Task<Stream> CreateAsync(Stream source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        var path = Path.GetTempFileName();
        FileStream file = null;

        try
        {
            file = new FileStream(
                path,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.DeleteOnClose);

            await using (source)
            {
                await source.CopyToAsync(file, BufferSize, cancellationToken);
            }

            await file.FlushAsync(cancellationToken);
            file.Position = 0;

            return file;
        }
        catch
        {
            if (file is not null)
            {
                await file.DisposeAsync();
            }
            else
            {
                File.Delete(path);
            }

            throw;
        }
    }
}
