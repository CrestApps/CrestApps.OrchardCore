using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Chat.Models;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.SignalR.Services;
using CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;
using CrestApps.OrchardCore.AI.Chat.Interactions.Handlers;
using CrestApps.OrchardCore.AI.Chat.Interactions.Hubs;
using CrestApps.OrchardCore.AI.Chat.Interactions.Migrations;
using CrestApps.OrchardCore.AI.Chat.Interactions.Services;
using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Chat.Interactions;

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    public Startup(IShellConfiguration configuration)
    {
        _configuration = configuration;
    }

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
            .AddDisplayDriver<ChatInteraction, ChatInteractionToolsDisplayDriver>()
            .AddDisplayDriver<ChatInteraction, ChatInteractionAgentsDisplayDriver>()
            .AddDisplayDriver<ChatInteractionListOptions, ChatInteractionListOptionsDisplayDriver>()
            .AddResourceConfiguration<ResourceManagementOptionsConfiguration>()
            .AddNavigationProvider<ChatInteractionsAdminMenu>()
            .AddDataMigration<DataSourceMetadataMigrations>();

        services
            .AddSiteDisplayDriver<ChatInteractionChatModeSettingsDisplayDriver>()
            .AddNavigationProvider<AISiteSettingsAdminMenu>();

        // Configure RowLevelTabularBatchSettings from configuration
        services.Configure<RowLevelTabularBatchOptions>(_configuration.GetSection("CrestApps_AI:ChatInteractions:BatchProcessing"));

        // Chat Interaction notification transport and hub options.
        services.AddKeyedScoped<IChatNotificationTransport, ChatInteractionNotificationTransport>(ChatContextType.ChatInteraction);
        services.ConfigureCrestAppsChatHubOptions<ChatInteractionHub>();

        services.AddDisplayDriver<ChatInteraction, ChatInteractionConnectionDisplayDriver>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        HubRouteManager.MapHub<ChatInteractionHub>(routes);
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
