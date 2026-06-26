namespace CrestApps.OrchardCore.DialPad;

/// <summary>
/// Contains constant values used by the DialPad telephony provider.
/// </summary>
public static class DialPadConstants
{
    /// <summary>
    /// The technical name used to register and resolve the DialPad provider.
    /// </summary>
    public const string ProviderTechnicalName = "DialPad";

    /// <summary>
    /// The name of the data protector used to protect the DialPad API key.
    /// </summary>
    public const string ProtectorName = "DialPad";

    /// <summary>
    /// The name of the data protector used to protect the DialPad OAuth client secret.
    /// </summary>
    public const string OAuthProtectorName = "DialPad.OAuth";

    /// <summary>
    /// The DialPad OAuth 2.0 authorization endpoint.
    /// </summary>
    public const string OAuthAuthorizeUrl = "https://dialpad.com/oauth2/authorize";

    /// <summary>
    /// The DialPad OAuth 2.0 token endpoint.
    /// </summary>
    public const string OAuthTokenUrl = "https://dialpad.com/oauth2/token";

    /// <summary>
    /// The default base address of the DialPad REST API.
    /// </summary>
    public const string DefaultApiBaseUrl = "https://dialpad.com/api/v2/";

    /// <summary>
    /// Contains the feature identifiers exposed by the DialPad module.
    /// </summary>
    public static class Feature
    {
        /// <summary>
        /// The identifier of the DialPad provider feature.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.DialPad";
    }
}
