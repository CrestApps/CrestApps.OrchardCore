namespace CrestApps.OrchardCore.Asterisk.Models;

/// <summary>
/// Represents an Asterisk ARI stored recording downloaded through the stored-file endpoint as a still-open,
/// readable stream together with the reported media content type. The recording body is streamed rather than
/// buffered, so the resource that owns the underlying network stream is held open until this content is
/// disposed; disposal releases that owning resource (and therefore the stream).
/// </summary>
internal sealed class AsteriskAriStoredRecordingContent : IAsyncDisposable, IDisposable
{
    private readonly IDisposable _owner;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskAriStoredRecordingContent"/> class.
    /// </summary>
    /// <param name="content">The readable recording stream to persist.</param>
    /// <param name="contentType">The media content type Asterisk reported for the recording, when present.</param>
    /// <param name="owner">The resource that owns the underlying stream and is disposed with this content.</param>
    public AsteriskAriStoredRecordingContent(
        Stream content,
        string contentType,
        IDisposable owner)
    {
        Content = content;
        ContentType = contentType;
        _owner = owner;
    }

    /// <summary>
    /// Gets the readable stream of raw, unencrypted recording bytes as returned by Asterisk.
    /// </summary>
    public Stream Content { get; }

    /// <summary>
    /// Gets the media content type Asterisk reported for the downloaded recording, when present.
    /// </summary>
    public string ContentType { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        _owner.Dispose();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_owner is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();

            return;
        }

        _owner.Dispose();
    }
}
