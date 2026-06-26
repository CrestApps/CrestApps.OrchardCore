using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Builders;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Resolves the configured telephony provider using the registered <see cref="TelephonyProviderOptions"/>
/// and the tenant's <see cref="TelephonySettings"/>.
/// </summary>
public sealed class DefaultTelephonyProviderResolver : ITelephonyProviderResolver
{
    private readonly ISiteService _siteService;
    private readonly IServiceProvider _serviceProvider;
    private readonly TelephonyProviderOptions _providerOptions;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTelephonyProviderResolver"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read the default provider name.</param>
    /// <param name="providerOptions">The registered telephony provider options.</param>
    /// <param name="serviceProvider">The service provider used to instantiate the provider.</param>
    /// <param name="logger">The logger.</param>
    public DefaultTelephonyProviderResolver(
        ISiteService siteService,
        IOptions<TelephonyProviderOptions> providerOptions,
        IServiceProvider serviceProvider,
        ILogger<DefaultTelephonyProviderResolver> logger)
    {
        _siteService = siteService;
        _serviceProvider = serviceProvider;
        _providerOptions = providerOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ITelephonyProvider> GetAsync(string name = null)
    {
        if (string.IsNullOrEmpty(name))
        {
            var settings = await _siteService.GetSettingsAsync<TelephonySettings>();

            name = settings.DefaultProviderName;
        }

        if (string.IsNullOrEmpty(name))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("No default telephony provider is configured.");
            }

            return null;
        }

        if (_providerOptions.Providers.TryGetValue(name, out var providerType) && providerType.IsEnabled)
        {
            return _serviceProvider.CreateInstance<ITelephonyProvider>(providerType.Type);
        }

        if (_logger.IsEnabled(LogLevel.Error))
        {
            _logger.LogError("No telephony provider is registered or enabled to match the given name {Name}.", name);
        }

        return null;
    }
}
