namespace CrestApps.OrchardCore.AI.Chat.Copilot.Settings;

/// <summary>
/// Settings for GitHub Copilot OAuth configuration.
/// </summary>
public sealed class CopilotSettings
{
    /// <summary>
    /// The GitHub App Client ID.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// The encrypted GitHub App Client Secret.
    /// </summary>
    public string ProtectedClientSecret { get; set; }

    /// <summary>
    /// OAuth scopes required for Copilot.
    /// </summary>
    public string[] Scopes { get; set; } = ["user:email", "read:org"];
}
