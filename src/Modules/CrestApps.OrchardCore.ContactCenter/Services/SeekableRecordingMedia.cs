namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Makes a recording seekable for playback. The recording media store decrypts forward only, but an audio player
/// seeks with HTTP byte ranges, which need a stream that can seek and knows its length.
/// </summary>
/// <remarks>
/// The store encrypts recordings at rest, so the decrypted audio is held in memory rather than written to disk. Only a
/// recording longer than <see cref="MaxInMemoryBytes"/> -- hours of call audio -- spills into a temporary file, which
/// is deleted the moment the response closes it. This works the same whichever store holds the recording (local files
/// or cloud blobs), because it only reads the store's decrypted stream. Each range request decrypts the recording
/// again; a call recording is a few megabytes, which the store's 64 KB frames decrypt in milliseconds.
/// </remarks>
internal static class SeekableRecordingMedia
{
    /// <summary>
    /// The largest recording held in memory: about four hours of the telephone-quality MP3 providers record.
    /// </summary>
    internal const int MaxInMemoryBytes = 64 * 1024 * 1024;

    private const int BufferSize = 81920;

    /// <summary>
    /// Reads a decrypted recording into a seekable stream positioned at the start.
    /// </summary>
    /// <param name="source">The decrypted recording. It is disposed once read.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A seekable stream over the recording; disposing it releases the memory or deletes the temporary file.</returns>
    public static async Task<Stream> CreateAsync(Stream source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        await using (source)
        {
            var memory = new MemoryStream();
            var buffer = new byte[BufferSize];
            int read;

            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (memory.Length + read > MaxInMemoryBytes)
                {
                    return await SpillToFileAsync(memory, buffer.AsMemory(0, read), source, cancellationToken);
                }

                await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            memory.Position = 0;

            return memory;
        }
    }

    private static async Task<Stream> SpillToFileAsync(MemoryStream head, ReadOnlyMemory<byte> pending, Stream rest, CancellationToken cancellationToken)
    {
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

            head.Position = 0;
            await head.CopyToAsync(file, BufferSize, cancellationToken);
            await head.DisposeAsync();
            await file.WriteAsync(pending, cancellationToken);
            await rest.CopyToAsync(file, BufferSize, cancellationToken);
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
