using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using CrestApps.Core.Telephony;

namespace CrestApps.Core.Telephony.Services;

/// <summary>
/// Resolves the configured telephony provider using the registered <see cref="TelephonyProviderOptions"/>
/// and the tenant's <see cref="TelephonySettings"/>.
/// </summary>
public sealed class DefaultTelephonyProviderResolver : ITelephonyProviderResolver
{
    private readonly IOptionsMonitor<TelephonySettings> _settings;
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<TelephonyProviderOptions> _providerOptions;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTelephonyProviderResolver"/> class.
    /// </summary>
    /// <param name="settings">The telephony settings the default provider name is read from.</param>
    /// <param name="providerOptions">The registered telephony provider options.</param>
    /// <param name="serviceProvider">The service provider used to instantiate the provider.</param>
    /// <param name="logger">The logger.</param>
    public DefaultTelephonyProviderResolver(
        IOptionsMonitor<TelephonySettings> settings,
        IOptionsMonitor<TelephonyProviderOptions> providerOptions,
        IServiceProvider serviceProvider,
        ILogger<DefaultTelephonyProviderResolver> logger)
    {
        _settings = settings;
        _serviceProvider = serviceProvider;
        _providerOptions = providerOptions;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ITelephonyProvider> GetAsync(string name = null)
    {
        if (string.IsNullOrEmpty(name))
        {
            name = _settings.CurrentValue.DefaultProviderName;
        }

        if (string.IsNullOrEmpty(name))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("No default telephony provider is configured.");
            }

            return null;
        }

        if (_providerOptions.CurrentValue.Providers.TryGetValue(name, out var providerType) && providerType.IsEnabled)
        {
            return (ITelephonyProvider)ActivatorUtilities.CreateInstance(_serviceProvider, providerType.Type);
        }

        if (_logger.IsEnabled(LogLevel.Error))
        {
            _logger.LogError("No telephony provider is registered or enabled to match the given name {Name}.", name);
        }

        return null;
    }
}
