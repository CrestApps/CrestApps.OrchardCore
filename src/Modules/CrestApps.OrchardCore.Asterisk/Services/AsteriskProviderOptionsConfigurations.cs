using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Registers the tenant-configured and configuration-backed Asterisk providers with the telephony provider options.
/// </summary>
public sealed class AsteriskProviderOptionsConfigurations : IConfigureOptions<TelephonyProviderOptions>
{
    private readonly ISiteService _siteService;
    private readonly DefaultAsteriskOptions _defaultOptions;
    private readonly ShellSettings _shellSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskProviderOptionsConfigurations"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read tenant-configured Asterisk settings.</param>
    /// <param name="defaultOptions">The configuration-backed default Asterisk options.</param>
    /// <param name="shellSettings">The current tenant shell settings used to scope the host-default fallback.</param>
    public AsteriskProviderOptionsConfigurations(
        ISiteService siteService,
        IOptions<DefaultAsteriskOptions> defaultOptions,
        ShellSettings shellSettings)
    {
        _siteService = siteService;
        _defaultOptions = defaultOptions.Value;
        _shellSettings = shellSettings;
    }

    /// <inheritdoc/>
    public void Configure(TelephonyProviderOptions options)
    {
        ConfigureTenantProvider(options);

        // Only the default shell may expose the host-level default provider; otherwise a non-default tenant could
        // originate into the shared host ARI application and cross tenant boundaries.
        if (_defaultOptions.IsEnabled && _shellSettings.IsDefaultShell())
        {
            ConfigureDefaultProvider(options);
        }
    }

    private void ConfigureTenantProvider(TelephonyProviderOptions options)
    {
        var settings = _siteService.GetSettings<AsteriskSettings>();
        var collidesWithHostDefault = !_shellSettings.IsDefaultShell() &&
            AsteriskSettingsUtilities.CollidesWithHostDefaultApplication(
                settings.BaseUrl,
                settings.ApplicationName,
                _defaultOptions);

        var typeOptions = new TelephonyProviderTypeOptions(typeof(AsteriskTelephonyProvider))
        {
            IsEnabled = settings.IsEnabled &&
                AsteriskSettingsUtilities.HasRequiredConfiguration(settings, settings.Password) &&
                !collidesWithHostDefault,
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
