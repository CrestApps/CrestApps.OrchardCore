using CrestApps.AI;
using CrestApps.AI.DataSources.Elasticsearch.Services;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.AI.DataSources.Elasticsearch;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Elasticsearch data source services.
    /// Requires an <see cref="ElasticsearchClient"/> to be registered in the service container.
    /// </summary>
    public static IServiceCollection AddElasticsearchDataSourceServices(this IServiceCollection services)
    {
        services.TryAddKeyedScoped<IDataSourceContentManager>(
            "Elasticsearch",
            (sp, _) => new ElasticsearchDataSourceContentManager(
                sp.GetRequiredService<ElasticsearchClient>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ElasticsearchDataSourceContentManager>>()));

        services.TryAddKeyedScoped<IDataSourceDocumentReader>(
            "Elasticsearch",
            (sp, _) => new DataSourceElasticsearchDocumentReader(
                sp.GetRequiredService<ElasticsearchClient>()));

        services.TryAddKeyedSingleton<IODataFilterTranslator>(
            "Elasticsearch",
            (_, _) => new ElasticsearchODataFilterTranslator());

        return services;
    }
}
