using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Persists completed conversation recordings into a durable media store, encrypting them at rest. The store
/// is provider-neutral and pluggable: any voice provider can ingest recordings through the same contract, and
/// concrete backends (local encrypted files, cloud blob storage, and so on) can be swapped without changing
/// callers. Recording bytes are never stored inside the Contact Center orchestration data; the orchestration
/// layer keeps only the opaque storage key that this store maps back to the encrypted media.
/// </summary>
public interface IRecordingMediaStore
{
    /// <summary>
    /// Encrypts and stores the supplied recording bytes at rest, keyed by the request's deterministic storage
    /// key. The operation is idempotent for a given storage key: re-storing the same recording overwrites the
    /// previously persisted bytes rather than creating a duplicate.
    /// </summary>
    /// <param name="request">The recording bytes together with the deterministic key and correlation metadata.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The opaque storage reference that addresses the stored recording for later reads or deletion.</returns>
    Task<string> StoreAsync(RecordingMediaWriteRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a readable, decrypted stream over a previously stored recording.
    /// </summary>
    /// <param name="storageReference">The storage reference returned when the recording was stored.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A readable stream of the decrypted recording bytes, or <see langword="null"/> when no recording is stored
    /// for the supplied reference.
    /// </returns>
    Task<Stream> OpenReadAsync(string storageReference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a stored recording. The operation is idempotent: deleting a recording that is already absent is
    /// treated as a successful no-op so a right-to-erasure request can be safely retried.
    /// </summary>
    /// <param name="storageReference">The storage reference returned when the recording was stored.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// <see langword="true"/> when the store confirms that no media remains for the supplied reference; otherwise,
    /// <see langword="false"/> when deletion could not be confirmed.
    /// </returns>
    Task<bool> DeleteAsync(string storageReference, CancellationToken cancellationToken = default);
}
