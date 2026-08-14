namespace CrestApps.OrchardCore.Dialpad.Models;

/// <summary>
/// Represents the Dialpad provider site settings.
/// </summary>
public sealed class DialpadSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the Dialpad provider is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the Dialpad environment (production or sandbox) used for the REST API and OAuth
    /// endpoints.
    /// </summary>
    public DialpadEnvironment Environment { get; set; }

    /// <summary>
    /// Gets or sets an optional internal override for the Dialpad REST API base address. When empty the
    /// default endpoint is used.
    /// </summary>
    public string ApiBaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the Dialpad user that places outbound calls.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the caller identifier presented on outbound calls.
    /// </summary>
    public string OutboundCallerId { get; set; }

    /// <summary>
    /// Gets or sets the protected Dialpad API key used when API key authentication is selected. The value is
    /// stored encrypted using the data protection provider.
    /// </summary>
    public string ApiToken { get; set; }

    /// <summary>
    /// Gets or sets the Dialpad authentication type.
    /// </summary>
    public DialpadAuthenticationType AuthenticationType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether each user authenticates with Dialpad through the OAuth 2.0
    /// authorization code flow instead of using a shared API key.
    /// </summary>
    public bool UseOAuth
    {
        get
        {
            return AuthenticationType == DialpadAuthenticationType.OAuth2;
        }
        set
        {
            AuthenticationType = value ? DialpadAuthenticationType.OAuth2 : DialpadAuthenticationType.ApiKey;
        }
    }

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
}
