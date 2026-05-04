using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure;
using CrestApps.Core.Infrastructure.Indexing.DataSources;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Handlers;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.Elasticsearch;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.DataSources.Elasticsearch;

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddOrchardCoreIndexingAdapters(ElasticsearchConstants.ProviderName);
        services.TryAddKeyedScoped<IDataSourceContentManager, OrchardCoreElasticsearchDataSourceContentManager>(ElasticsearchConstants.ProviderName);
        services.TryAddKeyedScoped<IDataSourceDocumentReader, OrchardCoreElasticsearchDataSourceDocumentReader>(ElasticsearchConstants.ProviderName);
        services.AddIndexProfileHandler<DataSourceElasticsearchIndexProfileHandler>();
        services.AddScoped<IDocumentIndexHandler, DataSourceElasticsearchDocumentIndexHandler>();

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
