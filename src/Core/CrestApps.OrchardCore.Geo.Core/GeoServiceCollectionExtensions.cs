using CrestApps.Core.Services;
using CrestApps.OrchardCore.Geo.Core.Services;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the Geo services.
/// </summary>
public static class GeoServiceCollectionExtensions
{
    /// <summary>
    /// Registers the geo service, the built-in travel estimate, territories and the geocoding queue.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddGeoCore(this IServiceCollection services)
    {
        services.TryAddScoped<IGeoSettingsService, SiteGeoSettingsService>();
        services.TryAddScoped<HaversineTravelTimeProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITravelTimeProvider, HaversineTravelTimeProvider>(sp => sp.GetRequiredService<HaversineTravelTimeProvider>()));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IRouteProvider, HaversineTravelTimeProvider>(sp => sp.GetRequiredService<HaversineTravelTimeProvider>()));
        services.TryAddScoped<IGeoService, DefaultGeoService>();

        services.TryAddScoped<ServiceTerritoryStore>();
        services.TryAddScoped<ServiceTerritoryManager>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<ServiceTerritory>, ServiceTerritoryHandler>());
        services.TryAddScoped<ITerritoryResolver, DefaultTerritoryResolver>();

        services.TryAddScoped<GeocodeRequestStore>();
        services.TryAddScoped<IGeocodeQueue, DefaultGeocodeQueue>();

        return services;
    }
}
