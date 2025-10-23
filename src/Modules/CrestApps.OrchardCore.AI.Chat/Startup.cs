using CrestApps.OrchardCore.AI.Chat.Drivers;
using CrestApps.OrchardCore.AI.Chat.Hubs;
using CrestApps.OrchardCore.AI.Chat.Migrations;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.SignalR.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.ResourceManagement;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Chat;

[Feature(AIConstants.Feature.Chat)]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IPermissionProvider, ChatSessionPermissionProvider>()
            .AddDisplayDriver<AIChatSessionListOptions, AIChatSessionListOptionsDisplayDriver>()
            .AddDisplayDriver<AIChatSession, AIChatSessionDisplayDriver>()
            .AddDisplayDriver<AIProfile, AIProfileMenuDisplayDriver>()
            .AddTransient<IConfigureOptions<ResourceManagementOptions>, ResourceManagementOptionsConfiguration>()
            .AddNavigationProvider<ChatAdminMenu>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        var hubRouteManager = serviceProvider.GetRequiredService<HubRouteManager>();

        hubRouteManager.MapHub<AIChatHub>(routes);
    }
}

[RequireFeatures("OrchardCore.Widgets")]
public sealed class WidgetsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddContentPart<AIProfilePart>()
            .UseDisplayDriver<AIChatProfilePartDisplayDriver>();

        services.AddDataMigration<AIChatMigrations>();
    }
}
