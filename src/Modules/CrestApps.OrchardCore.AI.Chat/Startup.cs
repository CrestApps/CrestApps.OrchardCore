using CrestApps.OrchardCore.AI.Chat.Core.Hubs;
using CrestApps.OrchardCore.AI.Chat.Drivers;
using CrestApps.OrchardCore.AI.Chat.Filters;
using CrestApps.OrchardCore.AI.Chat.Hubs;
using CrestApps.OrchardCore.AI.Chat.Migrations;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Services;
using CrestApps.OrchardCore.SignalR.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Chat;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddPermissionProvider<ChatSessionPermissionProvider>()
            .AddDisplayDriver<AIChatSessionListOptions, AIChatSessionListOptionsDisplayDriver>()
            .AddDisplayDriver<AIChatSession, AIChatSessionDisplayDriver>()
            .AddDisplayDriver<AIProfile, AIProfileMenuDisplayDriver>()
            .AddDisplayDriver<AIProfileTemplate, AIProfileTemplateMenuDisplayDriver>()
            .AddResourceConfiguration<ResourceManagementOptionsConfiguration>()
            .AddNavigationProvider<ChatAdminMenu>()
            .AddDisplayDriver<AIProfile, AIProfileDisplayDriver>()
            .AddDisplayDriver<AIProfile, AIProfileSessionSettingsDisplayDriver>()
            .AddDisplayDriver<AIProfileTemplate, AIProfileTemplateSessionSettingsDisplayDriver>()
            .AddDisplayDriver<AIProfile, AIProfileDataExtractionDisplayDriver>()
            .AddDisplayDriver<AIProfileTemplate, AIProfileTemplateDataExtractionDisplayDriver>()
            .AddDisplayDriver<AIProfile, AIProfilePostSessionDisplayDriver>()
            .AddDisplayDriver<AIProfileTemplate, AIProfileTemplatePostSessionDisplayDriver>()
            .AddDisplayDriver<AIProfile, AIProfileChatModeDisplayDriver>()
            .AddDisplayDriver<AIProfileTemplate, AIProfileTemplateChatModeDisplayDriver>();

        // Chat notification services.
        services.TryAddScoped<IChatNotificationSender, DefaultChatNotificationSender>();
        services.AddKeyedScoped<IChatNotificationTransport, AIChatNotificationTransport>(ChatContextType.AIChatSession);
        services.AddKeyedScoped<IChatNotificationActionHandler, CancelTransferNotificationActionHandler>(ChatNotificationActionNames.CancelTransfer);
        services.AddKeyedScoped<IChatNotificationActionHandler, EndSessionNotificationActionHandler>(ChatNotificationActionNames.EndSession);

        services.Configure<HubOptions<AIChatHub>>(options =>
        {
            // Allow long-running operations (e.g., multi-step MCP tool calls)
            // without the server dropping the connection prematurely.
            options.ClientTimeoutInterval = TimeSpan.FromMinutes(10);
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);

            // Allow larger messages for audio transcription payloads.
            options.MaximumReceiveMessageSize = 10 * 1024 * 1024;
        });
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

[Feature(AIConstants.Feature.ChatAdminWidget)]
public sealed class AdminWidgetStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddSiteDisplayDriver<AIChatAdminWidgetSettingsDisplayDriver>()
            .AddNavigationProvider<AISiteSettingsAdminMenu>();

        services.Configure<MvcOptions>(options =>
        {
            options.Filters.Add<AIChatAdminWidgetFilter>();
        });
    }
}

[Feature(AIConstants.Feature.ChatAnalytics)]
public sealed class ChatAnalyticsUIStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddPermissionProvider<ChatAnalyticsPermissionProvider>()
            .AddNavigationProvider<ChatAnalyticsAdminMenu>()
            .AddDisplayDriver<AIProfile, AIProfileAnalyticsDisplayDriver>()
            .AddDisplayDriver<AIProfileTemplate, AIProfileTemplateAnalyticsDisplayDriver>()
            .AddDisplayDriver<AIChatAnalyticsFilter, AIChatAnalyticsDateRangeFilterDisplayDriver>()
            .AddDisplayDriver<AIChatAnalyticsFilter, AIChatAnalyticsProfileFilterDisplayDriver>()
            .AddDisplayDriver<AIChatAnalyticsReport, AIChatAnalyticsOverviewDisplayDriver>()
            .AddDisplayDriver<AIChatAnalyticsReport, AIChatAnalyticsTimeOfDayDisplayDriver>()
            .AddDisplayDriver<AIChatAnalyticsReport, AIChatAnalyticsDayOfWeekDisplayDriver>()
            .AddDisplayDriver<AIChatAnalyticsReport, AIChatAnalyticsUserSegmentDisplayDriver>()
            .AddDisplayDriver<AIChatAnalyticsReport, AIChatAnalyticsPerformanceDisplayDriver>()
            .AddDisplayDriver<AIChatAnalyticsReport, AIChatAnalyticsConversionDisplayDriver>()
            .AddDisplayDriver<AIChatAnalyticsReport, AIChatAnalyticsFeedbackDisplayDriver>();
    }
}
