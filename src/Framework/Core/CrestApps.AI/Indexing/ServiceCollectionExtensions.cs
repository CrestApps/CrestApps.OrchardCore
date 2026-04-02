using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CrestApps.Infrastructure.Indexing;

namespace CrestApps.AI.Indexing;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAIDocumentIndexProfileHandler(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IIndexProfileHandler, AIDocumentSearchIndexProfileHandler>());

        return services;
    }

    public static IServiceCollection AddDataSourceIndexProfileHandler(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IIndexProfileHandler, DataSourceSearchIndexProfileHandler>());

        return services;
    }

    public static IServiceCollection AddAIMemoryIndexProfileHandler(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IIndexProfileHandler, AIMemorySearchIndexProfileHandler>());

        return services;
    }

    public static IServiceCollection AddDefaultIndexProfileHandler(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IIndexProfileHandler, DefaultSearchIndexProfileHandler>());

        return services;
    }
}
