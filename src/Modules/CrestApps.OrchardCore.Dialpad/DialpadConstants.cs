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
    /// The name of the data protector used to protect the Dialpad Admin API key used for webhook
    /// registration.
    /// </summary>
    public const string WebhookRegistrationProtectorName = "Dialpad.WebhookRegistration";

    /// <summary>
    /// The Dialpad OAuth scope that allows access to a refresh token so access tokens can be renewed
    /// without prompting the user to reconnect. Dialpad requires every scope, including this one, to be
    /// approved for the OAuth application, so it is only requested when an administrator adds it to the
    /// configured scopes.
    /// </summary>
    public const string OfflineAccessScope = "offline_access";

    /// <summary>
    /// The default host of the production Dialpad environment.
    /// </summary>
    public const string ProductionHost = "dialpad.com";

    /// <summary>
    /// The default host of the sandbox Dialpad environment. Dialpad exposes more than one sandbox-style
    /// host (for example a beta host), so this value is only a default that a tenant can override through
    /// its environment settings.
    /// </summary>
    public const string SandboxHost = "sandbox.dialpad.com";

    /// <summary>
    /// Gets the default host for the given Dialpad environment.
    /// </summary>
    /// <param name="environment">The Dialpad environment.</param>
    /// <returns>The default environment host.</returns>
    public static string GetDefaultHost(DialpadEnvironment environment)
        => environment == DialpadEnvironment.Sandbox ? SandboxHost : ProductionHost;

    /// <summary>
    /// Gets the scheme-qualified base address for the given Dialpad environment, applying an optional
    /// tenant-configured host override. When <paramref name="host"/> is empty the environment default host
    /// is used. A host without a scheme is assumed to use HTTPS.
    /// </summary>
    /// <param name="environment">The Dialpad environment.</param>
    /// <param name="host">The optional tenant-configured host override.</param>
    /// <returns>The environment base address.</returns>
    public static string GetBaseUrl(DialpadEnvironment environment, string host = null)
    {
        var effectiveHost = string.IsNullOrWhiteSpace(host) ? GetDefaultHost(environment) : host.Trim();

        if (!effectiveHost.Contains("://", StringComparison.Ordinal))
        {
            effectiveHost = "https://" + effectiveHost;
        }

        return effectiveHost.TrimEnd('/');
    }

    /// <summary>
    /// Gets the OAuth 2.0 authorization endpoint for the given Dialpad environment.
    /// </summary>
    /// <param name="environment">The Dialpad environment.</param>
    /// <param name="host">The optional tenant-configured host override.</param>
    /// <returns>The authorization endpoint URL.</returns>
    public static string GetAuthorizeUrl(DialpadEnvironment environment, string host = null)
        => $"{GetBaseUrl(environment, host)}/oauth2/authorize";

    /// <summary>
    /// Gets the OAuth 2.0 token endpoint for the given Dialpad environment.
    /// </summary>
    /// <param name="environment">The Dialpad environment.</param>
    /// <param name="host">The optional tenant-configured host override.</param>
    /// <returns>The token endpoint URL.</returns>
    public static string GetTokenUrl(DialpadEnvironment environment, string host = null)
        => $"{GetBaseUrl(environment, host)}/oauth2/token";

    /// <summary>
    /// Gets the OAuth 2.0 deauthorize endpoint for the given Dialpad environment, used to revoke the
    /// tokens issued to the application on behalf of a user.
    /// </summary>
    /// <param name="environment">The Dialpad environment.</param>
    /// <param name="host">The optional tenant-configured host override.</param>
    /// <returns>The deauthorize endpoint URL.</returns>
    public static string GetDeauthorizeUrl(DialpadEnvironment environment, string host = null)
        => $"{GetBaseUrl(environment, host)}/oauth2/deauthorize";

    /// <summary>
    /// Gets the default REST API base address for the given Dialpad environment.
    /// </summary>
    /// <param name="environment">The Dialpad environment.</param>
    /// <param name="host">The optional tenant-configured host override.</param>
    /// <returns>The REST API base address.</returns>
    public static string GetApiBaseUrl(DialpadEnvironment environment, string host = null)
        => $"{GetBaseUrl(environment, host)}/api/v2/";

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
