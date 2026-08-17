using CrestApps.OrchardCore.Dialpad.Models;

namespace CrestApps.OrchardCore.Dialpad.Services;

/// <summary>
/// Represents the Dialpad settings for the active environment with secrets already unprotected, used by
/// the provider at runtime. This flattens the environment-specific credentials so call-control code does
/// not need to know which environment is active.
/// </summary>
internal sealed class DialpadResolvedSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the Dialpad provider is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the active Dialpad environment the credentials belong to.
    /// </summary>
    public DialpadEnvironment Environment { get; set; }

    /// <summary>
    /// Gets or sets the Dialpad authentication type for the active environment.
    /// </summary>
    public DialpadAuthenticationType AuthenticationType { get; set; }

    /// <summary>
    /// Gets or sets the host (domain) the provider connects to for the active environment. When empty the
    /// default host for the environment is used.
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// Gets or sets the unprotected Dialpad API key used when API key authentication is selected.
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
    /// Gets or sets the unprotected OAuth client secret issued by Dialpad.
    /// </summary>
    public string ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the space-separated OAuth scopes requested during authorization.
    /// </summary>
    public string Scopes { get; set; }

    /// <summary>
    /// Gets or sets an optional internal override for the Dialpad REST API base address. When empty the
    /// default endpoint for the environment is used.
    /// </summary>
    public string ApiBaseUrl { get; set; }

    /// <summary>
    /// Gets the effective authentication type for the active environment, inferring it from the stored
    /// credentials when no explicit selection has been made.
    /// </summary>
    /// <returns>The effective authentication type.</returns>
    public DialpadAuthenticationType GetEffectiveAuthenticationType()
        => DialpadEnvironmentSettings.ResolveEffectiveAuthenticationType(AuthenticationType, ApiToken, ClientId, ClientSecret);
}
