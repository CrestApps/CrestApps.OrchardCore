using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Handlers;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Modules;
using OrchardCore.Search.Elasticsearch;

namespace CrestApps.OrchardCore.AI.DataSources.Elasticsearch;

public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddIndexProfileHandler<DataSourceElasticsearchIndexProfileHandler>();
        services.AddScoped<IDocumentIndexHandler, DataSourceElasticsearchDocumentIndexHandler>();
        services.AddKeyedScoped<IDataSourceVectorSearchService, DataSourceElasticsearchVectorSearchService>(ElasticsearchConstants.ProviderName);
        services.AddKeyedScoped<IDataSourceDocumentReader, DataSourceElasticsearchDocumentReader>(ElasticsearchConstants.ProviderName);
        services.AddKeyedSingleton<IODataFilterTranslator, ElasticsearchODataFilterTranslator>(ElasticsearchConstants.ProviderName);

        services.AddElasticsearchIndexingSource(DataSourceConstants.IndexingTaskType, o =>
        {
            o.DisplayName = S["AI Knowledge Base Index (Elasticsearch)"];
            o.Description = S["Create an Elasticsearch index to store AI knowledge base document embeddings for vector search."];
        });
    }
}
