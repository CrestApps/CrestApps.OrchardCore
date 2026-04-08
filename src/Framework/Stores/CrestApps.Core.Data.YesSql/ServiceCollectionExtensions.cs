using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Data.YesSql.Services;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using YesSql;

namespace CrestApps.Core.Data.YesSql;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddYesSqlDataStore(this IServiceCollection services, Func<Configuration, IConfiguration> configure)
    {
        services.AddSingleton<IStore>(_ => StoreFactory.CreateAndInitializeAsync(configure(new Configuration()))
            .GetAwaiter()
            .GetResult());

        services.AddScoped(sp => sp.GetRequiredService<IStore>().CreateSession());

        return services;
    }

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
        services.RemoveAll<ICatalog<TModel>>();
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
        services.RemoveAll<ICatalog<TModel>>();
        services.RemoveAll<INamedCatalog<TModel>>();

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
        services.RemoveAll<ICatalog<TModel>>();
        services.RemoveAll<ISourceCatalog<TModel>>();

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
        services.RemoveAll<ICatalog<TModel>>();
        services.RemoveAll<INamedCatalog<TModel>>();
        services.RemoveAll<ISourceCatalog<TModel>>();
        services.RemoveAll<INamedSourceCatalog<TModel>>();

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
