using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Applies the persisted <see cref="TelephonySettings"/> to the options resolved through the options system.
/// </summary>
public sealed class TelephonySettingsConfiguration : IPostConfigureOptions<TelephonySettings>
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonySettingsConfiguration"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read the persisted settings.</param>
    public TelephonySettingsConfiguration(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public void PostConfigure(string name, TelephonySettings options)
    {
        var settings = _siteService.GetSettings<TelephonySettings>();

        options.DefaultProviderName = settings.DefaultProviderName;
    }
}
