namespace CrestApps.OrchardCore.DialPad.Models;

/// <summary>
/// Represents the DialPad provider site settings.
/// </summary>
public sealed class DialPadSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the DialPad provider is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the DialPad environment (production or sandbox) used for the REST API and OAuth
    /// endpoints.
    /// </summary>
    public DialPadEnvironment Environment { get; set; }

    /// <summary>
    /// Gets or sets an optional internal override for the DialPad REST API base address. When empty the
    /// default endpoint is used.
    /// </summary>
    public string ApiBaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the DialPad user that places outbound calls.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the caller identifier presented on outbound calls.
    /// </summary>
    public string OutboundCallerId { get; set; }

    /// <summary>
    /// Gets or sets the protected DialPad API key used when API key authentication is selected. The value is
    /// stored encrypted using the data protection provider.
    /// </summary>
    public string ApiToken { get; set; }

    /// <summary>
    /// Gets or sets the DialPad authentication type.
    /// </summary>
    public DialPadAuthenticationType AuthenticationType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether each user authenticates with DialPad through the OAuth 2.0
    /// authorization code flow instead of using a shared API key.
    /// </summary>
    public bool UseOAuth
    {
        get
        {
            return AuthenticationType == DialPadAuthenticationType.OAuth2;
        }
        set
        {
            AuthenticationType = value ? DialPadAuthenticationType.OAuth2 : DialPadAuthenticationType.ApiKey;
        }
    }

    /// <summary>
    /// Gets or sets the OAuth client identifier issued by DialPad.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the protected OAuth client secret issued by DialPad. The value is stored encrypted
    /// using the data protection provider.
    /// </summary>
    public string ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the space-separated OAuth scopes requested during authorization.
    /// </summary>
    public string Scopes { get; set; }
}
