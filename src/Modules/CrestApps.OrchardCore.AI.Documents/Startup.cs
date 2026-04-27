using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Documents;
using CrestApps.Core.AI.Documents.Endpoints;
using CrestApps.Core.AI.Documents.Models;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Documents.Drivers;
using CrestApps.OrchardCore.AI.Documents.Handlers;
using CrestApps.OrchardCore.AI.Documents.Migrations;
using CrestApps.OrchardCore.AI.Documents.Services;
using CrestApps.OrchardCore.AI.Services;
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

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAIDocumentProcessing()
            .AddCoreAIDocumentProcessingStoresYesSql()
            .AddDataMigration<AIDocumentIndexMigrations>()
            .AddDataMigration<AIDocumentChunkIndexMigrations>();

        services.AddTransient<IConfigureOptions<InteractionDocumentOptions>, InteractionDocumentOptionsConfiguration>();
        services.AddSingleton<IPostConfigureOptions<DocumentFileSystemFileStoreOptions>, DocumentFileSystemFileStoreOptionsPostConfiguration>();
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

        // Register the session document cleanup handler to remove documents when a chat session is deleted.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAIChatSessionHandler, AIChatSessionDocumentCleanupHandler>());
    }
}

/// <summary>
/// Registers services and configuration for the ChatInteractionDocuments feature.
/// </summary>
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
            .AddUploadChatInteractionDocumentEndpoint(AIConstants.RouteNames.ChatInteractionUploadDocument)
            .AddRemoveChatInteractionDocumentEndpoint(AIConstants.RouteNames.ChatInteractionRemoveDocument);
    }
}

/// <summary>
/// Registers services and configuration for the ProfileDocuments feature.
/// </summary>
[Feature(AIConstants.Feature.ProfileDocuments)]
public sealed class ProfileDocumentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<AIProfile, AIProfileDocumentsDisplayDriver>();
        services.AddDisplayDriver<AIProfileTemplate, AIProfileTemplateDocumentsDisplayDriver>();
    }
}

/// <summary>
/// Registers services and configuration for the ChatSessionDocuments feature.
/// </summary>
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
            .AddUploadChatSessionDocumentEndpoint(AIConstants.RouteNames.ChatSessionUploadDocument)
            .AddRemoveChatSessionDocumentEndpoint(AIConstants.RouteNames.ChatSessionRemoveDocument);
    }
}
