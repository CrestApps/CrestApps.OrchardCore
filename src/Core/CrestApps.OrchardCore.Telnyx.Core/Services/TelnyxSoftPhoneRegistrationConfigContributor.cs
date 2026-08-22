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
