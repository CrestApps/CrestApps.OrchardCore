using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Builds the browser soft-phone registration configuration for Telnyx. It mints a short-lived SIP
/// credential from Telnyx and returns the login (SIP username), credential (SIP password), ICE, and media
/// configuration the Telnyx WebRTC SDK browser media adapter logs in with.
/// </summary>
public sealed class TelnyxSoftPhoneRegistrationConfigContributor : ISoftPhoneRegistrationConfigContributor
{
    private readonly ITelnyxTelephonyCredentialIssuer _credentialIssuer;
    private readonly TelnyxOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxSoftPhoneRegistrationConfigContributor"/> class.
    /// </summary>
    public TelnyxSoftPhoneRegistrationConfigContributor(
        ITelnyxTelephonyCredentialIssuer credentialIssuer,
        IOptionsMonitor<TelnyxOptions> telnyxOptions)
    {
        _credentialIssuer = credentialIssuer;
        _options = telnyxOptions.CurrentValue;
    }

    /// <inheritdoc/>
    public string ProviderName => TelnyxConstants.ProviderTechnicalName;

    /// <inheritdoc/>
    public async Task<SoftPhoneRegistrationConfig> BuildAsync(
        SoftPhoneRegistrationConfigContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_options.IsConfigured || string.IsNullOrWhiteSpace(_options.SipConnectionId) || string.IsNullOrWhiteSpace(context.UserId))
        {
            return null;
        }

        var credential = await _credentialIssuer.IssueAsync(context.UserId, context.DisplayName, cancellationToken);

        if (credential is null)
        {
            return null;
        }

        var codecs = ParseDelimited(_options.WebRtcCodecs);

        if (codecs.Count == 0)
        {
            // Default codec preference: Opus first (best quality and loss resilience) then the codecs Telnyx
            // actually negotiates on its SIP/PSTN/conference media paths. The browser applies this only to the
            // ordering of its SDP offer (via setCodecPreferences); Telnyx's answer picks the final codec, so
            // Opus is used only if Telnyx supports it on the path. Today Telnyx transcodes those legs to
            // G711/G722, so Opus is not negotiated -- this default keeps a clean, explicit preference so Opus is
            // used automatically if Telnyx ever enables it, without fabricating a capability that does not exist.
            codecs = ["opus", "G722", "PCMU", "PCMA"];
        }

        return new SoftPhoneRegistrationConfig
        {
            Provider = TelnyxConstants.ProviderTechnicalName,
            Signaling = new SoftPhoneSignalingConfig
            {
                WebSocketUrl = _options.SipWebSocketUrl,
                SipUri = $"sip:{credential.SipUsername}@{_options.SipDomain}",
                AuthorizationUser = credential.SipUsername,
                DisplayName = context.DisplayName,
            },
            Credential = new SoftPhoneCredentialConfig
            {
                Type = "password",
                Value = credential.SipPassword,
                ExpiresAtUtc = credential.ExpiresAtUtc,
            },
            Ice = new SoftPhoneIceConfig
            {
                IceServers = BuildIceServers(),
                IceTransportPolicy = _options.IceTransportPolicy,
            },
            Media = new SoftPhoneMediaConfig
            {
                Codecs = codecs,
            },
            Session = new SoftPhoneSessionConfig
            {
                InteractionId = credential.CredentialId,
                ExpiresAtUtc = credential.ExpiresAtUtc,
            },
            // Telnyx rejects a server-originated call placed to a registered WebRTC credential, so the browser
            // places its own outbound calls through the Telnyx WebRTC SDK, presenting the tenant caller id as
            // the newCall callerNumber.
            ClientOriginatesCalls = true,
            OutboundCallerId = _options.DefaultOutboundCallerId,
            EchoTestDestination = _options.EchoTestDestination,
        };
    }

    private IList<SoftPhoneIceServerConfig> BuildIceServers()
    {
        var urls = ParseDelimited(_options.IceUrls);

        if (urls.Count == 0)
        {
            return [];
        }

        var server = new SoftPhoneIceServerConfig
        {
            Urls = urls,
        };

        if (!string.IsNullOrWhiteSpace(_options.TurnUsername) && !string.IsNullOrWhiteSpace(_options.TurnCredential))
        {
            server.Username = _options.TurnUsername;
            server.Credential = _options.TurnCredential;
        }

        return [server];
    }

    private static List<string> ParseDelimited(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
