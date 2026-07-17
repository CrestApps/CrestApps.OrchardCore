using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed class AsteriskSoftPhoneRegistrationConfigContributor : ISoftPhoneRegistrationConfigContributor
{
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly DefaultAsteriskOptions _defaultOptions;
    private readonly IAsteriskPjsipCredentialIssuer _credentialIssuer;
    private readonly IClock _clock;

    public AsteriskSoftPhoneRegistrationConfigContributor(
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<DefaultAsteriskOptions> defaultOptions,
        IAsteriskPjsipCredentialIssuer credentialIssuer,
        IClock clock)
    {
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _defaultOptions = defaultOptions.Value;
        _credentialIssuer = credentialIssuer;
        _clock = clock;
    }

    public string ProviderName => AsteriskConstants.ProviderTechnicalName;

    public async Task<SoftPhoneRegistrationConfig> BuildAsync(
        SoftPhoneRegistrationConfigContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = ResolveSettings(context.ProviderName);

        if (!AsteriskSettingsUtilities.HasRequiredWebRtcConfiguration(settings))
        {
            return null;
        }

        var codecs = AsteriskSettingsUtilities.ParseDelimitedValues(settings.WebRtcCodecs);

        // The media session identity is server-owned: the issuer generates the session id and binds the
        // credential to the authenticated user. The caller-supplied interaction id is carried only as
        // non-authoritative metadata and never controls issuance. The per-user cap enforced by the issuer
        // bounds how many live credentials a single user can hold.
        var credential = await _credentialIssuer.IssueAsync(new AsteriskPjsipCredentialIssueRequest
        {
            UserId = context.UserId,
            DisplayName = context.DisplayName,
            InteractionId = context.InteractionId,
            SipDomain = settings.SipDomain,
            CredentialLifetime = TimeSpan.FromMinutes(settings.PjsipCredentialLifetimeMinutes),
            ContactExpiration = TimeSpan.FromSeconds(settings.PjsipContactExpirationSeconds),
            Codecs = codecs,
        }, cancellationToken);

        return new SoftPhoneRegistrationConfig
        {
            Provider = context.ProviderName,
            Signaling = new SoftPhoneSignalingConfig
            {
                WebSocketUrl = settings.WebSocketUrl,
                SipUri = credential.SipUri,
                AuthorizationUser = credential.AuthorizationUser,
                DisplayName = context.DisplayName,
            },
            Credential = new SoftPhoneCredentialConfig
            {
                Type = "password",
                Value = credential.Password,
                ExpiresAtUtc = credential.ExpiresAtUtc,
            },
            Ice = new SoftPhoneIceConfig
            {
                IceServers = BuildIceServers(settings, credential),
                IceTransportPolicy = settings.IceTransportPolicy,
            },
            Media = new SoftPhoneMediaConfig
            {
                Codecs = codecs.ToArray(),
            },
            Session = new SoftPhoneSessionConfig
            {
                InteractionId = credential.SessionId,
                ExpiresAtUtc = credential.ExpiresAtUtc,
            },
        };
    }

    private AsteriskResolvedSettings ResolveSettings(string providerName)
    {
        if (string.Equals(providerName, AsteriskConstants.DefaultProviderTechnicalName, StringComparison.Ordinal))
        {
            return new AsteriskResolvedSettings
            {
                IsEnabled = _defaultOptions.IsEnabled,
                ProviderName = AsteriskConstants.DefaultProviderTechnicalName,
                WebSocketUrl = _defaultOptions.WebSocketUrl,
                SipDomain = _defaultOptions.SipDomain,
                TurnUrls = _defaultOptions.TurnUrls,
                TurnSharedSecret = _defaultOptions.TurnSharedSecret,
                IceTransportPolicy = _defaultOptions.IceTransportPolicy,
                WebRtcCodecs = _defaultOptions.WebRtcCodecs,
                PjsipCredentialLifetimeMinutes = _defaultOptions.PjsipCredentialLifetimeMinutes,
                PjsipContactExpirationSeconds = _defaultOptions.PjsipContactExpirationSeconds,
                PjsipRealtimeProviderInvariantName = _defaultOptions.PjsipRealtimeProviderInvariantName,
                PjsipRealtimeConnectionString = _defaultOptions.PjsipRealtimeConnectionString,
                PjsipRealtimeTablePrefix = _defaultOptions.PjsipRealtimeTablePrefix,
            };
        }

        var settings = _siteService.GetSettings<AsteriskSettings>();
        var resolved = new AsteriskResolvedSettings
        {
            IsEnabled = settings.IsEnabled,
            ProviderName = AsteriskConstants.ProviderTechnicalName,
            WebSocketUrl = settings.WebSocketUrl,
            SipDomain = settings.SipDomain,
            TurnUrls = settings.TurnUrls,
            TurnSharedSecret = Unprotect(settings.TurnSharedSecret),
            IceTransportPolicy = settings.IceTransportPolicy,
            WebRtcCodecs = settings.WebRtcCodecs,
            PjsipCredentialLifetimeMinutes = settings.PjsipCredentialLifetimeMinutes,
            PjsipContactExpirationSeconds = settings.PjsipContactExpirationSeconds,
            PjsipRealtimeProviderInvariantName = settings.PjsipRealtimeProviderInvariantName,
            PjsipRealtimeConnectionString = settings.PjsipRealtimeConnectionString,
            PjsipRealtimeTablePrefix = settings.PjsipRealtimeTablePrefix,
        };

        AsteriskSettingsUtilities.ApplyDefaults(resolved);

        return resolved;
    }

    private IList<SoftPhoneIceServerConfig> BuildIceServers(
        AsteriskResolvedSettings settings,
        AsteriskPjsipCredential credential)
    {
        var urls = AsteriskSettingsUtilities.ParseDelimitedValues(settings.TurnUrls);

        if (urls.Count == 0)
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(settings.TurnSharedSecret))
        {
            return [new SoftPhoneIceServerConfig { Urls = urls.ToArray() }];
        }

        var turnUserName = CreateTurnUserName(credential);
        var turnCredential = CreateTurnCredential(settings.TurnSharedSecret, turnUserName);

        return [new SoftPhoneIceServerConfig
        {
            Urls = urls.ToArray(),
            Username = turnUserName,
            Credential = turnCredential,
        }];
    }

    private string CreateTurnUserName(AsteriskPjsipCredential credential)
    {
        var expires = new DateTimeOffset(credential.ExpiresAtUtc, TimeSpan.Zero).ToUnixTimeSeconds();
        var now = new DateTimeOffset(_clock.UtcNow, TimeSpan.Zero).ToUnixTimeSeconds();
        var effectiveExpires = Math.Max(expires, now + 60);

        return $"{effectiveExpires}:{credential.TenantName}:{credential.SessionId}";
    }

    private static string CreateTurnCredential(
        string sharedSecret,
        string userName)
    {
        // coturn's TURN REST API (use-auth-secret) mandates the time-limited credential be derived as
        // base64(HMAC-SHA1(sharedSecret, username)). The algorithm is fixed by the coturn interop contract
        // and is used only to authenticate a short-lived, tenant-namespaced TURN username, not for
        // confidentiality, so the weak-algorithm analyzer warning is intentionally suppressed here.
#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms - required by the coturn TURN REST API contract.
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(sharedSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(userName));
#pragma warning restore CA5350

        return Convert.ToBase64String(hash);
    }

    private string Unprotect(string protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            return null;
        }

        try
        {
            return _dataProtectionProvider.CreateProtector(AsteriskConstants.ProtectorName).Unprotect(protectedValue);
        }
        catch
        {
            return null;
        }
    }
}
