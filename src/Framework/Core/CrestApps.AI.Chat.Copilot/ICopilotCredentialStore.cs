namespace CrestApps.AI.Chat.Copilot;

/// <summary>
/// Abstracts storage and retrieval of GitHub OAuth credentials per user.
/// Implement with your preferred user store (OrchardCore users, EF Identity, etc.).
/// </summary>
public interface ICopilotCredentialStore
{
    /// <summary>
    /// Gets the protected credential for the specified user.
    /// </summary>
    Task<CopilotProtectedCredential> GetProtectedCredentialAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a protected credential for the specified user.
    /// </summary>
    Task SaveProtectedCredentialAsync(string userId, CopilotProtectedCredential credential, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the credential for the specified user.
    /// </summary>
    Task ClearCredentialAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Protected (encrypted) credential stored per user.
/// </summary>
public sealed class CopilotProtectedCredential
{
    public string GitHubUsername { get; set; }
    public string ProtectedAccessToken { get; set; }
    public string ProtectedRefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? UpdatedUtc { get; set; }
}
