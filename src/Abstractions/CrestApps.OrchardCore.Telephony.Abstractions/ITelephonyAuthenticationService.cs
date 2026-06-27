using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Coordinates the OAuth 2.0 authentication of the current user with the configured telephony provider.
/// </summary>
public interface ITelephonyAuthenticationService
{
    /// <summary>
    /// Gets the connection status of the current user with the configured default provider.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The connection status.</returns>
    Task<TelephonyConnectionStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the authorization request the current user is redirected with in order to connect to the provider.
    /// </summary>
    /// <param name="redirectUri">The absolute callback URL the provider redirects to.</param>
    /// <param name="state">The opaque state value used to protect the flow against cross-site request forgery.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authorization request, or <see langword="null"/> when the provider does not require authentication.</returns>
    Task<TelephonyAuthorizationRequest> GetAuthorizationUrlAsync(string redirectUri, string state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes the authorization by exchanging the authorization code for tokens and storing them on the user's account.
    /// </summary>
    /// <param name="code">The authorization code returned by the provider.</param>
    /// <param name="redirectUri">The callback URL used when the authorization request was created.</param>
    /// <param name="codeVerifier">The PKCE code verifier generated when the authorization request was created, or <see langword="null"/> when PKCE is not used.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the user was connected successfully; otherwise <see langword="false"/>.</returns>
    Task<bool> CompleteAuthorizationAsync(string code, string redirectUri, string codeVerifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects the current user from the configured provider by removing the stored tokens.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a valid set of tokens for the current user and the given provider, refreshing them when they are expired.
    /// </summary>
    /// <param name="providerName">The technical name of the provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The valid tokens, or <see langword="null"/> when the user is not connected or the tokens cannot be refreshed.</returns>
    Task<TelephonyUserTokens> GetValidTokensAsync(string providerName, CancellationToken cancellationToken = default);
}
