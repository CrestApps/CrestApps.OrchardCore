using Azure;
using Azure.Identity;
using Azure.Search.Documents.Indexes;
using CrestApps.AI;
using CrestApps.AI.DataSources.AzureAI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CrestApps.AI.DataSources.AzureAI;

public static class ServiceCollectionExtensions
{
    public const string ProviderName = "AzureAISearch";

    /// <summary>
    /// Adds Azure AI Search data source services and registers a <see cref="SearchIndexClient"/>
    /// from the given configuration section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">
    /// A configuration section that binds to <see cref="AzureAISearchConnectionOptions"/>
    /// (e.g. "CrestApps:Search:AzureAISearch").
    /// </param>
    public static IServiceCollection AddAzureAISearchDataSourceServices(
        this IServiceCollection services,
        IConfigurationSection configuration)
    {
        var options = new AzureAISearchConnectionOptions();
        configuration.Bind(options);

        if (!string.IsNullOrEmpty(options.Endpoint))
        {
            var endpoint = new Uri(options.Endpoint);

            if (!string.IsNullOrEmpty(options.ApiKey))
            {
                services.TryAddSingleton(new SearchIndexClient(endpoint, new AzureKeyCredential(options.ApiKey)));
            }
            else
            {
                services.TryAddSingleton(new SearchIndexClient(endpoint, new DefaultAzureCredential()));
            }
        }

        return services.AddAzureAISearchDataSourceServices();
    }

    /// <summary>
    /// Adds Azure AI Search data source and index management services.
    /// Requires a <see cref="SearchIndexClient"/> to be registered in the service container.
    /// </summary>
    public static IServiceCollection AddAzureAISearchDataSourceServices(this IServiceCollection services)
    {
        services.TryAddKeyedScoped<IDataSourceContentManager>(
            ProviderName,
            (sp, _) => new AzureAISearchDataSourceContentManager(
                sp.GetRequiredService<SearchIndexClient>(),
                sp.GetRequiredService<ILogger<AzureAISearchDataSourceContentManager>>()));

        services.TryAddKeyedScoped<IDataSourceDocumentReader>(
            ProviderName,
            (sp, _) => new DataSourceAzureAISearchDocumentReader(
                sp.GetRequiredService<SearchIndexClient>()));

        services.TryAddKeyedSingleton<IODataFilterTranslator>(
            ProviderName,
            (_, _) => new AzureAIODataFilterTranslator());

        services.TryAddKeyedScoped<ISearchIndexManager>(
            ProviderName,
            (sp, _) => new AzureAISearchIndexManager(
                sp.GetRequiredService<SearchIndexClient>(),
                sp.GetRequiredService<ILogger<AzureAISearchIndexManager>>()));

        services.TryAddKeyedScoped<ISearchDocumentManager>(
            ProviderName,
            (sp, _) => new AzureAISearchDocumentManager(
                sp.GetRequiredService<SearchIndexClient>(),
                sp.GetRequiredService<ILogger<AzureAISearchDocumentManager>>()));

        services.TryAddKeyedScoped<IVectorSearchService>(
            ProviderName,
            (sp, _) => new AzureAISearchVectorSearchService(
                sp.GetRequiredService<SearchIndexClient>(),
                sp.GetRequiredService<ILogger<AzureAISearchVectorSearchService>>()));

        return services;
    }
}
