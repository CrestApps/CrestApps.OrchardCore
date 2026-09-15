using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default resolver for Contact Center voice providers.
/// </summary>
public sealed class ContactCenterVoiceProviderResolver : IContactCenterVoiceProviderResolver
{
    private readonly IEnumerable<IContactCenterVoiceProvider> _providers;
    private readonly IOptionsSnapshot<TelephonySettings> _telephonySettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterVoiceProviderResolver"/> class.
    /// </summary>
    /// <param name="providers">The registered voice providers.</param>
    /// <param name="telephonySettings">The tenant telephony settings used to resolve the configured default provider.</param>
    public ContactCenterVoiceProviderResolver(
        IEnumerable<IContactCenterVoiceProvider> providers,
        IOptionsSnapshot<TelephonySettings> telephonySettings)
    {
        _providers = providers;
        _telephonySettings = telephonySettings;
    }

    /// <inheritdoc/>
    public IContactCenterVoiceProvider Get(string technicalName = null)
    {
        if (string.IsNullOrEmpty(technicalName))
        {
            // No explicit provider was requested (for example, a dialer profile that does not pin a provider, or
            // a caller that asks for "the current provider"). Prefer the tenant's configured default telephony
            // provider so that, when more than one provider module participates in Contact Center voice at once,
            // the provider the operator selected is used rather than whichever adapter happened to register
            // first. A provider's Contact Center TechnicalName matches its telephony provider technical name,
            // which is exactly what the default records. Fall back to the first registered provider only when no
            // default is configured, or the configured default does not itself register a voice adapter.
            var defaultProviderName = _telephonySettings.Value?.DefaultProviderName;

            if (!string.IsNullOrEmpty(defaultProviderName))
            {
                var configuredProvider = _providers.FirstOrDefault(provider =>
                    string.Equals(provider.TechnicalName, defaultProviderName, StringComparison.OrdinalIgnoreCase));

                if (configuredProvider is not null)
                {
                    return configuredProvider;
                }
            }

            return _providers.FirstOrDefault();
        }

        return _providers.FirstOrDefault(provider => string.Equals(provider.TechnicalName, technicalName, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public IEnumerable<IContactCenterVoiceProvider> GetAll()
    {
        return _providers;
    }
}
