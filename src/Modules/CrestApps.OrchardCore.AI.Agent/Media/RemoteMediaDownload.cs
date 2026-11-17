namespace CrestApps.OrchardCore.AI.Agent.Media;

/// <summary>
/// What <see cref="RemoteMediaDownloader"/> fetched from a public address, or the reason it fetched nothing.
/// </summary>
internal sealed class RemoteMediaDownload : IAsyncDisposable
{
    private RemoteMediaDownload()
    {
    }

    /// <summary>
    /// Gets the reason nothing was fetched, as a sentence for the result of a tool. It is <see langword="null"/> when
    /// the file was fetched.
    /// </summary>
    public string Error { get; private init; }

    /// <summary>
    /// Gets the fetched file, at its start. It is a temporary file that is deleted when the download is disposed.
    /// </summary>
    public Stream Content { get; private init; }

    /// <summary>
    /// Gets the size of the fetched file in bytes.
    /// </summary>
    public long Length { get; private init; }

    /// <summary>
    /// Gets the media type the address answered with, when it gave one.
    /// </summary>
    public string ContentType { get; private init; }

    /// <summary>
    /// Gets the address the file came from, after redirects.
    /// </summary>
    public Uri FinalUri { get; private init; }

    /// <summary>
    /// Creates the result of a fetch that failed or was refused.
    /// </summary>
    /// <param name="error">The reason, as a sentence for the result of a tool.</param>
    /// <returns>The result.</returns>
    public static RemoteMediaDownload Failed(string error)
    {
        ArgumentException.ThrowIfNullOrEmpty(error);

        return new RemoteMediaDownload
        {
            Error = error,
        };
    }

    /// <summary>
    /// Creates the result of a fetch that worked.
    /// </summary>
    /// <param name="content">The fetched file, at its start.</param>
    /// <param name="length">The size of the file in bytes.</param>
    /// <param name="contentType">The media type the address answered with.</param>
    /// <param name="finalUri">The address the file came from, after redirects.</param>
    /// <returns>The result.</returns>
    public static RemoteMediaDownload Fetched(Stream content, long length, string contentType, Uri finalUri)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(finalUri);

        return new RemoteMediaDownload
        {
            Content = content,
            Length = length,
            ContentType = contentType,
            FinalUri = finalUri,
        };
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Content is not null)
        {
            await Content.DisposeAsync();
        }
    }
}
