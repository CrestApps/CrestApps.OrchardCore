using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.Core.SignalR.Services;
using CrestApps.OrchardCore.AI.Chat.Core.Hubs;
using CrestApps.OrchardCore.AI.Chat.Drivers;
using CrestApps.OrchardCore.AI.Chat.Filters;
using CrestApps.OrchardCore.AI.Chat.Hubs;
using CrestApps.OrchardCore.AI.Chat.Migrations;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
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
            .AddDisplayDriver<AIProfile, AIProfileSessionSettingsDisplayDriver>()
            .AddDisplayDriver<AIProfileTemplate, AIProfileTemplateSessionSettingsDisplayDriver>()
            .AddDisplayDriver<AIProfile, AIProfileDataExtractionDisplayDriver>()
            .AddDisplayDriver<AIProfileTemplate, AIProfileTemplateDataExtractionDisplayDriver>()
            .AddDisplayDriver<AIProfile, AIProfilePostSessionDisplayDriver>()
            .AddDisplayDriver<AIProfileTemplate, AIProfileTemplatePostSessionDisplayDriver>()
            .AddDisplayDriver<AIProfile, AIProfileChatModeDisplayDriver>()
            .AddDisplayDriver<AIProfileTemplate, AIProfileTemplateChatModeDisplayDriver>();

        // Chat notification services and hub options.
        // Action handlers and sender are registered by the framework (AddChatNotificationServices).
        // Only the OC-specific transport is registered here.
        services.AddChatNotificationServices();
        services.AddKeyedScoped<IChatNotificationTransport, AIChatNotificationTransport>(ChatContextType.AIChatSession);
        services.ConfigureChatHubOptions<AIChatHub>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        HubRouteManager.MapHub<AIChatHub>(routes);
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
            .AddScoped<AIChatSessionExtractedDataService>()
            .AddDataMigration<AIChatSessionExtractedDataMigrations>()
            .AddIndexProvider<AIChatSessionExtractedDataIndexProvider>()
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

        services.AddScoped<IAIChatSessionExtractedDataRecorder>(sp => sp.GetRequiredService<AIChatSessionExtractedDataService>());
    }
}
