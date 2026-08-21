using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using CrestApps.OrchardCore.Telephony.Sms.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Infrastructure;
using OrchardCore.Settings;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Telephony.Sms.Core.Services;

/// <summary>
/// The default <see cref="ISmsDispatcher"/>: resolves the provider that owns the sending number and sends
/// through it, so a portal whose numbers span multiple carriers routes each send to the correct provider.
/// </summary>
public sealed class SmsDispatcher : ISmsDispatcher
{
    private readonly IOmnichannelChannelEndpointManager _endpointManager;
    private readonly ISmsProviderResolver _providerResolver;
    private readonly ISiteService _siteService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsDispatcher"/> class.
    /// </summary>
    /// <param name="endpointManager">The channel endpoint manager used to look up the number's pinned provider.</param>
    /// <param name="providerResolver">The SMS provider resolver used to obtain a provider by technical name.</param>
    /// <param name="siteService">The site service used to read the portal and built-in SMS settings.</param>
    /// <param name="logger">The logger instance.</param>
    public SmsDispatcher(
        IOmnichannelChannelEndpointManager endpointManager,
        ISmsProviderResolver providerResolver,
        ISiteService siteService,
        ILogger<SmsDispatcher> logger)
    {
        _endpointManager = endpointManager;
        _providerResolver = providerResolver;
        _siteService = siteService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Result> SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrEmpty(message.From))
        {
            return Failed("The sending number (From) is required to resolve a provider.");
        }

        var providerName = await ResolveProviderNameAsync(message.From, cancellationToken);

        if (string.IsNullOrEmpty(providerName))
        {
            return Failed("No SMS provider could be resolved for the sending number, the portal default, or the tenant default.");
        }

        var provider = await _providerResolver.GetAsync(providerName);

        if (provider is null)
        {
            _logger.LogWarning("The resolved SMS provider '{ProviderName}' is not registered or enabled.", providerName);

            return Failed($"The SMS provider '{providerName}' is not registered or enabled.");
        }

        return await provider.SendAsync(message, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<string> ResolveProviderNameAsync(string fromNumber, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(fromNumber))
        {
            var endpoint = await _endpointManager.GetByServiceAddressAsync(
                OmnichannelConstants.Channels.Sms,
                fromNumber.GetCleanedPhoneNumber(),
                cancellationToken);

            if (endpoint is not null && !string.IsNullOrEmpty(endpoint.ProviderName))
            {
                return endpoint.ProviderName;
            }
        }

        var portalSettings = await _siteService.GetSettingsAsync<SmsPortalSettings>();

        if (!string.IsNullOrEmpty(portalSettings.DefaultProviderName))
        {
            return portalSettings.DefaultProviderName;
        }

        var smsSettings = await _siteService.GetSettingsAsync<SmsSettings>();

        return smsSettings.DefaultProviderName;
    }

    private static Result Failed(string message)
        => Result.Failed(new LocalizedString(message, message));
}
