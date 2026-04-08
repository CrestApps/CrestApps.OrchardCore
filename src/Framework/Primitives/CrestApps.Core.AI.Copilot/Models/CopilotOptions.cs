namespace CrestApps.Core.AI.Copilot.Models;

/// <summary>
/// Options for GitHub Copilot authentication and provider configuration.
/// Configured via <c>IOptions&lt;CopilotOptions&gt;</c>.
/// </summary>
public sealed class CopilotOptions
{
    /// <summary>
    /// The authentication method used by the Copilot orchestrator.
    /// </summary>
    public CopilotAuthenticationType AuthenticationType { get; set; }
    /// <summary>
    /// The GitHub App Client ID.
    /// </summary>
    public string ClientId { get; set; }
    /// <summary>
    /// The GitHub App Client Secret (unprotected).
    /// </summary>
    public string ClientSecret { get; set; }
    /// <summary>
    /// OAuth scopes required for Copilot.
    /// </summary>
    public string[] Scopes { get; set; } = ["user:email", "read:org"];
    /// <summary>
    /// The provider type (e.g., "openai", "azure", "anthropic").
    /// </summary>
    public string ProviderType { get; set; }
    /// <summary>
    /// The API endpoint URL for the provider.
    /// </summary>
    public string BaseUrl { get; set; }
    /// <summary>
    /// The API key for the provider (unprotected).
    /// </summary>
    public string ApiKey { get; set; }
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
