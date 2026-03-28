using CrestApps.AI;
using CrestApps.AI.DataSources.Elasticsearch.Services;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CrestApps.AI.DataSources.Elasticsearch;

public static class ServiceCollectionExtensions
{
    public const string ProviderName = "Elasticsearch";

    /// <summary>
    /// Adds Elasticsearch data source services and registers an <see cref="ElasticsearchClient"/>
    /// from the given configuration section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">
    /// A configuration section that binds to <see cref="ElasticsearchConnectionOptions"/>
    /// (e.g. "CrestApps:Search:Elasticsearch").
    /// </param>
    public static IServiceCollection AddElasticsearchDataSourceServices(
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

        return services.AddElasticsearchDataSourceServices();
    }

    /// <summary>
    /// Adds Elasticsearch data source and index management services.
    /// Requires an <see cref="ElasticsearchClient"/> to be registered in the service container.
    /// </summary>
    public static IServiceCollection AddElasticsearchDataSourceServices(this IServiceCollection services)
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
