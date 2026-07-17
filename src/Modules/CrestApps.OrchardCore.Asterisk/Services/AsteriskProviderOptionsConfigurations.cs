using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Registers the tenant-configured and configuration-backed Asterisk providers with the telephony provider options.
/// </summary>
public sealed class AsteriskProviderOptionsConfigurations : IConfigureOptions<TelephonyProviderOptions>
{
    private readonly ISiteService _siteService;
    private readonly DefaultAsteriskOptions _defaultOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskProviderOptionsConfigurations"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read tenant-configured Asterisk settings.</param>
    /// <param name="defaultOptions">The configuration-backed default Asterisk options.</param>
    public AsteriskProviderOptionsConfigurations(
        ISiteService siteService,
        IOptions<DefaultAsteriskOptions> defaultOptions)
    {
        _siteService = siteService;
        _defaultOptions = defaultOptions.Value;
    }

    /// <inheritdoc/>
    public void Configure(TelephonyProviderOptions options)
    {
        ConfigureTenantProvider(options);

        if (_defaultOptions.IsEnabled)
        {
            ConfigureDefaultProvider(options);
        }
    }

    private void ConfigureTenantProvider(TelephonyProviderOptions options)
    {
        var settings = _siteService.GetSettings<AsteriskSettings>();

        var typeOptions = new TelephonyProviderTypeOptions(typeof(AsteriskTelephonyProvider))
        {
            IsEnabled = settings.IsEnabled && AsteriskSettingsUtilities.HasRequiredConfiguration(settings, settings.Password),
        };

        options.TryAddProvider(AsteriskConstants.ProviderTechnicalName, typeOptions);
    }

    private static void ConfigureDefaultProvider(TelephonyProviderOptions options)
    {
        var typeOptions = new TelephonyProviderTypeOptions(typeof(DefaultAsteriskTelephonyProvider))
        {
            IsEnabled = true,
        };

        options.TryAddProvider(AsteriskConstants.DefaultProviderTechnicalName, typeOptions);
    }
}
