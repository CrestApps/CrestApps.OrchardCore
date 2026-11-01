using CrestApps.Core.Services;
using CrestApps.OrchardCore.Geo;
using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.Workforce.Geo;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// Geocodes team locations and workers' home bases when Geo is on.
/// </summary>
[RequireFeatures(GeoConstants.Features.Geo)]
public sealed class GeoStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWorkforceGeocoding();
    }
}

/// <summary>
/// Registers the Workforce geocoding handlers and targets.
/// </summary>
public static class WorkforceGeocodingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the handlers that ask for team locations and home bases to be geocoded, and the targets that write
    /// the coordinates back.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddWorkforceGeocoding(this IServiceCollection services)
    {
        // The queue's targets need the team and worker managers, whose handlers need the queue: the handlers take it
        // lazily to break the cycle.
        services.TryAddScoped(sp => new Lazy<IGeocodeQueue>(sp.GetRequiredService<IGeocodeQueue>));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<WorkforceTeam>, TeamLocationGeocodeHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<WorkerProfile>, WorkerHomeBaseGeocodeHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IGeocodeTarget, TeamLocationGeocodeTarget>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IGeocodeTarget, WorkerHomeBaseGeocodeTarget>());

        return services;
    }
}
