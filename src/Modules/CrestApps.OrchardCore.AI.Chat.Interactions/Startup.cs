using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Chat.Models;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;
using CrestApps.OrchardCore.AI.Chat.Interactions.Handlers;
using CrestApps.OrchardCore.AI.Chat.Interactions.Hubs;
using CrestApps.OrchardCore.AI.Chat.Interactions.Migrations;
using CrestApps.OrchardCore.AI.Chat.Interactions.Services;
using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Chat.Interactions;

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Register framework-level chat interaction handlers.
        services
            .AddCoreAIChatInteractions()
            .AddCoreAIChatInteractionStoresYesSql()
            .AddDataMigration<ChatInteractionMigrations>()
            .AddDataMigration<ChatInteractionPromptIndexMigrations>();

        services
            .AddScoped<IAuthorizationHandler, ChatInteractionAuthorizationHandler>()
            .AddPermissionProvider<ChatInteractionPermissionProvider>()
            .AddDisplayDriver<ChatInteraction, ChatInteractionDisplayDriver>()
            .AddDisplayDriver<ChatInteraction, ChatInteractionModelParametersDisplayDriver>()
            .AddDisplayDriver<ChatInteraction, ChatInteractionToolsDisplayDriver>()
            .AddDisplayDriver<ChatInteraction, ChatInteractionAgentsDisplayDriver>()
            .AddDisplayDriver<ChatInteractionListOptions, ChatInteractionListOptionsDisplayDriver>()
            .AddResourceConfiguration<ResourceManagementOptionsConfiguration>()
            .AddNavigationProvider<ChatInteractionsAdminMenu>()
            .AddDataMigration<DataSourceMetadataMigrations>();

        services
            .AddSiteDisplayDriver<ChatInteractionChatModeSettingsDisplayDriver>()
            .AddNavigationProvider<AISiteSettingsAdminMenu>();

        // Configure RowLevelTabularBatchSettings from configuration.
        services.AddTransient<IConfigureOptions<RowLevelTabularBatchOptions>, RowLevelTabularBatchOptionsConfiguration>();

        // Chat Interaction notification transport and hub options.
        services.AddKeyedScoped<IChatNotificationTransport, ChatInteractionNotificationTransport>(ChatContextType.ChatInteraction);
        services.ConfigureCrestAppsChatHubOptions<ChatInteractionHub>();

        // Enables realtime (speech-to-speech) voice over the server-relay WebRTC transport, with automatic
        // WebSocket fallback. Idempotent when also registered by the AI Chat feature.
        services.AddWebRtcRealtimeTransport();

        services.AddDisplayDriver<ChatInteraction, ChatInteractionConnectionDisplayDriver>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapHub<ChatInteractionHub>(SignalRHubRoutes.GetHubPath<ChatInteractionHub>());
    }
}

/// <summary>
/// Registers services and configuration for the DataSource feature.
/// </summary>
[RequireFeatures(AIConstants.Feature.DataSources)]
public sealed class DataSourceStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<ChatInteraction, ChatInteractionDataSourceDisplayDriver>();
    }
}

/// <summary>
/// Registers services and configuration for the ToolInstances feature.
/// </summary>
[RequireFeatures(AIConstants.Feature.ToolInstances)]
public sealed class ToolInstancesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<ChatInteraction, ChatInteractionToolInstancesDisplayDriver>();
    }
}
