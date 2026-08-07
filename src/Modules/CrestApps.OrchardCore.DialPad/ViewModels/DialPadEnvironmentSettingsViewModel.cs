using CrestApps.OrchardCore.DialPad.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.DialPad.ViewModels;

/// <summary>
/// View model for editing the credentials of a single DialPad environment (production or sandbox).
/// </summary>
public class DialPadEnvironmentSettingsViewModel
{
    /// <summary>
    /// Gets or sets the selected DialPad authentication type for this environment.
    /// </summary>
    public DialPadAuthenticationType AuthenticationType { get; set; }

    /// <summary>
    /// Gets or sets the DialPad API key used when API key authentication is selected.
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
    /// Gets or sets the OAuth client identifier.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the OAuth client secret.
    /// </summary>
    public string ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the space-separated OAuth scopes requested during authorization.
    /// </summary>
    public string Scopes { get; set; }

    /// <summary>
    /// Gets or sets the DialPad webhook signing secret used to validate inbound call-event webhooks.
    /// </summary>
    public string WebhookSigningSecret { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an API key has already been saved for this environment.
    /// </summary>
    [BindNever]
    public bool HasApiToken { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an OAuth client secret has already been saved for this
    /// environment.
    /// </summary>
    [BindNever]
    public bool HasClientSecret { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a webhook signing secret has already been saved for this
    /// environment.
    /// </summary>
    [BindNever]
    public bool HasWebhookSigningSecret { get; set; }
}
