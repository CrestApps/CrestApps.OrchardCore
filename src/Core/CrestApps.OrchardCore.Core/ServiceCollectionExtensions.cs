using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreModelServices(this IServiceCollection services)
    {
        services
            .AddCoreModelStores()
            .AddScoped(typeof(ICatalogManager<>), typeof(CatalogManager<>))
            .AddScoped(typeof(INamedCatalogManager<>), typeof(NamedCatalogManager<>))
            .AddScoped(typeof(ISourceCatalogManager<>), typeof(SourceCatalogManager<>))
            .AddScoped(typeof(INamedSourceCatalogManager<>), typeof(NamedSourceCatalogManager<>));

        return services;
    }

    public static IServiceCollection AddCoreModelStores(this IServiceCollection services)
    {
        services
            .AddScoped(typeof(ICatalog<>), typeof(Catalog<>))
            .AddScoped(typeof(INamedCatalog<>), typeof(NamedCatalog<>))
            .AddScoped(typeof(ISourceCatalog<>), typeof(SourceCatalog<>))
            .AddScoped(typeof(INamedSourceCatalog<>), typeof(NamedSourceCatalog<>));

        return services;
    }
}
