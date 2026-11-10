using CrestApps.OrchardCore.AgenticCommerce.Core.Security;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// The store's ES256 signing keys, which sign UCP order updates and AP2 checkouts and are published in the store's UCP
/// profile.
/// </summary>
public interface IMerchantSigningKeyService
{
    /// <summary>
    /// Returns the key that signs now, with its private half. One is made when the store has none.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EcJsonWebKey> GetSigningKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the public keys to publish: the one that signs now, and those that stopped signing in the last week.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<EcJsonWebKey>> GetPublishedKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes a new key that signs from now on; the old one stays published for a week.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task RotateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates the key when it is 90 days old, and stops publishing keys retired more than a week ago.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<bool> RotateIfDueAsync(CancellationToken cancellationToken = default);
}
