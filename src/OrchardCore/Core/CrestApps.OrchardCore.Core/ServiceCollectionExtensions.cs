using CrestApps.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogManagers(this IServiceCollection services)
    {
        services.TryAddScoped(typeof(ICatalogManager<>), typeof(CatalogManager<>));
        services.TryAddScoped(typeof(INamedCatalogManager<>), typeof(NamedCatalogManager<>));
        services.TryAddScoped(typeof(ISourceCatalogManager<>), typeof(SourceCatalogManager<>));
        services.TryAddScoped(typeof(INamedSourceCatalogManager<>), typeof(NamedSourceCatalogManager<>));

        return services;
    }

    public static IServiceCollection AddCatalogs(this IServiceCollection services)
    {
        services.TryAddScoped(typeof(ICatalog<>), typeof(CrestApps.OrchardCore.Core.Services.Catalog<>));
        services.TryAddScoped(typeof(INamedCatalog<>), typeof(CrestApps.OrchardCore.Core.Services.NamedCatalog<>));
        services.TryAddScoped(typeof(ISourceCatalog<>), typeof(CrestApps.OrchardCore.Core.Services.SourceCatalog<>));
        services.TryAddScoped(typeof(INamedSourceCatalog<>), typeof(CrestApps.OrchardCore.Core.Services.NamedSourceCatalog<>));

        return services;
    }
}
