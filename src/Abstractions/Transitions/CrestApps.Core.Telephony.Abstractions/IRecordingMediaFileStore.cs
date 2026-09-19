namespace CrestApps.Core.Telephony;

/// <summary>
/// Stores and retrieves the opaque bytes of a recording under a flat name, so the encrypted recording store
/// can keep one container format and one naming rule while the bytes live wherever a deployment puts them:
/// a directory on disk, a blob container, or anything else a host can implement these four operations over.
/// </summary>
/// <remarks>
/// Implementations see only ciphertext. Encryption, the container format, and the derivation of the name from
/// a storage key all belong to the recording store above this contract, which is what lets a deployment change
/// backends without changing how a recording is encrypted or addressed.
/// </remarks>
public interface IRecordingMediaFileStore
{
    /// <summary>
    /// Writes the bytes under <paramref name="name"/>, replacing anything already stored under it.
    /// </summary>
    /// <param name="name">The flat name to store the bytes under.</param>
    /// <param name="content">The readable stream of bytes to store.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task WriteAsync(string name, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the bytes stored under <paramref name="name"/> for reading.
    /// </summary>
    /// <param name="name">The flat name the bytes were stored under.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A readable stream, or <see langword="null"/> when nothing is stored under that name.</returns>
    Task<Stream> OpenReadAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes whatever is stored under <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The flat name to remove.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when nothing is stored under that name once the call returns, including when
    /// nothing was stored under it to begin with; <see langword="false"/> when the removal failed.
    /// </returns>
    Task<bool> DeleteAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enumerates the names of everything currently stored.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    IAsyncEnumerable<string> ListAsync(CancellationToken cancellationToken = default);
}
