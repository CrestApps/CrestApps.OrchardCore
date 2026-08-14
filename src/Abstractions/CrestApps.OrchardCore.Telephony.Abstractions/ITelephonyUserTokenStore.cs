using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Securely stores and retrieves the current user's telephony provider tokens. Implementations are
/// responsible for encrypting the tokens at rest and persisting them on the user's account.
/// </summary>
public interface ITelephonyUserTokenStore
{
    /// <summary>
    /// Gets the current user's tokens for the given provider.
    /// </summary>
    /// <param name="providerName">The technical name of the provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The stored tokens, or <see langword="null"/> when the user has no tokens for the provider.</returns>
    Task<TelephonyUserTokens> GetAsync(string providerName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the given tokens for the current user and the given provider.
    /// </summary>
    /// <param name="providerName">The technical name of the provider.</param>
    /// <param name="tokens">The tokens to store.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task StoreAsync(string providerName, TelephonyUserTokens tokens, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the current user's tokens for the given provider.
    /// </summary>
    /// <param name="providerName">The technical name of the provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RemoveAsync(string providerName, CancellationToken cancellationToken = default);
}
