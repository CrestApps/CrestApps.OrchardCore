using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.SigningKeys;

/// <summary>
/// The signing keys of the tenant's key sets: the key that signs now, the public keys to publish, and rotation. Each key
/// pair is a secret of Orchard Core Secrets; the ring itself keeps only which keys a set has, their public halves and
/// when they were made and retired.
/// </summary>
public interface ISigningKeyRing
{
    /// <summary>
    /// Returns the key that signs now, with its private half. One is made when the set has none, or when the secret of
    /// its key is gone.
    /// </summary>
    /// <param name="keySet">The name of a registered key set.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The key.</returns>
    Task<SigningKey> GetSigningKeyAsync(string keySet, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the public keys to publish: the one that signs now and those retired within the set's retired lifetime,
    /// oldest first. A key is made when the set has none.
    /// </summary>
    /// <param name="keySet">The name of a registered key set.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The public JSON Web Keys.</returns>
    Task<IReadOnlyList<JsonObject>> GetPublishedKeysAsync(string keySet, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes a new key that signs from now on; the old one stays published for the set's retired lifetime.
    /// </summary>
    /// <param name="keySet">The name of a registered key set.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The new key.</returns>
    Task<SigningKey> RotateAsync(string keySet, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates the key when it reached the set's rotation age, or makes one when the set has none.
    /// </summary>
    /// <param name="keySet">The name of a registered key set.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when a key was made.</returns>
    Task<bool> RotateIfDueAsync(string keySet, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports keys a module kept before it used the ring, with their ids, public halves and dates. A key whose id the
    /// set already has is skipped, so importing twice changes nothing.
    /// </summary>
    /// <param name="keySet">The name of a registered key set.</param>
    /// <param name="keys">The keys.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The number of keys imported.</returns>
    Task<int> ImportAsync(string keySet, IEnumerable<SigningKeyImport> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether the set has a key yet, without making one.
    /// </summary>
    /// <param name="keySet">The name of a registered key set.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when the set has a key.</returns>
    Task<bool> HasKeysAsync(string keySet, CancellationToken cancellationToken = default);
}
