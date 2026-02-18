using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Documents.Drivers;
using CrestApps.OrchardCore.AI.Documents.Endpoints;
using CrestApps.OrchardCore.AI.Documents.Handlers;
using CrestApps.OrchardCore.AI.Documents.Indexes;
using CrestApps.OrchardCore.AI.Documents.Migrations;
using CrestApps.OrchardCore.AI.Documents.Services;
using CrestApps.OrchardCore.AI.Documents.Tools;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Services;
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

namespace CrestApps.OrchardCore.AI.Documents;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDocumentTextExtractor<DefaultDocumentTextExtractor>(".txt", new ExtractorExtension(".csv", false),
                ".md", ".json", ".xml", ".html", ".htm", ".log", ".yaml", ".yml");

        services
            .AddSiteDisplayDriver<InteractionDocumentSettingsDisplayDriver>()
             .AddNavigationProvider<AISiteSettingsAdminMenu>();

        // Register unified document store, index provider, and migration.
        services.AddScoped<IAIDocumentStore, DefaultAIDocumentStore>();
        services.AddScoped<IAIDocumentProcessingService, DefaultAIDocumentProcessingService>();
        services.AddIndexProvider<AIDocumentIndexProvider>();
        services.AddDataMigration<AIDocumentIndexMigrations>();

        // Add document processing system tools and supporting services.
        services.AddDefaultDocumentProcessingServices();

        // Register the RAG search system tool.
        services.AddAITool<SearchDocumentsTool>(SearchDocumentsTool.TheName)
            .WithTitle("Search Documents")
            .WithDescription("Searches uploaded or attached documents using semantic vector search.")
            .WithPurpose(AIToolPurposes.DocumentProcessing);
    }
}

[Feature(ChatInteractionsConstants.Feature.ChatInteractionDocuments)]
public sealed class ChatInteractionDocumentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDisplayDriver<ChatInteraction, ChatInteractionDocumentsDisplayDriver>();

        // Add Indexing Services.
        services.AddScoped<ICatalogEntryHandler<ChatInteraction>, ChatInteractionIndexingHandler>()
            .AddScoped<AIDocumentsIndexingService>()
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

[Feature(AIConstants.Feature.ProfileDocuments)]
public sealed class ProfileDocumentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<AIProfile, AIProfileDocumentsDisplayDriver>();
    }
}
