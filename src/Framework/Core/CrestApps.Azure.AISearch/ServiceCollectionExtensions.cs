using Azure;
using Azure.Identity;
using Azure.Search.Documents.Indexes;
using CrestApps.Azure.AISearch.Services;
using CrestApps.Infrastructure.Indexing;
using CrestApps.Infrastructure.Indexing.DataSources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CrestApps.Azure.AISearch;

public static class ServiceCollectionExtensions
{
    public const string ProviderName = "AzureAISearch";
    public static IServiceCollection AddAzureAISearchServices(
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

        return services.AddAzureAISearchServices();
    }

    public static IServiceCollection AddAzureAISearchServices(this IServiceCollection services)
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
