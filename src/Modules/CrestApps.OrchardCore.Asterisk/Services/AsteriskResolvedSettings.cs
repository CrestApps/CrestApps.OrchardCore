namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed class AsteriskResolvedSettings
{
    public bool IsEnabled { get; set; }

    public string ProviderName { get; set; }

    public string BaseUrl { get; set; }

    public string UserName { get; set; }

    public string Password { get; set; }

    public string ApplicationName { get; set; }

    public string EndpointTemplate { get; set; }

    public string OutboundCallerId { get; set; }

    public int TimeoutSeconds { get; set; }

    public string VoicemailContext { get; set; }

    public string VoicemailExtensionTemplate { get; set; }

    public int VoicemailPriority { get; set; }

    public string WebSocketUrl { get; set; }

    public string SipDomain { get; set; }

    public string TurnUrls { get; set; }

    public string TurnSharedSecret { get; set; }

    public string IceTransportPolicy { get; set; }

    public string WebRtcCodecs { get; set; }

    public int PjsipCredentialLifetimeMinutes { get; set; }

    public int PjsipContactExpirationSeconds { get; set; }

    public string PjsipRealtimeProviderInvariantName { get; set; }

    public string PjsipRealtimeConnectionString { get; set; }

    public string PjsipRealtimeTablePrefix { get; set; }
}
