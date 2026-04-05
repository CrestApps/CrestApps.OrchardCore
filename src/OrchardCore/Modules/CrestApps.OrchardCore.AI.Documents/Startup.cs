using CrestApps.AI;
using CrestApps.AI.Chat;
using CrestApps.AI.Chat.Endpoints;
using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Documents.Drivers;
using CrestApps.OrchardCore.AI.Documents.Handlers;
using CrestApps.OrchardCore.AI.Documents.Indexes;
using CrestApps.OrchardCore.AI.Documents.Migrations;
using CrestApps.OrchardCore.AI.Documents.Services;
using CrestApps.OrchardCore.AI.Services;
using CrestApps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
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
        services.AddDefaultDocumentProcessingServices();
        services.AddTransient<IConfigureOptions<InteractionDocumentOptions>, InteractionDocumentOptionsConfiguration>();
        services
            .AddSiteDisplayDriver<InteractionDocumentSettingsDisplayDriver>()
            .AddNavigationProvider<AISiteSettingsAdminMenu>();

        // Register unified document store, index provider, and migration.
        services
            .Configure<StoreCollectionOptions>(o => o.Collections.Add(AIConstants.AIDocsCollectionName))
            .AddScoped<IAIDocumentChunkStore, DefaultAIDocumentChunkStore>()
            .AddScoped<IAIDocumentStore, DefaultAIDocumentStore>();

        services.AddScoped<IAuthorizationHandler, OrchardChatInteractionDocumentAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, OrchardAIChatSessionDocumentAuthorizationHandler>();
        services.AddScoped<IAIChatDocumentEventHandler, OrchardAIChatDocumentEventHandler>();

        services.AddIndexProvider<AIDocumentIndexProvider>();
        services.AddIndexProvider<AIDocumentChunkIndexProvider>();
        services.AddDataMigration<AIDocumentIndexMigrations>();
        services.AddDataMigration<AIDocumentChunkIndexMigrations>();

        // Register the session document cleanup handler to remove documents when a chat session is deleted.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAIChatSessionHandler, AIChatSessionDocumentCleanupHandler>());
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
            .AddUploadChatInteractionDocumentEndpoint()
            .AddRemoveChatInteractionDocumentEndpoint();
    }
}

[Feature(AIConstants.Feature.ProfileDocuments)]
public sealed class ProfileDocumentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<AIProfile, AIProfileDocumentsDisplayDriver>();
        services.AddDisplayDriver<AIProfileTemplate, AIProfileTemplateDocumentsDisplayDriver>();
    }
}

[Feature(AIConstants.Feature.ChatSessionDocuments)]
public sealed class ChatSessionDocumentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<AIProfile, AIProfileSessionDocumentsDisplayDriver>();
        services.AddDisplayDriver<AIProfileTemplate, AIProfileTemplateSessionDocumentsDisplayDriver>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes
            .AddUploadChatSessionDocumentEndpoint()
            .AddRemoveChatSessionDocumentEndpoint();
    }
}
