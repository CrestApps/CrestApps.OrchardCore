using CrestApps.Data.YesSql.Indexes;
using CrestApps.Data.YesSql.Services;
using CrestApps.Models;
using CrestApps.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using YesSql;

namespace CrestApps.Data.YesSql;

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

        services.AddScoped<INamedCatalog<TModel>>(sp =>
        (INamedCatalog<TModel>)sp.GetRequiredService<ICatalog<TModel>>());

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

        services.AddScoped<ISourceCatalog<TModel>>(sp =>
        (ISourceCatalog<TModel>)sp.GetRequiredService<ICatalog<TModel>>());

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

        services.AddScoped<INamedCatalog<TModel>>(sp =>
        (INamedCatalog<TModel>)sp.GetRequiredService<ICatalog<TModel>>());

        services.AddScoped<ISourceCatalog<TModel>>(sp =>
        (ISourceCatalog<TModel>)sp.GetRequiredService<ICatalog<TModel>>());

        services.AddScoped<INamedSourceCatalog<TModel>>(sp =>
        (INamedSourceCatalog<TModel>)sp.GetRequiredService<ICatalog<TModel>>());

        return services;
    }
}
