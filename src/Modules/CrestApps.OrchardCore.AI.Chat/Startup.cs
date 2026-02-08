using CrestApps.OrchardCore.AI.Chat.Drivers;
using CrestApps.OrchardCore.AI.Chat.Hubs;
using CrestApps.OrchardCore.AI.Chat.Migrations;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.SignalR.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Chat;

public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _configuration;

    public Startup(IShellConfiguration configuration)
    {
        _configuration = configuration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddPermissionProvider<ChatSessionPermissionProvider>()
            .AddDisplayDriver<AIChatSessionListOptions, AIChatSessionListOptionsDisplayDriver>()
            .AddDisplayDriver<AIChatSession, AIChatSessionDisplayDriver>()
            .AddDisplayDriver<AIProfile, AIProfileMenuDisplayDriver>()
            .AddResourceConfiguration<ResourceManagementOptionsConfiguration>()
            .AddNavigationProvider<ChatAdminMenu>()
            .AddDisplayDriver<AIProfile, AIProfileDisplayDriver>();

        // Configure PromptProcessingOptions from configuration
        services.Configure<PromptProcessingOptions>(_configuration.GetSection(PromptProcessingOptions.SectionName));

        // Register intent detection and strategy routing services for AI Profile chat
        services
            .AddPromptRoutingServices()
            .AddDefaultPromptProcessingStrategies();
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
