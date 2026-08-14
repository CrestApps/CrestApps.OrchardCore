using CrestApps.OrchardCore.Dialpad.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Dialpad.ViewModels;

/// <summary>
/// View model for editing the Dialpad provider settings.
/// </summary>
public class DialpadSettingsViewModel
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
    /// Gets or sets the selected Dialpad authentication type.
    /// </summary>
    public DialpadAuthenticationType AuthenticationType { get; set; }

    /// <summary>
    /// Gets or sets the Dialpad API key used when API key authentication is selected.
    /// </summary>
    public string ApiToken { get; set; }

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
    /// Gets or sets the caller identifier presented on outbound calls.
    /// </summary>
    public string OutboundCallerId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the Dialpad user that places outbound calls.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the Dialpad webhook signing secret used to validate inbound call-event webhooks.
    /// </summary>
    public string WebhookSigningSecret { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an API key has already been saved.
    /// </summary>
    [BindNever]
    public bool HasApiToken { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an OAuth client secret has already been saved.
    /// </summary>
    [BindNever]
    public bool HasClientSecret { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a webhook signing secret has already been saved.
    /// </summary>
    [BindNever]
    public bool HasWebhookSigningSecret { get; set; }
}
