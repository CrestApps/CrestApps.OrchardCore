namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Describes audio whose bytes are being written into a durable, encrypted media store: a completed conversation
/// recording, or the master copy of a voice media clip. The request carries the opaque bytes together with the
/// deterministic key that addresses the media, so a store implementation can persist and later retrieve the same
/// bytes without any provider-specific knowledge.
/// </summary>
public sealed class RecordingMediaWriteRequest
{
    /// <summary>
    /// Gets or sets the deterministic, provider-neutral key that uniquely and stably addresses this recording.
    /// The same key is used to read or delete the stored recording, so it must be derivable without any
    /// additional state.
    /// </summary>
    public string StorageKey { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the interaction the recording belongs to, used to namespace the stored
    /// media per conversation and to correlate audit records. It is <see langword="null"/> for media that belongs
    /// to no conversation, such as the master copy of a voice media clip.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the media format the bytes are encoded in (for example, <c>wav</c>).
    /// </summary>
    public string Format { get; set; }

    /// <summary>
    /// Gets or sets the readable stream of raw, unencrypted bytes to persist. The store reads the stream to
    /// completion and encrypts it at rest without buffering the whole recording in memory. The caller retains
    /// ownership of the stream and is responsible for disposing it.
    /// </summary>
    public Stream Content { get; set; }
}
