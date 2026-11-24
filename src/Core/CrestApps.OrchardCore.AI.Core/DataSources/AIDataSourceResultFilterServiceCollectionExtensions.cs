using CrestApps.Core.Infrastructure.Indexing.DataSources;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core.DataSources;

/// <summary>
/// Registers data source result filters and wraps the providers' content managers so the filters run.
/// </summary>
public static class AIDataSourceResultFilterServiceCollectionExtensions
{
    /// <summary>
    /// Adds a result filter that every data source search passes its chunks through.
    /// </summary>
    /// <typeparam name="TFilter">The filter.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddAIDataSourceResultFilter<TFilter>(this IServiceCollection services)
        where TFilter : class, IAIDataSourceResultFilter
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAIDataSourceResultFilter, TFilter>());

        return services;
    }

    /// <summary>
    /// Wraps every keyed <see cref="IDataSourceContentManager"/> registered so far in a
    /// <see cref="FilteringDataSourceContentManager"/>, keeping its key and lifetime. A content manager that is already
    /// wrapped is left alone, so calling this twice is harmless. Call it after the providers registered theirs.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection DecorateDataSourceContentManagers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        for (var index = 0; index < services.Count; index++)
        {
            var descriptor = services[index];

            if (descriptor.ServiceType != typeof(IDataSourceContentManager) ||
                !descriptor.IsKeyedService ||
                descriptor.KeyedImplementationFactory?.Target is DecoratedContentManagerFactory)
            {
                continue;
            }

            var factory = new DecoratedContentManagerFactory(descriptor);

            services[index] = new ServiceDescriptor(typeof(IDataSourceContentManager), descriptor.ServiceKey, factory.Create, descriptor.Lifetime);
        }

        return services;
    }

    private sealed class DecoratedContentManagerFactory
    {
        private readonly ServiceDescriptor _inner;

        public DecoratedContentManagerFactory(ServiceDescriptor inner)
        {
            _inner = inner;
        }

        public FilteringDataSourceContentManager Create(IServiceProvider serviceProvider, object key)
        {
            var inner = _inner.KeyedImplementationInstance as IDataSourceContentManager
                ?? (_inner.KeyedImplementationFactory is not null
                    ? (IDataSourceContentManager)_inner.KeyedImplementationFactory(serviceProvider, key)
                    : (IDataSourceContentManager)ActivatorUtilities.CreateInstance(serviceProvider, _inner.KeyedImplementationType));

            return new FilteringDataSourceContentManager(
                inner,
                serviceProvider.GetServices<IAIDataSourceResultFilter>(),
                serviceProvider.GetService<IHttpContextAccessor>(),
                serviceProvider.GetService<IOptions<AIDataSourceResultFilterOptions>>()?.Value);
        }
    }
}
