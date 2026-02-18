using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Documents.AzureAI.Handlers;
using CrestApps.OrchardCore.AI.Documents.AzureAI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Modules;
using OrchardCore.Search.AzureAI;
using OrchardCore.Search.AzureAI.Core;

namespace CrestApps.OrchardCore.AI.Documents.AzureAI;

public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddIndexProfileHandler<AIDocumentAzureAISearchIndexProfileHandler>();
        services.AddScoped<IDocumentIndexHandler, AIDocumentAzureAISearchDocumentIndexHandler>();
        services.AddKeyedScoped<IVectorSearchService, AzureAISearchVectorSearchService>(AzureAISearchConstants.ProviderName);
        services.AddAzureAISearchIndexingSource(AIConstants.AIDocumentsIndexingTaskType, o =>
        {
            o.DisplayName = S["AI Documents (Azure AI Search)"];
            o.Description = S["Create an Azure AI Search index for AI documents."];
        });
    }
}