using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Drivers;
using CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Endpoints;
using CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Handlers;
using CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Services;
using CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Strategies;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Indexing.Core;
using OrchardCore.Indexing.Models;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Documents;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDocumentTextExtractor<DefaultDocumentTextExtractor>(".txt", ".csv",
                ".md", ".json", ".xml", ".html", ".htm", ".log", ".yaml", ".yml")
            .AddDisplayDriver<ChatInteraction, ChatInteractionDocumentsDisplayDriver>()
            .AddSiteDisplayDriver<InteractionDocumentSettingsDisplayDriver>()
            .AddNavigationProvider<ChatInteractionDocumentsAdminMenu>();

        // Add Indexing Services.
        services.AddScoped<ICatalogEntryHandler<ChatInteraction>, ChatInteractionIndexingHandler>()
            .AddScoped<ChatInteractionIndexingService>()
            .AddScoped<ICatalogEntryHandler<ChatInteraction>, ChatInteractionHandler>()
            .AddIndexProfileHandler<ChatInteractionIndexProfileHandler>()
            .AddDisplayDriver<IndexProfile, ChatInteractionIndexProfileDisplayDriver>();

        // Add document processing services for intent-aware, strategy-based document handling.
        services
            .AddDocumentProcessingServices()
            .AddDefaultDocumentProcessingStrategies()
            .AddDocumentProcessingStrategy<RagDocumentProcessingStrategy>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes
            .AddUploadDocumentEndpoint()
            .AddRemoveDocumentEndpoint();
    }
}
