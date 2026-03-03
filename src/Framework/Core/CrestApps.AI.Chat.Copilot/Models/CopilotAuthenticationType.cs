namespace CrestApps.AI.Chat.Copilot.Models;

/// <summary>
/// Specifies the authentication method used by the Copilot orchestrator.
/// </summary>
public enum CopilotAuthenticationType
{
    /// <summary>
    /// Users authenticate via GitHub OAuth.
    /// Requires a GitHub Copilot subscription.
    /// </summary>
    GitHubOAuth,

    /// <summary>
    /// Bring Your Own Key — uses a provider API key configured by the tenant owner.
    /// No GitHub Copilot subscription required.
    /// </summary>
    ApiKey,
}
