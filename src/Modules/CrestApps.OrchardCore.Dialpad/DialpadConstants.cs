using CrestApps.OrchardCore.Dialpad.Models;

namespace CrestApps.OrchardCore.Dialpad;

/// <summary>
/// Contains constant values used by the Dialpad telephony provider.
/// </summary>
public static class DialpadConstants
{
    /// <summary>
    /// The technical name used to register and resolve the Dialpad provider.
    /// </summary>
    public const string ProviderTechnicalName = "Dialpad";

    /// <summary>
    /// The name of the data protector used to protect the Dialpad API key.
    /// </summary>
    public const string ProtectorName = "Dialpad";

    /// <summary>
    /// The name of the data protector used to protect the Dialpad OAuth client secret.
    /// </summary>
    public const string OAuthProtectorName = "Dialpad.OAuth";

    /// <summary>
    /// The name of the data protector used to protect the Dialpad webhook signing secret.
    /// </summary>
    public const string WebhookProtectorName = "Dialpad.Webhook";

    /// <summary>
    /// The Dialpad OAuth scope that allows access to a refresh token so access tokens can be renewed
    /// without prompting the user to reconnect.
    /// </summary>
    public const string OfflineAccessScope = "offline_access";

    /// <summary>
    /// The base address of the production Dialpad environment.
    /// </summary>
    public const string ProductionBaseUrl = "https://dialpad.com";

    /// <summary>
    /// The base address of the sandbox Dialpad environment.
    /// </summary>
    public const string SandboxBaseUrl = "https://sandbox.dialpad.com";

    /// <summary>
    /// Gets the base address for the given Dialpad environment.
    /// </summary>
    /// <param name="environment">The Dialpad environment.</param>
    /// <returns>The environment base address.</returns>
    public static string GetBaseUrl(DialpadEnvironment environment)
        => environment == DialpadEnvironment.Sandbox ? SandboxBaseUrl : ProductionBaseUrl;

    /// <summary>
    /// Gets the OAuth 2.0 authorization endpoint for the given Dialpad environment.
    /// </summary>
    /// <param name="environment">The Dialpad environment.</param>
    /// <returns>The authorization endpoint URL.</returns>
    public static string GetAuthorizeUrl(DialpadEnvironment environment)
        => $"{GetBaseUrl(environment)}/oauth2/authorize";

    /// <summary>
    /// Gets the OAuth 2.0 token endpoint for the given Dialpad environment.
    /// </summary>
    /// <param name="environment">The Dialpad environment.</param>
    /// <returns>The token endpoint URL.</returns>
    public static string GetTokenUrl(DialpadEnvironment environment)
        => $"{GetBaseUrl(environment)}/oauth2/token";

    /// <summary>
    /// Gets the OAuth 2.0 deauthorize endpoint for the given Dialpad environment, used to revoke the
    /// tokens issued to the application on behalf of a user.
    /// </summary>
    /// <param name="environment">The Dialpad environment.</param>
    /// <returns>The deauthorize endpoint URL.</returns>
    public static string GetDeauthorizeUrl(DialpadEnvironment environment)
        => $"{GetBaseUrl(environment)}/oauth2/deauthorize";

    /// <summary>
    /// Gets the default REST API base address for the given Dialpad environment.
    /// </summary>
    /// <param name="environment">The Dialpad environment.</param>
    /// <returns>The REST API base address.</returns>
    public static string GetApiBaseUrl(DialpadEnvironment environment)
        => $"{GetBaseUrl(environment)}/api/v2/";

    /// <summary>
    /// Contains the feature identifiers exposed by the Dialpad module.
    /// </summary>
    public static class Feature
    {
        /// <summary>
        /// The identifier of the Dialpad provider feature.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.Dialpad";

        /// <summary>
        /// The identifier of the Dialpad Contact Center voice-provider feature.
        /// </summary>
        public const string ContactCenterVoice = "CrestApps.OrchardCore.Dialpad.ContactCenterVoice";
    }
}
