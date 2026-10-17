using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Persists the audio the platform owns into a durable media store, encrypting it at rest: completed conversation
/// recordings, and the master copy of every voice media clip (hold music, IVR prompts, voicemail greetings) that
/// telephony providers are given a copy of. The store is provider-neutral and pluggable: any voice provider can
/// ingest recordings through the same contract, and concrete backends (local encrypted files, cloud blob storage,
/// and so on) can be swapped without changing callers. Media bytes are never stored inside the Contact Center
/// orchestration data; the orchestration layer keeps only the opaque storage key that this store maps back to the
/// encrypted media.
/// </summary>
public interface IRecordingMediaStore
{
    /// <summary>
    /// Encrypts and stores the supplied audio bytes at rest, keyed by the request's deterministic storage key. The
    /// operation is idempotent for a given storage key: re-storing the same media overwrites the previously
    /// persisted bytes rather than creating a duplicate.
    /// </summary>
    /// <param name="request">The audio bytes together with the deterministic key and correlation metadata.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The opaque storage reference that addresses the stored media for later reads or deletion.</returns>
    Task<string> StoreAsync(RecordingMediaWriteRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a readable, decrypted stream over previously stored media.
    /// </summary>
    /// <remarks>
    /// Return a stream that can seek and reports its <see cref="Stream.Length"/> whenever the stored media allows it.
    /// Playback serves the stream with HTTP range requests, so a stream that cannot seek still plays, but the player
    /// cannot seek in it.
    /// </remarks>
    /// <param name="storageReference">The storage reference returned when the media was stored.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A readable stream of the decrypted bytes, or <see langword="null"/> when no media is stored for the
    /// supplied reference.
    /// </returns>
    Task<Stream> OpenReadAsync(string storageReference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes stored media. The operation is idempotent: deleting media that is already absent is treated as a
    /// successful no-op so a right-to-erasure request can be safely retried.
    /// </summary>
    /// <param name="storageReference">The storage reference returned when the media was stored.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// <see langword="true"/> when the store confirms that no media remains for the supplied reference; otherwise,
    /// <see langword="false"/> when deletion could not be confirmed.
    /// </returns>
    Task<bool> DeleteAsync(string storageReference, CancellationToken cancellationToken = default);
}
