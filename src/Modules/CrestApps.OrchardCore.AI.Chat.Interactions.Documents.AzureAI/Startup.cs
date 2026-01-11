using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Chat.Interactions.Documents.AzureAI.Handlers;
using CrestApps.OrchardCore.AI.Chat.Interactions.Documents.AzureAI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Modules;
using OrchardCore.Search.AzureAI;
using OrchardCore.Search.AzureAI.Core;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Documents.AzureAI;

[RequireFeatures(ChatInteractionsConstants.Feature.ChatDocuments, "OrchardCore.Search.AzureAI")]
public sealed class AzureAISearchStartup : StartupBase
{
    internal readonly IStringLocalizer S;
    public AzureAISearchStartup(IStringLocalizer<AzureAISearchStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddIndexProfileHandler<ChatInteractionAzureAISearchIndexProfileHandler>();
        // Register Azure AI Search document index handler for chat interaction document embeddings
        services.AddScoped<IDocumentIndexHandler, ChatInteractionAzureAISearchDocumentIndexHandler>();
        // Register Azure AI Search vector search service as a keyed service
        services.AddKeyedScoped<IVectorSearchService, AzureAISearchVectorSearchService>(AzureAISearchConstants.ProviderName);
        services.AddAzureAISearchIndexingSource(ChatInteractionsConstants.IndexingTaskType, o =>
        {
            o.DisplayName = S["Chat Interaction Documents (Azure AI Search)"];
            o.Description = S["Create an Azure AI Search index for chat interaction documents."];
        });
    }
}
