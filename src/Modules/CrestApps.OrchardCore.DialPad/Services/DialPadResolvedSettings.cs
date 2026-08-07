using CrestApps.OrchardCore.DialPad.Models;

namespace CrestApps.OrchardCore.DialPad.Services;

/// <summary>
/// Represents the DialPad settings for the active environment with secrets already unprotected, used by
/// the provider at runtime. This flattens the environment-specific credentials so call-control code does
/// not need to know which environment is active.
/// </summary>
internal sealed class DialPadResolvedSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the DialPad provider is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the active DialPad environment the credentials belong to.
    /// </summary>
    public DialPadEnvironment Environment { get; set; }

    /// <summary>
    /// Gets or sets the DialPad authentication type for the active environment.
    /// </summary>
    public DialPadAuthenticationType AuthenticationType { get; set; }

    /// <summary>
    /// Gets or sets the unprotected DialPad API key used when API key authentication is selected.
    /// </summary>
    public string ApiToken { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the DialPad user that places outbound calls.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the caller identifier presented on outbound calls.
    /// </summary>
    public string OutboundCallerId { get; set; }

    /// <summary>
    /// Gets or sets the OAuth client identifier issued by DialPad.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the unprotected OAuth client secret issued by DialPad.
    /// </summary>
    public string ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the space-separated OAuth scopes requested during authorization.
    /// </summary>
    public string Scopes { get; set; }

    /// <summary>
    /// Gets or sets an optional internal override for the DialPad REST API base address. When empty the
    /// default endpoint for the environment is used.
    /// </summary>
    public string ApiBaseUrl { get; set; }

    /// <summary>
    /// Gets the effective authentication type for the active environment, inferring it from the stored
    /// credentials when no explicit selection has been made.
    /// </summary>
    /// <returns>The effective authentication type.</returns>
    public DialPadAuthenticationType GetEffectiveAuthenticationType()
        => DialPadEnvironmentSettings.ResolveEffectiveAuthenticationType(AuthenticationType, ApiToken, ClientId, ClientSecret);
}
