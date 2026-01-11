using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Elasticsearch.Handlers;
using CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Elasticsearch.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Modules;
using OrchardCore.Search.Elasticsearch;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Elasticsearch;

public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddIndexProfileHandler<ChatInteractionElasticsearchIndexProfileHandler>();

        // Register Elasticsearch document index handler for chat interaction document embeddings
        services.AddScoped<IDocumentIndexHandler, ChatInteractionElasticsearchDocumentIndexHandler>();

        // Register Elasticsearch vector search service as a keyed service
        services.AddKeyedScoped<IVectorSearchService, ElasticsearchVectorSearchService>(ElasticsearchConstants.ProviderName);

        services.AddElasticsearchIndexingSource(ChatInteractionsConstants.IndexingTaskType, o =>
        {
            o.DisplayName = S["Chat Interaction Documents (Elasticsearch)"];
            o.Description = S["Create an Elasticsearch index for chat interaction documents."];
        });
    }
}

