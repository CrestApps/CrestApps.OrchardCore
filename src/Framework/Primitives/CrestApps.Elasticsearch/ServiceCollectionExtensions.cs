using CrestApps.AI;
using CrestApps.AI.Indexing;
using CrestApps.AI.Memory;
using CrestApps.Elasticsearch.Services;
using CrestApps.Infrastructure.Indexing;
using CrestApps.Infrastructure.Indexing.DataSources;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.Elasticsearch;

public static class ServiceCollectionExtensions
{

    public const string ProviderName = "Elasticsearch";

    public static IServiceCollection AddElasticsearchServices(
        this IServiceCollection services,
        IConfigurationSection configuration)
    {
        services.Configure<ElasticsearchConnectionOptions>(configuration);

        var options = new ElasticsearchConnectionOptions();

        configuration.Bind(options);

        if (!string.IsNullOrEmpty(options.Url))
        {

            var settings = new ElasticsearchClientSettings(new Uri(options.Url));

            if (!string.IsNullOrEmpty(options.Username) && !string.IsNullOrEmpty(options.Password))
            {
                settings.Authentication(
                    new Elastic.Transport.BasicAuthentication(options.Username, options.Password));

            }

            if (!string.IsNullOrEmpty(options.CertificateFingerprint))
            {
                settings.CertificateFingerprint(options.CertificateFingerprint);

            }

            services.TryAddSingleton(new ElasticsearchClient(settings));

        }

        return services.AddElasticsearchServices();

    }

    public static IServiceCollection AddElasticsearchServices(this IServiceCollection services)
    {
        services.TryAddKeyedScoped<IDataSourceContentManager>(
            ProviderName,
            (sp, _) => new ElasticsearchDataSourceContentManager(
            sp.GetRequiredService<ElasticsearchClient>(),
            sp.GetRequiredService<ILogger<ElasticsearchDataSourceContentManager>>()));

        services.TryAddKeyedScoped<IDataSourceDocumentReader>(
            ProviderName,
            (sp, _) => new DataSourceElasticsearchDocumentReader(
            sp.GetRequiredService<ElasticsearchClient>()));
        services.TryAddKeyedSingleton<IODataFilterTranslator>(
            ProviderName,
            (_, _) => new ElasticsearchODataFilterTranslator());

        services.TryAddKeyedScoped<ISearchIndexManager>(
            ProviderName,
            (sp, _) => new ElasticsearchSearchIndexManager(
            sp.GetRequiredService<ElasticsearchClient>(),
            sp.GetRequiredService<IOptions<ElasticsearchConnectionOptions>>(),
            sp.GetRequiredService<ILogger<ElasticsearchSearchIndexManager>>()));

        services.TryAddKeyedScoped<ISearchDocumentManager>(
            ProviderName,
            (sp, _) => new ElasticsearchSearchDocumentManager(
            sp.GetRequiredService<ElasticsearchClient>(),
            sp.GetRequiredService<ILogger<ElasticsearchSearchDocumentManager>>()));

        services.TryAddKeyedScoped<IVectorSearchService>(
            ProviderName,
            (sp, _) => new ElasticsearchVectorSearchService(
            sp.GetRequiredService<ElasticsearchClient>(),
            sp.GetRequiredService<ILogger<ElasticsearchVectorSearchService>>()));

        services.TryAddKeyedScoped<IMemoryVectorSearchService>(
            ProviderName,
            (sp, _) => new ElasticsearchMemoryVectorSearchService(
                sp.GetRequiredService<ElasticsearchClient>(),
                sp.GetRequiredService<ILogger<ElasticsearchMemoryVectorSearchService>>()));

        return services;
    }

    public static IServiceCollection AddElasticsearchDataSource(
        this IServiceCollection services,
        string type,
        Action<IndexProfileSourceDescriptor> configure = null)
    {
        services.AddDefaultIndexProfileHandler();
        services.Configure<IndexProfileSourceOptions>(options =>
            options.AddOrUpdate(ProviderName, "Elasticsearch", type, configure));

        return services;
    }

    public static IServiceCollection AddElasticsearchAIDocumentSource(this IServiceCollection services)
        => services
            .AddElasticsearchDataSource(IndexProfileTypes.AIDocuments, descriptor =>
            {
                descriptor.DisplayName = "AI Documents";
                descriptor.Description = "Create an Elasticsearch index for uploaded and embedded AI document chunks.";
            })
            .AddAIDocumentIndexProfileHandler();

    public static IServiceCollection AddElasticsearchAIDataSource(this IServiceCollection services)
        => services
            .AddElasticsearchDataSource(IndexProfileTypes.DataSource, descriptor =>
            {
                descriptor.DisplayName = "Data Source";
                descriptor.Description = "Create an Elasticsearch index for AI knowledge base data source documents.";
            })
            .AddDataSourceRagServices()
            .AddDataSourceIndexProfileHandler();

    public static IServiceCollection AddElasticsearchAIMemorySource(this IServiceCollection services)
        => services
            .AddElasticsearchDataSource(IndexProfileTypes.AIMemory, descriptor =>
            {
                descriptor.DisplayName = "AI Memory";
                descriptor.Description = "Create an Elasticsearch index for user and system memory records.";
            })
            .AddAIMemoryIndexProfileHandler();
}
