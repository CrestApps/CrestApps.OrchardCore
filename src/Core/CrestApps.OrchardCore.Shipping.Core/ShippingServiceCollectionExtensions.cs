using CrestApps.Core;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Shipping.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Shipping;

public static class ShippingServiceCollectionExtensions
{
    /// <summary>
    /// Adds the shipping zones catalog, the rate service, and the flat, table and free rate providers.
    /// </summary>
    public static IServiceCollection AddShippingCore(this IServiceCollection services)
    {
        services
            .AddCatalogs()
            .AddCatalogManagers();

        services.TryAddScoped<IShippingRateService, DefaultShippingRateService>();
        services.AddScoped<IShippingRateProvider, FlatRateShippingProvider>();
        services.AddScoped<IShippingRateProvider, TableRateShippingProvider>();
        services.AddScoped<IShippingRateProvider, FreeShippingProvider>();

        return services;
    }
}
