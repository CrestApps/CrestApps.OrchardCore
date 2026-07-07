using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Asterisk.ViewModels;

/// <summary>
/// View model for editing the tenant-configured Asterisk provider settings.
/// </summary>
public class AsteriskSettingsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the tenant-configured Asterisk provider is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the base URL of the Asterisk ARI endpoint.
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the ARI user name.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the ARI password.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Gets or sets the Stasis application name used for originated channels.
    /// </summary>
    public string ApplicationName { get; set; }

    /// <summary>
    /// Gets or sets the optional endpoint template used to turn the dialed destination into an ARI endpoint.
    /// </summary>
    public string EndpointTemplate { get; set; }

    /// <summary>
    /// Gets or sets the caller identifier presented on outbound calls.
    /// </summary>
    public string OutboundCallerId { get; set; }

    /// <summary>
    /// Gets or sets the outbound dial timeout, in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = AsteriskConstants.DefaultTimeoutSeconds;

    /// <summary>
    /// Gets or sets a value indicating whether a password has already been saved.
    /// </summary>
    [BindNever]
    public bool HasPassword { get; set; }
}
