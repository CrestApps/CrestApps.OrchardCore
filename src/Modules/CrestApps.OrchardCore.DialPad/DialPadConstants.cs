using CrestApps.OrchardCore.DialPad.Models;

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
    /// The DialPad OAuth scope that allows access to a refresh token so access tokens can be renewed
    /// without prompting the user to reconnect.
    /// </summary>
    public const string OfflineAccessScope = "offline_access";

    /// <summary>
    /// The base address of the production DialPad environment.
    /// </summary>
    public const string ProductionBaseUrl = "https://dialpad.com";

    /// <summary>
    /// The base address of the sandbox DialPad environment.
    /// </summary>
    public const string SandboxBaseUrl = "https://sandbox.dialpad.com";

    /// <summary>
    /// Gets the base address for the given DialPad environment.
    /// </summary>
    /// <param name="environment">The DialPad environment.</param>
    /// <returns>The environment base address.</returns>
    public static string GetBaseUrl(DialPadEnvironment environment)
        => environment == DialPadEnvironment.Sandbox ? SandboxBaseUrl : ProductionBaseUrl;

    /// <summary>
    /// Gets the OAuth 2.0 authorization endpoint for the given DialPad environment.
    /// </summary>
    /// <param name="environment">The DialPad environment.</param>
    /// <returns>The authorization endpoint URL.</returns>
    public static string GetAuthorizeUrl(DialPadEnvironment environment)
        => $"{GetBaseUrl(environment)}/oauth2/authorize";

    /// <summary>
    /// Gets the OAuth 2.0 token endpoint for the given DialPad environment.
    /// </summary>
    /// <param name="environment">The DialPad environment.</param>
    /// <returns>The token endpoint URL.</returns>
    public static string GetTokenUrl(DialPadEnvironment environment)
        => $"{GetBaseUrl(environment)}/oauth2/token";

    /// <summary>
    /// Gets the OAuth 2.0 deauthorize endpoint for the given DialPad environment, used to revoke the
    /// tokens issued to the application on behalf of a user.
    /// </summary>
    /// <param name="environment">The DialPad environment.</param>
    /// <returns>The deauthorize endpoint URL.</returns>
    public static string GetDeauthorizeUrl(DialPadEnvironment environment)
        => $"{GetBaseUrl(environment)}/oauth2/deauthorize";

    /// <summary>
    /// Gets the default REST API base address for the given DialPad environment.
    /// </summary>
    /// <param name="environment">The DialPad environment.</param>
    /// <returns>The REST API base address.</returns>
    public static string GetApiBaseUrl(DialPadEnvironment environment)
        => $"{GetBaseUrl(environment)}/api/v2/";

    /// <summary>
    /// Contains the feature identifiers exposed by the DialPad module.
    /// </summary>
    public static class Feature
    {
        /// <summary>
        /// The identifier of the DialPad provider feature.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.DialPad";

        /// <summary>
        /// The identifier of the DialPad Contact Center voice-provider feature.
        /// </summary>
        public const string Dialer = "CrestApps.OrchardCore.DialPad.Dialer";
    }
}
