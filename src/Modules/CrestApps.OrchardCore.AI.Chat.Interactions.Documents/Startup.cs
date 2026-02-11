using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Services;
using CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Drivers;
using CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Endpoints;
using CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Handlers;
using CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Indexes;
using CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Migrations;
using CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Services;
using CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Strategies;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
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
        services.AddScoped<DefaultChatInteractionDocumentStore>()
            .AddScoped<IChatInteractionDocumentStore>(sp => sp.GetRequiredService<DefaultChatInteractionDocumentStore>())
            .AddScoped<ICatalog<ChatInteractionDocument>>(sp => sp.GetRequiredService<DefaultChatInteractionDocumentStore>());

        services
            .AddDocumentTextExtractor<DefaultDocumentTextExtractor>(".txt", new ExtractorExtension(".csv", false),
                ".md", ".json", ".xml", ".html", ".htm", ".log", ".yaml", ".yml")
            .AddDisplayDriver<ChatInteraction, ChatInteractionDocumentsDisplayDriver>()
            .AddSiteDisplayDriver<InteractionDocumentSettingsDisplayDriver>()
            .AddNavigationProvider<ChatInteractionDocumentsAdminMenu>()
            .AddIndexProvider<ChatInteractionDocumentIndexProvider>()
            .AddDataMigration<ChatInteractionDocumentIndexMigrations>();

        // Add Indexing Services.
        services.AddScoped<ICatalogEntryHandler<ChatInteraction>, ChatInteractionIndexingHandler>()
            .AddScoped<ChatInteractionIndexingService>()
            .AddScoped<ICatalogEntryHandler<ChatInteraction>, ChatInteractionHandler>()
            .AddIndexProfileHandler<ChatInteractionIndexProfileHandler>()
            .AddDisplayDriver<IndexProfile, ChatInteractionIndexProfileDisplayDriver>();

        // Add document processing services for intent-aware, strategy-based document handling.
        services.AddDefaultDocumentPromptProcessingStrategies();

        services.AddPromptProcessingIntent(
            DocumentIntents.DocumentQnA,
            "The user wants to ask questions about documents, search for information, or find specific content within documents using RAG (Retrieval-Augmented Generation).")
            .WithStrategy<RagDocumentProcessingStrategy>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes
            .AddUploadDocumentEndpoint()
            .AddRemoveDocumentEndpoint();
    }
}
