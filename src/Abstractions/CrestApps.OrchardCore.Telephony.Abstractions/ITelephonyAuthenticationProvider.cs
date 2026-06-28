using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Implemented by a telephony provider that authenticates each user through the OAuth 2.0
/// authorization code flow. Providers that use a single, shared API key do not implement this interface.
/// </summary>
public interface ITelephonyAuthenticationProvider
{
    /// <summary>
    /// Gets a value indicating whether the provider currently requires per-user OAuth authentication.
    /// </summary>
    bool RequiresUserAuthentication { get; }

    /// <summary>
    /// Gets the authentication scheme identifier the provider uses, for example
    /// <see cref="TelephonyConstants.AuthenticationSchemes.OAuth2"/>. The soft phone uses this to select the
    /// matching authentication experience.
    /// </summary>
    string AuthenticationScheme { get; }

    /// <summary>
    /// Gets a value indicating whether the provider supports PKCE (Proof Key for Code Exchange) for
    /// the authorization code flow. When <see langword="true"/>, the authentication service generates
    /// a code verifier and challenge for each authorization request.
    /// </summary>
    bool SupportsProofKeyForCodeExchange { get; }

    /// <summary>
    /// Builds the provider's OAuth authorization URL the user is redirected to in order to grant access.
    /// </summary>
    /// <param name="context">The authorization context containing the callback URL and state.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The absolute authorization URL.</returns>
    Task<string> GetAuthorizationUrlAsync(TelephonyAuthorizationContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges an authorization code for access and refresh tokens.
    /// </summary>
    /// <param name="context">The exchange context containing the authorization code and callback URL.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The issued tokens, or <see langword="null"/> when the exchange fails.</returns>
    Task<TelephonyUserTokens> ExchangeCodeAsync(TelephonyCodeExchangeContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtains a new set of tokens using a refresh token.
    /// </summary>
    /// <param name="tokens">The current tokens that contain the refresh token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The refreshed tokens, or <see langword="null"/> when the refresh fails.</returns>
    Task<TelephonyUserTokens> RefreshTokensAsync(TelephonyUserTokens tokens, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes the given tokens at the provider, for example when the user disconnects their account.
    /// Implementations should not throw when revocation fails.
    /// </summary>
    /// <param name="tokens">The tokens to revoke.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RevokeTokensAsync(TelephonyUserTokens tokens, CancellationToken cancellationToken = default);
}
