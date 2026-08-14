namespace CrestApps.OrchardCore.Dialpad.Models;

/// <summary>
/// Represents the credentials and options for a single Dialpad environment (production or sandbox).
/// Each environment is configured independently so a tenant can hold production and sandbox credentials
/// side by side and switch the active environment without re-entering them.
/// </summary>
public sealed class DialpadEnvironmentSettings
{
    /// <summary>
    /// Gets or sets the Dialpad authentication type used for this environment.
    /// </summary>
    public DialpadAuthenticationType AuthenticationType { get; set; }

    /// <summary>
    /// Gets or sets the protected Dialpad API key used when API key authentication is selected. The value
    /// is stored encrypted using the data protection provider.
    /// </summary>
    public string ApiToken { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the Dialpad user that places outbound calls.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the caller identifier presented on outbound calls.
    /// </summary>
    public string OutboundCallerId { get; set; }

    /// <summary>
    /// Gets or sets the OAuth client identifier issued by Dialpad.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the protected OAuth client secret issued by Dialpad. The value is stored encrypted
    /// using the data protection provider.
    /// </summary>
    public string ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the space-separated OAuth scopes requested during authorization.
    /// </summary>
    public string Scopes { get; set; }

    /// <summary>
    /// Gets or sets the protected secret Dialpad uses to sign call-event webhooks (JWT HS256). The value
    /// is stored encrypted using the data protection provider. Inbound webhooks are rejected when empty.
    /// </summary>
    public string WebhookSigningSecret { get; set; }

    /// <summary>
    /// Gets or sets an optional internal override for the Dialpad REST API base address. When empty the
    /// default endpoint for the environment is used.
    /// </summary>
    public string ApiBaseUrl { get; set; }

    /// <summary>
    /// Resolves the effective authentication type from an explicit selection, falling back to inferring it
    /// from the presence of an API key or OAuth client credentials for backward compatibility.
    /// </summary>
    /// <param name="authenticationType">The explicitly selected authentication type.</param>
    /// <param name="apiToken">The stored API key, when API key authentication is used.</param>
    /// <param name="clientId">The stored OAuth client id, when OAuth authentication is used.</param>
    /// <param name="clientSecret">The stored OAuth client secret, when OAuth authentication is used.</param>
    /// <returns>The effective authentication type.</returns>
    public static DialpadAuthenticationType ResolveEffectiveAuthenticationType(
        DialpadAuthenticationType authenticationType,
        string apiToken,
        string clientId,
        string clientSecret)
    {
        if (authenticationType != DialpadAuthenticationType.NotConfigured)
        {
            return authenticationType;
        }

        if (!string.IsNullOrEmpty(apiToken))
        {
            return DialpadAuthenticationType.ApiKey;
        }

        if (!string.IsNullOrWhiteSpace(clientId) || !string.IsNullOrEmpty(clientSecret))
        {
            return DialpadAuthenticationType.OAuth2;
        }

        return DialpadAuthenticationType.NotConfigured;
    }

    /// <summary>
    /// Gets the effective authentication type for this environment, inferring it from the stored
    /// credentials when no explicit selection has been made.
    /// </summary>
    /// <returns>The effective authentication type.</returns>
    public DialpadAuthenticationType GetEffectiveAuthenticationType()
        => ResolveEffectiveAuthenticationType(AuthenticationType, ApiToken, ClientId, ClientSecret);
}
