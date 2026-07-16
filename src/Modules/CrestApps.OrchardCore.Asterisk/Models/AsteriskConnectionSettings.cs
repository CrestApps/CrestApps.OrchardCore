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

    /// <summary>
    /// Gets or sets the dialplan context the provider should continue a call into when the user sends
    /// an inbound offer to voicemail from the soft phone.
    /// </summary>
    public virtual string VoicemailContext { get; set; }

    /// <summary>
    /// Gets or sets the template that resolves the dialplan extension for voicemail routing.
    /// The template may reference provider-neutral call metadata tokens such as
    /// <c>{voicemailRecipientUserName}</c> or <c>{calledAddress}</c>.
    /// </summary>
    public virtual string VoicemailExtensionTemplate { get; set; }

    /// <summary>
    /// Gets or sets the dialplan priority to use when continuing a call to voicemail.
    /// </summary>
    public virtual int VoicemailPriority { get; set; } = 1;
}
