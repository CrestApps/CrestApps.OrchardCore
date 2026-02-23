using CrestApps.OrchardCore.AI.Chat.Copilot.Models;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.Settings;

/// <summary>
/// Settings for GitHub Copilot authentication and provider configuration.
/// </summary>
public sealed class CopilotSettings
{
    /// <summary>
    /// The authentication method used by the Copilot orchestrator.
    /// </summary>
    public CopilotAuthenticationType AuthenticationType { get; set; }

    // ── GitHub OAuth fields ──

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

    // ── BYOK (API Key) fields ──

    /// <summary>
    /// The provider type (e.g., "openai", "azure", "anthropic").
    /// </summary>
    public string ProviderType { get; set; }

    /// <summary>
    /// The API endpoint URL for the provider.
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// The encrypted API key for the provider.
    /// </summary>
    public string ProtectedApiKey { get; set; }

    /// <summary>
    /// The wire API format: "completions" (default) or "responses".
    /// </summary>
    public string WireApi { get; set; } = "completions";

    /// <summary>
    /// The default model name for BYOK sessions (e.g., "gpt-4o").
    /// </summary>
    public string DefaultModel { get; set; }

    /// <summary>
    /// The Azure API version (only used when ProviderType is "azure").
    /// </summary>
    public string AzureApiVersion { get; set; }
}
