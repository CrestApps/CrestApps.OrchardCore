using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.SiteResources.Models;
using CrestApps.OrchardCore.SiteResources.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.SiteResources;

/// <summary>
/// Registers the site resources services.
/// </summary>
public static class SiteResourcesServiceCollectionExtensions
{
    /// <summary>
    /// Adds the site resources catalog (kept in one cached document), its manager, the validator and the service that
    /// works out the resources a page gets.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddSiteResourcesCore(this IServiceCollection services)
    {
        services
            .AddCatalogs()
            .AddCatalogManagers();

        // The admin pages, recipes and deployments go through the named manager, which keeps names unique.
        services.TryAddScoped<ICatalogManager<SiteResource>>(serviceProvider => serviceProvider.GetRequiredService<INamedCatalogManager<SiteResource>>());
        services.TryAddScoped<ISiteResourceService, SiteResourceService>();
        services.TryAddScoped<SiteResourceValidator>();

        return services;
    }
}
