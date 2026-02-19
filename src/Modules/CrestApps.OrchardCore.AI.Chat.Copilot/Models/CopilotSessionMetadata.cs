namespace CrestApps.OrchardCore.AI.Chat.Copilot.Models;

/// <summary>
/// Metadata specific to Copilot orchestrator sessions.
/// Stored on <see cref="AI.Models.ChatInteraction"/> or <see cref="AI.Models.AIProfile"/>
/// via <c>.Put()</c> and read by the orchestration pipeline.
/// </summary>
internal sealed class CopilotSessionMetadata
{
    /// <summary>
    /// The Copilot model to use (e.g., gpt-4o, claude-3.5-sonnet).
    /// </summary>
    public string CopilotModel { get; set; }

    /// <summary>
    /// Additional Copilot execution flags (e.g., --allow-all).
    /// </summary>
    public string CopilotFlags { get; set; }

    /// <summary>
    /// The GitHub username associated with the stored credential.
    /// Only populated on AIProfile entities (not ChatInteraction).
    /// </summary>
    public string GitHubUsername { get; set; }

    /// <summary>
    /// The encrypted access token for GitHub Copilot.
    /// Stored on AIProfile entities so sessions can reuse the credential
    /// without requiring the chatting user to be individually authenticated.
    /// </summary>
    public string ProtectedAccessToken { get; set; }

    /// <summary>
    /// The encrypted refresh token for GitHub Copilot.
    /// </summary>
    public string ProtectedRefreshToken { get; set; }

    /// <summary>
    /// When the access token expires.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
}
