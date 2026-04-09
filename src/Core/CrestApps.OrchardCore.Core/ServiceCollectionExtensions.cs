using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogs(this IServiceCollection services)
    {
        services.TryAddScoped(typeof(ICatalog<>), typeof(Catalog<>));
        services.TryAddScoped(typeof(INamedCatalog<>), typeof(NamedCatalog<>));
        services.TryAddScoped(typeof(ISourceCatalog<>), typeof(SourceCatalog<>));
        services.TryAddScoped(typeof(INamedSourceCatalog<>), typeof(NamedSourceCatalog<>));

        return services;
    }

    public static IServiceCollection AddCatalog<TModel>(this IServiceCollection services)
        where TModel : CatalogItem
    {
        services.AddScoped<ICatalog<TModel>, Catalog<TModel>>();

        return services;
    }

    public static IServiceCollection AddNamedCatalog<TModel>(this IServiceCollection services)
        where TModel : CatalogItem, INameAwareModel
    {
        services.AddScoped<ICatalog<TModel>, NamedCatalog<TModel>>();

        return services;
    }

    public static IServiceCollection AddSourceCatalog<TModel>(this IServiceCollection services)
        where TModel : CatalogItem, ISourceAwareModel
    {
        services.AddScoped<ISourceCatalog<TModel>, SourceCatalog<TModel>>();

        return services;
    }

    public static IServiceCollection AddNamedSourceCatalog<TModel>(this IServiceCollection services)
        where TModel : CatalogItem, INameAwareModel, ISourceAwareModel
    {
        services.AddScoped<INamedSourceCatalog<TModel>, NamedSourceCatalog<TModel>>();

        return services;
    }
}
