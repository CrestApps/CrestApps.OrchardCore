using CrestApps.AI;
using CrestApps.AI.Elasticsearch.Services;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CrestApps.AI.Elasticsearch;

public static class ServiceCollectionExtensions
{
    public const string ProviderName = "Elasticsearch";

    public static IServiceCollection AddElasticsearchServices(
        this IServiceCollection services,
        IConfigurationSection configuration)
    {
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

        return services;
    }
}
