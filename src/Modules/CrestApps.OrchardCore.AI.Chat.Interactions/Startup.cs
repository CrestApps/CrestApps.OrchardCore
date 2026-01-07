using CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;
using CrestApps.OrchardCore.AI.Chat.Interactions.Endpoints;
using CrestApps.OrchardCore.AI.Chat.Interactions.Handlers;
using CrestApps.OrchardCore.AI.Chat.Interactions.Hubs;
using CrestApps.OrchardCore.AI.Chat.Interactions.Indexes;
using CrestApps.OrchardCore.AI.Chat.Interactions.Migrations;
using CrestApps.OrchardCore.AI.Chat.Interactions.Services;
using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using CrestApps.OrchardCore.SignalR.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Indexing.Models;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Search.Elasticsearch;
using OrchardCore.Security.Permissions;
using ElasticsearchConstants = OrchardCore.Search.Elasticsearch.ElasticsearchConstants;

namespace CrestApps.OrchardCore.AI.Chat.Interactions;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IAuthorizationHandler, ChatInteractionAuthorizationHandler>()
            .AddScoped<ICatalogEntryHandler<ChatInteraction>, ChatInteractionEntryHandler>()
            .AddScoped<ISourceCatalog<ChatInteraction>, DefaultChatInteractionCatalog>()
            .AddIndexProvider<ChatInteractionIndexProvider>()
            .AddPermissionProvider<ChatInteractionPermissionProvider>()
            .AddDisplayDriver<ChatInteraction, ChatInteractionDisplayDriver>()
            .AddDisplayDriver<ChatInteraction, ChatInteractionConnectionDisplayDriver>()
            .AddDisplayDriver<ChatInteraction, ChatInteractionDataSourceDisplayDriver>()
            .AddDisplayDriver<ChatInteraction, ChatInteractionToolsDisplayDriver>()
            .AddDisplayDriver<ChatInteractionListOptions, ChatInteractionListOptionsDisplayDriver>()
            .AddResourceConfiguration<ResourceManagementOptionsConfiguration>()
            .AddNavigationProvider<ChatInteractionsAdminMenu>()
            .AddDataMigration<ChatInteractionMigrations>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        var hubRouteManager = serviceProvider.GetRequiredService<HubRouteManager>();

        hubRouteManager.MapHub<ChatInteractionHub>(routes);
    }
}

[Feature(AIConstants.Feature.ChatDocuments)]
public sealed class DocumentsStartup : StartupBase
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
            .AddIndexProfileHandler<ChatInteractionIndexProfileHandler>();
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

        // Register display driver for Chat Interaction index profile embedding configuration
        services.AddDisplayDriver<IndexProfile, ChatInteractionIndexProfileDisplayDriver>();

        // Register Elasticsearch document index handler for chat interaction document embeddings
        services.AddScoped<IDocumentIndexHandler, ChatInteractionDocumentIndexHandler>();

        // Register Elasticsearch vector search service as a keyed service
        services.AddKeyedScoped<IVectorSearchService, ElasticsearchVectorSearchService>(ElasticsearchConstants.ProviderName);

        services.AddElasticsearchIndexingSource(ChatInteractionsConstants.IndexingTaskType, o =>
        {
            o.DisplayName = S["Chat Interaction Documents (Elasticsearch)"];
            o.Description = S["Create an Elasticsearch index for chat interaction documents."];
        });
    }
}
