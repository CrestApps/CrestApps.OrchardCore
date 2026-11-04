using CrestApps.OrchardCore.Commerce.Core.Stores;
using CrestApps.OrchardCore.Commerce.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Commerce.Core;

/// <summary>
/// Registers the store's identity and places.
/// </summary>
public static class StoreLocationServiceCollectionExtensions
{
    /// <summary>
    /// Adds the <see cref="IStoreLocationService"/>. A feature that adds places the store works from registers an
    /// <see cref="IStoreLocationSource"/>.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddStoreLocations(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IStoreLocationService, DefaultStoreLocationService>();

        return services;
    }
}
