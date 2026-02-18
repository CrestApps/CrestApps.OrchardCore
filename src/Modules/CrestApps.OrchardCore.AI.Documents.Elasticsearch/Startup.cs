using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Documents.Elasticsearch.Handlers;
using CrestApps.OrchardCore.AI.Documents.Elasticsearch.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Modules;
using OrchardCore.Search.Elasticsearch;

namespace CrestApps.OrchardCore.AI.Documents.Elasticsearch;

public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddIndexProfileHandler<AIDocumentElasticsearchIndexProfileHandler>();

        // Register Elasticsearch document index handler for AI document embeddings.
        services.AddScoped<IDocumentIndexHandler, AIDocumentElasticsearchDocumentIndexHandler>();

        // Register Elasticsearch vector search service as a keyed service.
        services.AddKeyedScoped<IVectorSearchService, ElasticsearchVectorSearchService>(ElasticsearchConstants.ProviderName);

        services.AddElasticsearchIndexingSource(AIConstants.AIDocumentsIndexingTaskType, o =>
        {
            o.DisplayName = S["AI Documents (Elasticsearch)"];
            o.Description = S["Create an Elasticsearch index for AI documents."];
        });
    }
}

