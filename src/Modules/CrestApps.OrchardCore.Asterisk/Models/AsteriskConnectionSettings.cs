namespace CrestApps.OrchardCore.Asterisk.Models;

/// <summary>
/// Represents the common Asterisk connection settings shared by the tenant and configuration-backed providers.
/// </summary>
public class AsteriskConnectionSettings
{
    /// <summary>
    /// Gets or sets the base URL of the Asterisk ARI endpoint.
    /// </summary>
    public virtual string BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the ARI user name.
    /// </summary>
    public virtual string UserName { get; set; }

    /// <summary>
    /// Gets or sets the Stasis application name the provider uses for originated channels.
    /// </summary>
    public virtual string ApplicationName { get; set; }

    /// <summary>
    /// Gets or sets an optional endpoint template used to turn the dialed destination into an ARI endpoint.
    /// Use the <c>{number}</c> token to inject the dialed destination.
    /// </summary>
    public virtual string EndpointTemplate { get; set; }

    /// <summary>
    /// Gets or sets the caller identifier presented on outbound calls.
    /// </summary>
    public virtual string OutboundCallerId { get; set; }

    /// <summary>
    /// Gets or sets the outbound dial timeout, in seconds.
    /// </summary>
    public virtual int TimeoutSeconds { get; set; } = AsteriskConstants.DefaultTimeoutSeconds;
}
