using CrestApps.OrchardCore.DialPad.Models;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.DialPad.Services;

/// <summary>
/// Registers the DialPad provider with the telephony provider options and reflects whether it is
/// enabled based on the current tenant settings.
/// </summary>
public sealed class DialPadProviderOptionsConfigurations : IConfigureOptions<TelephonyProviderOptions>
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialPadProviderOptionsConfigurations"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read DialPad settings.</param>
    public DialPadProviderOptionsConfigurations(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public void Configure(TelephonyProviderOptions options)
    {
        var settings = _siteService.GetSettings<DialPadSettings>();

        var typeOptions = new TelephonyProviderTypeOptions(typeof(DialPadTelephonyProvider))
        {
            IsEnabled = settings.IsEnabled,
        };

        options.TryAddProvider(DialPadConstants.ProviderTechnicalName, typeOptions);
    }
}
