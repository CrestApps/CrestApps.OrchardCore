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

    /// <summary>
    /// Gets or sets the secure WebSocket URL used by browser SIP user agents.
    /// </summary>
    public virtual string WebSocketUrl { get; set; }

    /// <summary>
    /// Gets or sets the SIP domain used to compose browser agent addresses of record.
    /// </summary>
    public virtual string SipDomain { get; set; }

    /// <summary>
    /// Gets or sets the comma- or newline-delimited STUN/TURN URLs advertised to browser agents.
    /// </summary>
    public virtual string TurnUrls { get; set; }

    /// <summary>
    /// Gets or sets the coturn REST shared secret used to issue time-limited TURN credentials.
    /// </summary>
    public virtual string TurnSharedSecret { get; set; }

    /// <summary>
    /// Gets or sets the ICE transport policy sent to the browser.
    /// </summary>
    public virtual string IceTransportPolicy { get; set; } = AsteriskConstants.DefaultIceTransportPolicy;

    /// <summary>
    /// Gets or sets the comma- or newline-delimited browser audio codec preference list.
    /// </summary>
    public virtual string WebRtcCodecs { get; set; } = AsteriskConstants.DefaultWebRtcCodecs;

    /// <summary>
    /// Gets or sets the short-lived PJSIP credential lifetime, in minutes.
    /// </summary>
    public virtual int PjsipCredentialLifetimeMinutes { get; set; } = AsteriskConstants.DefaultPjsipCredentialLifetimeMinutes;

    /// <summary>
    /// Gets or sets the PJSIP contact expiration, in seconds.
    /// </summary>
    public virtual int PjsipContactExpirationSeconds { get; set; } = AsteriskConstants.DefaultPjsipContactExpirationSeconds;

    /// <summary>
    /// Gets or sets the ADO.NET provider invariant name used to write PJSIP Realtime rows.
    /// </summary>
    public virtual string PjsipRealtimeProviderInvariantName { get; set; }

    /// <summary>
    /// Gets or sets the PJSIP Realtime database connection string.
    /// </summary>
    public virtual string PjsipRealtimeConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the optional PJSIP Realtime table prefix.
    /// </summary>
    public virtual string PjsipRealtimeTablePrefix { get; set; }
}
