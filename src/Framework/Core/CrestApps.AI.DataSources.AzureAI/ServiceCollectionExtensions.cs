using Azure.Search.Documents.Indexes;
using CrestApps.AI;
using CrestApps.AI.DataSources.AzureAI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.AI.DataSources.AzureAI;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Azure AI Search data source services.
    /// Requires a <see cref="SearchIndexClient"/> to be registered in the service container.
    /// </summary>
    public static IServiceCollection AddAzureAISearchDataSourceServices(this IServiceCollection services)
    {
        services.TryAddKeyedScoped<IDataSourceContentManager>(
            "AzureAISearch",
            (sp, _) => new AzureAISearchDataSourceContentManager(
                sp.GetRequiredService<SearchIndexClient>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AzureAISearchDataSourceContentManager>>()));

        services.TryAddKeyedScoped<IDataSourceDocumentReader>(
            "AzureAISearch",
            (sp, _) => new DataSourceAzureAISearchDocumentReader(
                sp.GetRequiredService<SearchIndexClient>()));

        services.TryAddKeyedSingleton<IODataFilterTranslator>(
            "AzureAISearch",
            (_, _) => new AzureAIODataFilterTranslator());

        return services;
    }
}
