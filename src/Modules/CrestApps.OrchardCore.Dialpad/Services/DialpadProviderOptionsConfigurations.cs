using CrestApps.OrchardCore.Dialpad.Models;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Dialpad.Services;

/// <summary>
/// Registers the Dialpad provider with the telephony provider options and reflects whether it is
/// enabled based on the current tenant settings.
/// </summary>
public sealed class DialpadProviderOptionsConfigurations : IConfigureOptions<TelephonyProviderOptions>
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialpadProviderOptionsConfigurations"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read Dialpad settings.</param>
    public DialpadProviderOptionsConfigurations(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public void Configure(TelephonyProviderOptions options)
    {
        var settings = _siteService.GetSettings<DialpadSettings>();

        var typeOptions = new TelephonyProviderTypeOptions(typeof(DialpadTelephonyProvider))
        {
            IsEnabled = settings.IsEnabled,
        };

        options.TryAddProvider(DialpadConstants.ProviderTechnicalName, typeOptions);
    }
}
