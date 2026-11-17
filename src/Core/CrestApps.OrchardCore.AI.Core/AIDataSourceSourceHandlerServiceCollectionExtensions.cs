using CrestApps.Core.AI.DataSources;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Registers the handler of a data source's source.
/// </summary>
public static class AIDataSourceSourceHandlerServiceCollectionExtensions
{
    /// <summary>
    /// Registers a source handler under its source type, where indexing resolves it, and in the list of every source
    /// handler, where the catalog's validation finds it by its <see cref="IAIDataSourceSourceHandler.SourceType"/>.
    /// Both resolve the same instance in a scope.
    /// </summary>
    /// <typeparam name="THandler">The source handler.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="sourceType">The source type, such as <c>AzureAISearch</c>.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAIDataSourceSourceHandler<THandler>(this IServiceCollection services, string sourceType)
        where THandler : class, IAIDataSourceSourceHandler
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(sourceType);

        services.AddKeyedScoped<IAIDataSourceSourceHandler, THandler>(sourceType);
        services.AddScoped(serviceProvider => serviceProvider.GetRequiredKeyedService<IAIDataSourceSourceHandler>(sourceType));

        return services;
    }
}
