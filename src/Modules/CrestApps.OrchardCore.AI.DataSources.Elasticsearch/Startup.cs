using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
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
        services.AddKeyedScoped<IDataSourceContentManager, ElasticsearchDataSourceContentManager>(ElasticsearchConstants.ProviderName);
        services.AddKeyedScoped<IDataSourceDocumentReader, DataSourceElasticsearchDocumentReader>(ElasticsearchConstants.ProviderName);
        services.AddKeyedSingleton<IODataFilterTranslator, ElasticsearchODataFilterTranslator>(ElasticsearchConstants.ProviderName);

        services.AddElasticsearchIndexingSource(DataSourceConstants.IndexingTaskType, o =>
        {
            o.DisplayName = S["AI Knowledge Base Index (Elasticsearch)"];
            o.Description = S["Create an Elasticsearch index to store AI knowledge base document embeddings for vector search."];
        });

        services.Configure<AIDataSourceOptions>(options =>
        {
            options.AddFieldMapping(ElasticsearchConstants.ProviderName, IndexingConstants.ContentsIndexSource, mapping =>
            {
                mapping.DefaultKeyField = "ContentItemId";
                mapping.DefaultTitleField = "Content.ContentItem.DisplayText.keyword";
                mapping.DefaultContentField = "Content.ContentItem.FullText";
            });

            options.AddFieldMapping(ElasticsearchConstants.ProviderName, AIConstants.AIDocumentsIndexingTaskType, mapping =>
            {
                mapping.DefaultKeyField = AIConstants.ColumnNames.ChunkId;
                mapping.DefaultTitleField = AIConstants.ColumnNames.FileName;
                mapping.DefaultContentField = AIConstants.ColumnNames.Content;
            });
        });
    }
}
