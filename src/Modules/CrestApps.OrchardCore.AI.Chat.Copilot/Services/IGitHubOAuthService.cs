using CrestApps.OrchardCore.AI.Chat.Copilot.Models;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.Services;

/// <summary>
/// Service for managing GitHub OAuth credentials for Copilot.
/// </summary>
public interface IGitHubOAuthService
{
    /// <summary>
    /// Gets the authorization URL to initiate GitHub OAuth flow.
    /// </summary>
    /// <param name="returnUrl">The URL to return to after authentication.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The GitHub authorization URL.</returns>
    Task<string> GetAuthorizationUrlAsync(string returnUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges an authorization code for access tokens.
    /// </summary>
    /// <param name="code">The authorization code from GitHub.</param>
    /// <param name="userId">The OrchardCore user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored credential.</returns>
    Task<GitHubOAuthCredential> ExchangeCodeForTokenAsync(
        string code,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the OAuth credential for a user.
    /// </summary>
    /// <param name="userId">The OrchardCore user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The credential if found, otherwise null.</returns>
    Task<GitHubOAuthCredential> GetCredentialAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a valid access token for a user, refreshing if necessary.
    /// </summary>
    /// <param name="userId">The OrchardCore user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The decrypted access token, or null if not authenticated.</returns>
    Task<string> GetValidAccessTokenAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes and deletes the OAuth credential for a user.
    /// </summary>
    /// <param name="userId">The OrchardCore user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DisconnectAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has a valid OAuth credential.
    /// </summary>
    /// <param name="userId">The OrchardCore user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the user has a valid credential.</returns>
    Task<bool> IsAuthenticatedAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
