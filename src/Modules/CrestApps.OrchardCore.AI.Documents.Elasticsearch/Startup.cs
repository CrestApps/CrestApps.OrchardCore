using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Documents.Elasticsearch.Handlers;
using CrestApps.OrchardCore.AI.Documents.Elasticsearch.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Elasticsearch;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Documents.Elasticsearch;

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
