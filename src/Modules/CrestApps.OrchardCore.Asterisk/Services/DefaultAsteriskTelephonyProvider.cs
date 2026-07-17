using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// A telephony provider that controls calls through a configuration-backed default Asterisk ARI endpoint.
/// </summary>
internal sealed class DefaultAsteriskTelephonyProvider : AsteriskTelephonyProviderBase
{
    private readonly DefaultAsteriskOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAsteriskTelephonyProvider"/> class.
    /// </summary>
    /// <param name="options">The configuration-backed default Asterisk options.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DefaultAsteriskTelephonyProvider(
        IOptions<DefaultAsteriskOptions> options,
        IHttpClientFactory httpClientFactory,
        IClock clock,
        ILogger<DefaultAsteriskTelephonyProvider> logger,
        IStringLocalizer<DefaultAsteriskTelephonyProvider> stringLocalizer)
        : base(httpClientFactory, clock, logger, stringLocalizer)
    {
        _options = options.Value;
    }

    /// <inheritdoc/>
    public override LocalizedString Name => S["Default Asterisk"];

    /// <inheritdoc/>
    public override TelephonyCapabilities Capabilities
        => GetCapabilities(_options.EndpointTemplate, AsteriskSettingsUtilities.HasVoicemailConfiguration(_options));

    /// <inheritdoc/>
    public override TelephonyAudioCapabilities AudioCapabilities
        => AsteriskSettingsUtilities.HasRequiredWebRtcConfiguration(_options)
            ? TelephonyAudioCapabilities.Browser
            : TelephonyAudioCapabilities.None;

    /// <inheritdoc/>
    public override TelephonyAudioMode ConfiguredAudioMode
        => AsteriskSettingsUtilities.HasRequiredWebRtcConfiguration(_options)
            ? TelephonyAudioMode.Browser
            : TelephonyAudioMode.None;

    /// <inheritdoc/>
    public override string BrowserMediaAdapterName
        => AsteriskSettingsUtilities.HasRequiredWebRtcConfiguration(_options)
            ? AsteriskConstants.BrowserMediaAdapterName
            : null;

    protected override string ProviderName
        => AsteriskConstants.DefaultProviderTechnicalName;

    protected override ValueTask<AsteriskResolvedSettings> GetResolvedSettingsAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult(new AsteriskResolvedSettings
        {
            IsEnabled = _options.IsEnabled,
            ProviderName = ProviderName,
            BaseUrl = _options.BaseUrl,
            UserName = _options.UserName,
            Password = _options.Password,
            ApplicationName = _options.ApplicationName,
            EndpointTemplate = _options.EndpointTemplate,
            OutboundCallerId = _options.OutboundCallerId,
            TimeoutSeconds = _options.TimeoutSeconds,
            VoicemailContext = _options.VoicemailContext,
            VoicemailExtensionTemplate = _options.VoicemailExtensionTemplate,
            VoicemailPriority = _options.VoicemailPriority,
            WebSocketUrl = _options.WebSocketUrl,
            SipDomain = _options.SipDomain,
            TurnUrls = _options.TurnUrls,
            TurnSharedSecret = _options.TurnSharedSecret,
            IceTransportPolicy = _options.IceTransportPolicy,
            WebRtcCodecs = _options.WebRtcCodecs,
            PjsipCredentialLifetimeMinutes = _options.PjsipCredentialLifetimeMinutes,
            PjsipContactExpirationSeconds = _options.PjsipContactExpirationSeconds,
            PjsipRealtimeProviderInvariantName = _options.PjsipRealtimeProviderInvariantName,
            PjsipRealtimeConnectionString = _options.PjsipRealtimeConnectionString,
            PjsipRealtimeTablePrefix = _options.PjsipRealtimeTablePrefix,
        });
}
