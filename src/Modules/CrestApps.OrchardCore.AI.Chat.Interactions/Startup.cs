using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;
using CrestApps.OrchardCore.AI.Chat.Interactions.Handlers;
using CrestApps.OrchardCore.AI.Chat.Interactions.Hubs;
using CrestApps.OrchardCore.AI.Chat.Interactions.Indexes;
using CrestApps.OrchardCore.AI.Chat.Interactions.Migrations;
using CrestApps.OrchardCore.AI.Chat.Interactions.Services;
using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using CrestApps.OrchardCore.SignalR.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

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

        services
            .AddPromptRoutingServices()
            .AddDefaultPromptProcessingStrategies();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        var hubRouteManager = serviceProvider.GetRequiredService<HubRouteManager>();

        hubRouteManager.MapHub<ChatInteractionHub>(routes);
    }
}
