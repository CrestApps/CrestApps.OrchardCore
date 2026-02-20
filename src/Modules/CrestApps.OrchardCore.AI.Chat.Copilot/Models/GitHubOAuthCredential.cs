namespace CrestApps.OrchardCore.AI.Chat.Copilot.Models;

/// <summary>
/// Represents GitHub OAuth credentials for a user.
/// </summary>
public sealed class GitHubOAuthCredential
{
    /// <summary>
    /// The unique identifier for this credential.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// The user ID (from OrchardCore) who owns this credential.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// The GitHub username.
    /// </summary>
    public string GitHubUsername { get; set; }

    /// <summary>
    /// The encrypted access token.
    /// </summary>
    public string EncryptedAccessToken { get; set; }

    /// <summary>
    /// The encrypted refresh token (if available).
    /// </summary>
    public string EncryptedRefreshToken { get; set; }

    /// <summary>
    /// When the access token expires.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// When this credential was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// When this credential was last updated.
    /// </summary>
    public DateTime UpdatedUtc { get; set; }
}
