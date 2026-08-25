using CrestApps.OrchardCore.Telnyx.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Resolves the Telnyx settings and their protected secrets when the tenant options are first loaded.
/// </summary>
internal sealed class TelnyxOptionsConfigurations : IConfigureOptions<TelnyxOptions>
{
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxOptionsConfigurations"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read Telnyx settings.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to unprotect secrets.</param>
    /// <param name="logger">The logger.</param>
    public TelnyxOptionsConfigurations(
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<TelnyxOptionsConfigurations> logger)
    {
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public void Configure(TelnyxOptions options)
    {
        var settings = _siteService.GetSettings<TelnyxSettings>();
        var apiKeyProtector = _dataProtectionProvider.CreateProtector(TelnyxConstants.ProtectorName);

        options.IsEnabled = settings.IsEnabled;
        options.ConnectionId = settings.ConnectionId?.Trim();
        options.SipConnectionId = string.IsNullOrWhiteSpace(settings.SipConnectionId)
            ? settings.ConnectionId?.Trim()
            : settings.SipConnectionId.Trim();
        options.OutboundVoiceProfileId = settings.OutboundVoiceProfileId?.Trim();
        options.CredentialLifetimeMinutes = settings.CredentialLifetimeMinutes > 0 ? settings.CredentialLifetimeMinutes : 180;
        options.DefaultOutboundCallerId = settings.DefaultOutboundCallerId?.Trim();
        options.ApiKey = string.IsNullOrEmpty(settings.ApiKey) ? null : Unprotect(apiKeyProtector, settings.ApiKey);
        options.TurnCredential = string.IsNullOrEmpty(settings.TurnCredential) ? null : Unprotect(apiKeyProtector, settings.TurnCredential);
        options.TurnUsername = settings.TurnUsername?.Trim();
        options.IceUrls = string.IsNullOrWhiteSpace(settings.IceUrls) ? TelnyxConstants.DefaultStunUrl : settings.IceUrls.Trim();
        options.IceTransportPolicy = string.IsNullOrWhiteSpace(settings.IceTransportPolicy) ? "all" : settings.IceTransportPolicy.Trim();
        options.WebRtcCodecs = settings.WebRtcCodecs?.Trim();
        options.SipWebSocketUrl = string.IsNullOrWhiteSpace(settings.SipWebSocketUrl)
            ? TelnyxConstants.DefaultSipWebSocketUrl
            : settings.SipWebSocketUrl.Trim();
        options.SipDomain = string.IsNullOrWhiteSpace(settings.SipDomain)
            ? TelnyxConstants.DefaultSipDomain
            : settings.SipDomain.Trim();
        options.EchoTestDestination = settings.EchoTestDestination?.Trim();

        options.ApiBaseUrl = ResolveApiBaseUrl(settings.ApiBaseUrl);
    }

    private static string ResolveApiBaseUrl(string configuredBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return TelnyxConstants.DefaultApiBaseUrl;
        }

        var trimmed = configuredBaseUrl.Trim();

        return trimmed.EndsWith('/') ? trimmed : trimmed + '/';
    }

    private string Unprotect(IDataProtector protector, string value)
    {
        try
        {
            return protector.Unprotect(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to unprotect a Telnyx secret.");

            return null;
        }
    }
}
