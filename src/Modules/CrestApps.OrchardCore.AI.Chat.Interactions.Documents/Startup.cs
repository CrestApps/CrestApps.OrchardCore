using CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Drivers;
using CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Endpoints;
using CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Handlers;
using CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Indexing.Models;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Search.AzureAI;
using OrchardCore.Search.AzureAI.Core;
using OrchardCore.Search.Elasticsearch;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Documents;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDocumentTextExtractor<DefaultDocumentTextExtractor>()
            .AddDisplayDriver<ChatInteraction, ChatInteractionDocumentsDisplayDriver>()
            .AddSiteDisplayDriver<InteractionDocumentSettingsDisplayDriver>()
            .AddNavigationProvider<ChatInteractionDocumentsAdminMenu>();

        // Add Indexing Services.
        services.AddScoped<ICatalogEntryHandler<ChatInteraction>, ChatInteractionIndexingHandler>()
            .AddScoped<ChatInteractionIndexingService>()
            .AddScoped<ICatalogEntryHandler<ChatInteraction>, ChatInteractionHandler>()
            .AddIndexProfileHandler<ChatInteractionIndexProfileHandler>()
            .AddDisplayDriver<IndexProfile, ChatInteractionIndexProfileDisplayDriver>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes
            .AddUploadDocumentEndpoint()
            .AddRemoveDocumentEndpoint();
    }
}

[RequireFeatures(AIConstants.Feature.ChatDocuments, "OrchardCore.Search.Elasticsearch")]
public sealed class ElasticsearchStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public ElasticsearchStartup(IStringLocalizer<ElasticsearchStartup> stringLocalizer)
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

[RequireFeatures(AIConstants.Feature.ChatDocuments, "OrchardCore.Search.AzureAI")]
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
