namespace CrestApps.OrchardCore.SigningKeys;

/// <summary>
/// Keys a module kept before it moved onto the shared signing keys. Before the ring makes the first key of
/// <see cref="KeySet"/>, whoever asks first (a request or the rotation task), it imports these keys with their ids and
/// dates, so a published key set does not change when a module moves.
/// </summary>
public interface ISigningKeyImportSource
{
    /// <summary>
    /// Gets the name of the key set the keys belong to.
    /// </summary>
    string KeySet { get; }

    /// <summary>
    /// Reads the keys to import.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The keys, oldest first; empty when there are none.</returns>
    Task<IReadOnlyList<SigningKeyImport>> GetKeysAsync(CancellationToken cancellationToken = default);
}
