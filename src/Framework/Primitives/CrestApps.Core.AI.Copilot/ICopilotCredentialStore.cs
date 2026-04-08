namespace CrestApps.Core.AI.Copilot;

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
