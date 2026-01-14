using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using CrestApps.OrchardCore.YesSql.Core.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using YesSql;

namespace CrestApps.OrchardCore.YesSql.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDocumentCatalogs(this IServiceCollection services)
    {
        services.TryAddScoped(typeof(ICatalog<>), typeof(DocumentCatalog<,>));
        services.TryAddScoped(typeof(INamedCatalog<>), typeof(NamedDocumentCatalog<,>));
        services.TryAddScoped(typeof(ISourceCatalog<>), typeof(SourceDocumentCatalog<,>));
        services.TryAddScoped(typeof(INamedSourceCatalog<>), typeof(NamedSourceDocumentCatalog<,>));

        return services;
    }

    public static IServiceCollection AddDocumentCatalog<TModel, TIndex>(this IServiceCollection services, string collection = null)
        where TModel : CatalogItem
        where TIndex : CatalogItemIndex
    {
        services.AddScoped<ICatalog<TModel>>(sp =>
        {
            var session = sp.GetRequiredService<ISession>();

            return new DocumentCatalog<TModel, TIndex>(session, collection);
        });

        return services;
    }

    public static IServiceCollection AddNamedDocumentCatalog<TModel, TIndex>(this IServiceCollection services, string collection = null)
        where TModel : CatalogItem, INameAwareModel
        where TIndex : CatalogItemIndex, INameAwareIndex
    {
        services.AddScoped<ICatalog<TModel>>(sp =>
        {
            var session = sp.GetRequiredService<ISession>();

            return new NamedDocumentCatalog<TModel, TIndex>(session, collection);
        });

        return services;
    }

    public static IServiceCollection AddSourceDocumentCatalog<TModel, TIndex>(this IServiceCollection services, string collection = null)
        where TModel : CatalogItem, ISourceAwareModel
        where TIndex : CatalogItemIndex, ISourceAwareIndex
    {
        services.AddScoped<ICatalog<TModel>>(sp =>
        {
            var session = sp.GetRequiredService<ISession>();

            return new SourceDocumentCatalog<TModel, TIndex>(session, collection);
        });

        return services;
    }

    public static IServiceCollection AddNamedSourceDocumentCatalog<TModel, TIndex>(this IServiceCollection services, string collection = null)
        where TModel : CatalogItem, INameAwareModel, ISourceAwareModel
        where TIndex : CatalogItemIndex, INameAwareIndex, ISourceAwareIndex
    {
        services.AddScoped<ICatalog<TModel>>(sp =>
        {
            var session = sp.GetRequiredService<ISession>();

            return new NamedSourceDocumentCatalog<TModel, TIndex>(session, collection);
        });

        return services;
    }
}
