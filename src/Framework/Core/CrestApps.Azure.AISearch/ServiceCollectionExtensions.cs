using Azure;
using Azure.Identity;
using Azure.Search.Documents.Indexes;
using CrestApps.AI;
using CrestApps.AI.Indexing;
using CrestApps.AI.Memory;
using CrestApps.Azure.AISearch.Services;
using CrestApps.Infrastructure.Indexing;
using CrestApps.Infrastructure.Indexing.DataSources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.Azure.AISearch;

public static class ServiceCollectionExtensions
{

    public const string ProviderName = "AzureAISearch";

    public static IServiceCollection AddAzureAISearchServices(
        this IServiceCollection services,
        IConfigurationSection configuration)
    {
        services.Configure<AzureAISearchConnectionOptions>(configuration);

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
            sp.GetRequiredService<SearchIndexClient>(), sp.GetRequiredService<ILogger<AzureAISearchDataSourceContentManager>>()));

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
            sp.GetRequiredService<IOptions<AzureAISearchConnectionOptions>>(), sp.GetRequiredService<ILogger<AzureAISearchIndexManager>>()));

        services.TryAddKeyedScoped<ISearchDocumentManager>(
            ProviderName,
            (sp, _) => new AzureAISearchDocumentManager(
            sp.GetRequiredService<SearchIndexClient>(), sp.GetRequiredService<ILogger<AzureAISearchDocumentManager>>()));

        services.TryAddKeyedScoped<IVectorSearchService>(
            ProviderName,
            (sp, _) => new AzureAISearchVectorSearchService(
            sp.GetRequiredService<SearchIndexClient>(), sp.GetRequiredService<ILogger<AzureAISearchVectorSearchService>>()));

        services.TryAddKeyedScoped<IMemoryVectorSearchService>(
            ProviderName,
            (sp, _) => new AzureAISearchMemoryVectorSearchService(
                sp.GetRequiredService<SearchIndexClient>(),
                sp.GetRequiredService<ILogger<AzureAISearchMemoryVectorSearchService>>()));

        return services;
    }

    public static IServiceCollection AddAzureAISearchDataSource(
        this IServiceCollection services,
        string type,
        Action<IndexProfileSourceDescriptor> configure = null)
    {
        services.AddDefaultIndexProfileHandler();
        services.Configure<IndexProfileSourceOptions>(options =>
            options.AddOrUpdate(ProviderName, "Azure AI Search", type, configure));

        return services;
    }

    public static IServiceCollection AddAzureAISearchAIDocumentSource(this IServiceCollection services)
        => services
            .AddAzureAISearchDataSource(IndexProfileTypes.AIDocuments, descriptor =>
            {
                descriptor.DisplayName = "AI Documents";
                descriptor.Description = "Create an Azure AI Search index for uploaded and embedded AI document chunks.";
            })
            .AddAIDocumentIndexProfileHandler();

    public static IServiceCollection AddAzureAISearchAIDataSource(this IServiceCollection services)
        => services
            .AddAzureAISearchDataSource(IndexProfileTypes.DataSource, descriptor =>
            {
                descriptor.DisplayName = "Data Source";
                descriptor.Description = "Create an Azure AI Search index for AI knowledge base data source documents.";
            })
            .AddDataSourceRagServices()
            .AddDataSourceIndexProfileHandler();

    public static IServiceCollection AddAzureAISearchAIMemorySource(this IServiceCollection services)
        => services
            .AddAzureAISearchDataSource(IndexProfileTypes.AIMemory, descriptor =>
            {
                descriptor.DisplayName = "AI Memory";
                descriptor.Description = "Create an Azure AI Search index for user and system memory records.";
            })
            .AddAIMemoryIndexProfileHandler();
}
