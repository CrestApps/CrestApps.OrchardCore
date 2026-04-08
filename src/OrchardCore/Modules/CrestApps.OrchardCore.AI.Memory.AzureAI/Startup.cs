using CrestApps.Core.AI.Memory;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Memory.AzureAI.Handlers;
using CrestApps.OrchardCore.AI.Memory.AzureAI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Modules;
using OrchardCore.Search.AzureAI;
using OrchardCore.Search.AzureAI.Core;

namespace CrestApps.OrchardCore.AI.Memory.AzureAI;

public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddOrchardCoreIndexingAdapters(AzureAISearchConstants.ProviderName);
        services.AddIndexProfileHandler<AIMemoryAzureAISearchIndexProfileHandler>();
        services.AddScoped<IDocumentIndexHandler, AIMemoryAzureAISearchDocumentIndexHandler>();
        services.AddKeyedScoped<IMemoryVectorSearchService, AzureAISearchMemoryVectorSearchService>(AzureAISearchConstants.ProviderName);
        services.AddAzureAISearchIndexingSource(MemoryConstants.IndexingTaskType, o =>
        {
            o.DisplayName = S["AI Memory (Azure AI Search)"];
            o.Description = S["Create an Azure AI Search index for persistent user memories."];
        });
    }
}
