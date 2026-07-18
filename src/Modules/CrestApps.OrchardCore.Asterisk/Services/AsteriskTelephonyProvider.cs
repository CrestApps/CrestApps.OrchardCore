using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// A telephony provider that controls calls through a tenant-configured Asterisk ARI endpoint.
/// </summary>
internal sealed class AsteriskTelephonyProvider : AsteriskTelephonyProviderBase
{
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IAsteriskAriApplicationGate _applicationGate;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskTelephonyProvider"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read the tenant-configured Asterisk settings.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to unprotect the stored password.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="applicationGate">The gate that enforces single-tenant ownership of each ARI application.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AsteriskTelephonyProvider(
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IHttpClientFactory httpClientFactory,
        IAsteriskAriApplicationGate applicationGate,
        IClock clock,
        ILogger<AsteriskTelephonyProvider> logger,
        IStringLocalizer<AsteriskTelephonyProvider> stringLocalizer)
        : base(httpClientFactory, clock, logger, stringLocalizer)
    {
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _applicationGate = applicationGate;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override LocalizedString Name => S["Asterisk"];

    /// <inheritdoc/>
    public override TelephonyCapabilities Capabilities
        => GetCapabilities(
            _siteService.GetSettings<AsteriskSettings>()?.EndpointTemplate,
            AsteriskSettingsUtilities.HasVoicemailConfiguration(_siteService.GetSettings<AsteriskSettings>()));

    /// <inheritdoc/>
    public override TelephonyAudioCapabilities AudioCapabilities
        => AsteriskSettingsUtilities.HasRequiredWebRtcConfiguration(_siteService.GetSettings<AsteriskSettings>())
            ? TelephonyAudioCapabilities.Browser
            : TelephonyAudioCapabilities.None;

    /// <inheritdoc/>
    public override TelephonyAudioMode ConfiguredAudioMode
        => AsteriskSettingsUtilities.HasRequiredWebRtcConfiguration(_siteService.GetSettings<AsteriskSettings>())
            ? TelephonyAudioMode.Browser
            : TelephonyAudioMode.None;

    /// <inheritdoc/>
    public override string BrowserMediaAdapterName
        => AsteriskSettingsUtilities.HasRequiredWebRtcConfiguration(_siteService.GetSettings<AsteriskSettings>())
            ? AsteriskConstants.BrowserMediaAdapterName
            : null;

    protected override string ProviderName
        => AsteriskConstants.ProviderTechnicalName;

    protected override ValueTask<AsteriskResolvedSettings> GetResolvedSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = _siteService.GetSettings<AsteriskSettings>();
        var resolved = new AsteriskResolvedSettings
        {
            IsEnabled = settings.IsEnabled,
            ProviderName = ProviderName,
            BaseUrl = settings.BaseUrl,
            UserName = settings.UserName,
            Password = UnprotectPassword(settings.Password),
            ApplicationName = settings.ApplicationName,
            EndpointTemplate = settings.EndpointTemplate,
            OutboundCallerId = settings.OutboundCallerId,
            TimeoutSeconds = settings.TimeoutSeconds,
            VoicemailContext = settings.VoicemailContext,
            VoicemailExtensionTemplate = settings.VoicemailExtensionTemplate,
            VoicemailPriority = settings.VoicemailPriority,
            WebSocketUrl = settings.WebSocketUrl,
            SipDomain = settings.SipDomain,
            TurnUrls = settings.TurnUrls,
            TurnSharedSecret = UnprotectPassword(settings.TurnSharedSecret),
            IceTransportPolicy = settings.IceTransportPolicy,
            WebRtcCodecs = settings.WebRtcCodecs,
            PjsipCredentialLifetimeMinutes = settings.PjsipCredentialLifetimeMinutes,
            PjsipContactExpirationSeconds = settings.PjsipContactExpirationSeconds,
            PjsipRealtimeProviderInvariantName = settings.PjsipRealtimeProviderInvariantName,
            PjsipRealtimeConnectionString = settings.PjsipRealtimeConnectionString,
            PjsipRealtimeTablePrefix = settings.PjsipRealtimeTablePrefix,
        };

        AsteriskSettingsUtilities.ApplyDefaults(new AsteriskConnectionSettingsAdapter(resolved));

        // Every telephony operation resolves settings through this method before it originates into the configured
        // Stasis application, so enforcing ownership here fails closed for a tenant that does not own the application:
        // the base provider then reports the provider as not configured instead of originating into a shared app.
        if (resolved.IsEnabled && !_applicationGate.TryAcquire(resolved))
        {
            resolved.IsEnabled = false;
        }

        return ValueTask.FromResult(resolved);
    }

    private string UnprotectPassword(string protectedPassword)
    {
        if (string.IsNullOrWhiteSpace(protectedPassword))
        {
            return null;
        }

        try
        {
            return _dataProtectionProvider.CreateProtector(AsteriskConstants.ProtectorName).Unprotect(protectedPassword);
        }
        catch (Exception ex)
        {
            _logger.LogError(OperationalLogRedactor.RedactException(ex), "Failed to unprotect the tenant-configured Asterisk password.");

            return null;
        }
    }

    private sealed class AsteriskConnectionSettingsAdapter : AsteriskConnectionSettings
    {
        private readonly AsteriskResolvedSettings _settings;

        public AsteriskConnectionSettingsAdapter(AsteriskResolvedSettings settings)
        {
            _settings = settings;
        }

        public override string BaseUrl
        {
            get => _settings.BaseUrl;
            set => _settings.BaseUrl = value;
        }

        public override string UserName
        {
            get => _settings.UserName;
            set => _settings.UserName = value;
        }

        public override string ApplicationName
        {
            get => _settings.ApplicationName;
            set => _settings.ApplicationName = value;
        }

        public override string EndpointTemplate
        {
            get => _settings.EndpointTemplate;
            set => _settings.EndpointTemplate = value;
        }

        public override string OutboundCallerId
        {
            get => _settings.OutboundCallerId;
            set => _settings.OutboundCallerId = value;
        }

        public override int TimeoutSeconds
        {
            get => _settings.TimeoutSeconds;
            set => _settings.TimeoutSeconds = value;
        }

        public override string VoicemailContext
        {
            get => _settings.VoicemailContext;
            set => _settings.VoicemailContext = value;
        }

        public override string VoicemailExtensionTemplate
        {
            get => _settings.VoicemailExtensionTemplate;
            set => _settings.VoicemailExtensionTemplate = value;
        }

        public override int VoicemailPriority
        {
            get => _settings.VoicemailPriority;
            set => _settings.VoicemailPriority = value;
        }

        public override string WebSocketUrl
        {
            get => _settings.WebSocketUrl;
            set => _settings.WebSocketUrl = value;
        }

        public override string SipDomain
        {
            get => _settings.SipDomain;
            set => _settings.SipDomain = value;
        }

        public override string TurnUrls
        {
            get => _settings.TurnUrls;
            set => _settings.TurnUrls = value;
        }

        public override string TurnSharedSecret
        {
            get => _settings.TurnSharedSecret;
            set => _settings.TurnSharedSecret = value;
        }

        public override string IceTransportPolicy
        {
            get => _settings.IceTransportPolicy;
            set => _settings.IceTransportPolicy = value;
        }

        public override string WebRtcCodecs
        {
            get => _settings.WebRtcCodecs;
            set => _settings.WebRtcCodecs = value;
        }

        public override int PjsipCredentialLifetimeMinutes
        {
            get => _settings.PjsipCredentialLifetimeMinutes;
            set => _settings.PjsipCredentialLifetimeMinutes = value;
        }

        public override int PjsipContactExpirationSeconds
        {
            get => _settings.PjsipContactExpirationSeconds;
            set => _settings.PjsipContactExpirationSeconds = value;
        }

        public override string PjsipRealtimeProviderInvariantName
        {
            get => _settings.PjsipRealtimeProviderInvariantName;
            set => _settings.PjsipRealtimeProviderInvariantName = value;
        }

        public override string PjsipRealtimeConnectionString
        {
            get => _settings.PjsipRealtimeConnectionString;
            set => _settings.PjsipRealtimeConnectionString = value;
        }

        public override string PjsipRealtimeTablePrefix
        {
            get => _settings.PjsipRealtimeTablePrefix;
            set => _settings.PjsipRealtimeTablePrefix = value;
        }
    }
}
