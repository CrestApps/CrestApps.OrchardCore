using CrestApps.OrchardCore.Addresses.Core.Services;
using CrestApps.OrchardCore.Addresses.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Addresses.Core;

public static class AddressesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the address formats of every country, and the checks and printing built on them. Countries come from
    /// the ISO list unless a module registers its own <see cref="ICountryService"/>.
    /// </summary>
    public static IServiceCollection AddAddressesCore(this IServiceCollection services)
    {
        services.TryAddSingleton<IAddressFormatProvider, DefaultAddressFormatProvider>();
        services.TryAddScoped<IAddressValidator, DefaultAddressValidator>();
        services.TryAddScoped<IAddressFormatter, DefaultAddressFormatter>();
        services.TryAddScoped<ICountryService, DefaultCountryService>();

        return services;
    }
}
