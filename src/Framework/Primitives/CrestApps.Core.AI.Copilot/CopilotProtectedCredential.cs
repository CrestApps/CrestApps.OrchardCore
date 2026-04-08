namespace CrestApps.Core.AI.Copilot;

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
