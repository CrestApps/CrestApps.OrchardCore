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
        });
}
