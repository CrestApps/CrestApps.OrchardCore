using CrestApps.Core.Support;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Infrastructure;
using OrchardCore.Settings;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// The default <see cref="ISmsProviderRouter"/>: the sending number's SMS channel endpoint names its provider, and
/// the tenant-default SMS provider covers a number without one.
/// </summary>
public sealed class SmsProviderRouter : ISmsProviderRouter
{
    private readonly IOmnichannelChannelEndpointManager _endpointManager;
    private readonly ISmsProviderResolver _providerResolver;
    private readonly ISiteService _siteService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsProviderRouter"/> class.
    /// </summary>
    /// <param name="endpointManager">The channel endpoint manager used to look up the number's pinned provider.</param>
    /// <param name="providerResolver">The SMS provider resolver used to obtain a provider by technical name.</param>
    /// <param name="siteService">The site service used to read the tenant-default SMS provider.</param>
    /// <param name="logger">The logger instance.</param>
    public SmsProviderRouter(
        IOmnichannelChannelEndpointManager endpointManager,
        ISmsProviderResolver providerResolver,
        ISiteService siteService,
        ILogger<SmsProviderRouter> logger)
    {
        _endpointManager = endpointManager;
        _providerResolver = providerResolver;
        _siteService = siteService;
        _logger = logger;
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
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "SMS provider '{ProviderName}' chosen by the sending number's channel endpoint {EndpointId}.",
                        endpoint.ProviderName.SanitizeLogValue(),
                        endpoint.ItemId.SanitizeLogValue());
                }

                return endpoint.ProviderName;
            }
        }

        // Fall back to OrchardCore's tenant-default SMS provider (Configuration -> Settings -> SMS).
        var smsSettings = await _siteService.GetSettingsAsync<SmsSettings>();

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "SMS provider '{ProviderName}' chosen as the tenant default, because the sending number has no channel endpoint that names a provider.",
                smsSettings.DefaultProviderName.SanitizeLogValue());
        }

        return smsSettings.DefaultProviderName;
    }

    /// <inheritdoc/>
    public async Task<ISmsProvider> GetProviderAsync(string providerName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(providerName))
        {
            return null;
        }

        // Orchard Core's provider resolver takes no token, so a send that was already cancelled stops here instead.
        cancellationToken.ThrowIfCancellationRequested();

        var provider = await _providerResolver.GetAsync(providerName);

        if (provider is null)
        {
            // Usually a number pinned to a provider whose feature is off or whose settings are disabled. The text is
            // not sent through another provider instead, because that provider does not own the number.
            _logger.LogWarning("The resolved SMS provider '{ProviderName}' is not registered or enabled.", providerName.SanitizeLogValue());
        }

        return provider;
    }

    /// <inheritdoc/>
    public async Task<Result> SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var providerName = await ResolveProviderNameAsync(message.From, cancellationToken);

        if (string.IsNullOrEmpty(providerName))
        {
            _logger.LogWarning("No SMS provider could be resolved for the sending number or the tenant default, so the message was not sent.");

            return Failed("No SMS provider could be resolved for the sending number or the tenant default.");
        }

        var provider = await GetProviderAsync(providerName, cancellationToken);

        if (provider is null)
        {
            return Failed($"The SMS provider '{providerName}' is not registered or enabled.");
        }

        return await provider.SendAsync(message, cancellationToken);
    }

    private static Result Failed(string message)
        => Result.Failed(new LocalizedString(message, message));
}
