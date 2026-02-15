namespace CrestApps.OrchardCore.AI.Chat.Copilot.Models;

/// <summary>
/// GitHub OAuth credentials stored on the user object using OrchardCore.Entities.
/// </summary>
public sealed class GitHubOAuthCredentials
{
    /// <summary>
    /// The GitHub username.
    /// </summary>
    public string GitHubUsername { get; set; }

    /// <summary>
    /// The encrypted access token.
    /// </summary>
    public string ProtectedAccessToken { get; set; }

    /// <summary>
    /// The encrypted refresh token (if available).
    /// </summary>
    public string ProtectedRefreshToken { get; set; }

    /// <summary>
    /// When the access token expires.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// When this credential was last updated.
    /// </summary>
    public DateTime UpdatedUtc { get; set; }
}
