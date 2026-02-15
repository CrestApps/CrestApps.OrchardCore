namespace CrestApps.OrchardCore.AI.Chat.Copilot.Settings;

/// <summary>
/// Settings for GitHub Copilot OAuth configuration.
/// </summary>
public sealed class CopilotSettings
{
    /// <summary>
    /// The GitHub OAuth App Client ID.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// The encrypted GitHub OAuth App Client Secret.
    /// </summary>
    public string ProtectedClientSecret { get; set; }

    /// <summary>
    /// The OAuth callback URL.
    /// </summary>
    public string CallbackUrl { get; set; }

    /// <summary>
    /// OAuth scopes required for Copilot.
    /// </summary>
    public string[] Scopes { get; set; } = ["user:email", "read:org"];
}
