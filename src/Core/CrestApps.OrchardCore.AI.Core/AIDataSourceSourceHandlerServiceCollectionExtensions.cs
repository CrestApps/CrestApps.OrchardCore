using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Registers the handler of a data source's source.
/// </summary>
public static class AIDataSourceSourceHandlerServiceCollectionExtensions
{
    /// <summary>
    /// Registers a source handler under its source type, where indexing resolves it, and in the list of every source
    /// handler, where the catalog's validation finds it by its <see cref="IAIDataSourceSourceHandler.SourceType"/>.
    /// Both resolve the same instance in a scope. The copies of the data sources read in a request, which the handlers
    /// compare a save with, are registered once.
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
        services.TryAddScoped<AIDataSourceStoredSettings>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<AIDataSource>, AIDataSourceStoredSettingsHandler>());

        return services;
    }
}
