using CrestApps.OrchardCore.Shipping.Models;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Shipping.Services;

public sealed class DefaultCarrierProviderResolver : ICarrierProviderResolver
{
    private readonly IEnumerable<ICarrierProvider> _providers;
    private readonly ISiteService _siteService;

    public DefaultCarrierProviderResolver(IEnumerable<ICarrierProvider> providers, ISiteService siteService)
    {
        _providers = providers;
        _siteService = siteService;
    }

    public async Task<ICarrierProvider> GetDefaultAsync()
    {
        var name = (await _siteService.GetSettingsAsync<CarrierProviderSettings>()).DefaultProviderName;

        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var provider = _providers.FirstOrDefault(provider => string.Equals(provider.Name, name, StringComparison.OrdinalIgnoreCase));

        return provider is not null && await provider.IsEnabledAsync() ? provider : null;
    }

    public async Task<IReadOnlyList<ICarrierProvider>> GetEnabledAsync()
    {
        var enabled = new List<ICarrierProvider>();

        foreach (var provider in _providers)
        {
            if (await provider.IsEnabledAsync())
            {
                enabled.Add(provider);
            }
        }

        return enabled;
    }
}
