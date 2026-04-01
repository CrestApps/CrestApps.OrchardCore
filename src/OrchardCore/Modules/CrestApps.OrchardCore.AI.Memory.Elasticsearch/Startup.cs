using CrestApps.OrchardCore.AI.Memory.Elasticsearch.Handlers;
using CrestApps.OrchardCore.AI.Memory.Elasticsearch.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Modules;
using OrchardCore.Search.Elasticsearch;

namespace CrestApps.OrchardCore.AI.Memory.Elasticsearch;

public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;
    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddIndexProfileHandler<AIMemoryElasticsearchIndexProfileHandler>();
        services.AddScoped<IDocumentIndexHandler, AIMemoryElasticsearchDocumentIndexHandler>();
        services.AddKeyedScoped<IMemoryVectorSearchService, ElasticsearchMemoryVectorSearchService>(ElasticsearchConstants.ProviderName);
        services.AddElasticsearchIndexingSource(MemoryConstants.IndexingTaskType, o =>
        {
            o.DisplayName = S["AI Memory (Elasticsearch)"];
            o.Description = S["Create an Elasticsearch index for persistent user memories."];
        });
    }
}
