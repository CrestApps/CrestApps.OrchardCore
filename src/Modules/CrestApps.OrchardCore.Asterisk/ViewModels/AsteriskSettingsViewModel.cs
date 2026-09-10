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
    /// Gets or sets the voicemail dialplan context.
    /// </summary>
    public string VoicemailContext { get; set; }

    /// <summary>
    /// Gets or sets the template used to resolve the voicemail dialplan extension.
    /// </summary>
    public string VoicemailExtensionTemplate { get; set; }

    /// <summary>
    /// Gets or sets the voicemail dialplan priority.
    /// </summary>
    public int VoicemailPriority { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether a password has already been saved.
    /// </summary>
    [BindNever]
    public bool HasPassword { get; set; }

    /// <summary>
    /// Gets or sets the secure WebSocket URL used by browser SIP user agents.
    /// </summary>
    public string WebSocketUrl { get; set; }

    /// <summary>
    /// Gets or sets the SIP domain used to compose browser agent addresses of record.
    /// </summary>
    public string SipDomain { get; set; }

    /// <summary>
    /// Gets or sets the STUN/TURN URLs advertised to browser agents.
    /// </summary>
    public string TurnUrls { get; set; }

    /// <summary>
    /// Gets or sets the coturn REST shared secret.
    /// </summary>
    public string TurnSharedSecret { get; set; }

    /// <summary>
    /// Gets or sets the ICE transport policy sent to browsers.
    /// </summary>
    public string IceTransportPolicy { get; set; } = AsteriskConstants.DefaultIceTransportPolicy;

    /// <summary>
    /// Gets or sets the browser audio codec preference list.
    /// </summary>
    public string WebRtcCodecs { get; set; } = AsteriskConstants.DefaultWebRtcCodecs;

    /// <summary>
    /// Gets or sets the short-lived PJSIP credential lifetime, in minutes.
    /// </summary>
    public int PjsipCredentialLifetimeMinutes { get; set; } = AsteriskConstants.DefaultPjsipCredentialLifetimeMinutes;

    /// <summary>
    /// Gets or sets the PJSIP contact expiration, in seconds.
    /// </summary>
    public int PjsipContactExpirationSeconds { get; set; } = AsteriskConstants.DefaultPjsipContactExpirationSeconds;

    /// <summary>
    /// Gets or sets a value indicating whether a TURN shared secret has already been saved.
    /// </summary>
    [BindNever]
    public bool HasTurnSharedSecret { get; set; }

    /// <summary>
    /// Gets or sets the ADO.NET provider invariant name used for PJSIP Realtime writes.
    /// </summary>
    public string PjsipRealtimeProviderInvariantName { get; set; }

    /// <summary>
    /// Gets or sets the PJSIP Realtime connection string.
    /// </summary>
    public string PjsipRealtimeConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the optional PJSIP Realtime table prefix.
    /// </summary>
    public string PjsipRealtimeTablePrefix { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a PJSIP Realtime connection string has already been saved.
    /// </summary>
    [BindNever]
    public bool HasPjsipRealtimeConnectionString { get; set; }
}
