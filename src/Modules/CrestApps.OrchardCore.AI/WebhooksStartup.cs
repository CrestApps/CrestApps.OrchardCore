using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Webhooks;
using CrestApps.OrchardCore.AI.Drivers;
using CrestApps.OrchardCore.AI.Migrations;
using CrestApps.OrchardCore.AI.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Registers the AI Webhooks feature: subscriptions, the delivery log and the background sender.
/// </summary>
[Feature(AIConstants.Feature.Webhooks)]
public sealed class WebhooksStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IAIWebhookSubscriptionStore, AIWebhookSubscriptionStore>()
            .AddScoped<IAIWebhookDeliveryStore, AIWebhookDeliveryStore>()
            .AddScoped<AIWebhookPublisher>()
            .AddScoped<AIWebhookSender>()
            .AddIndexProvider<AIWebhookSubscriptionIndexProvider>()
            .AddIndexProvider<AIWebhookDeliveryIndexProvider>()
            .AddDataMigration<AIWebhookIndexMigrations>()
            .AddDisplayDriver<AIWebhookSubscription, AIWebhookSubscriptionDisplayDriver>()
            .AddDisplayDriver<AIWebhookDelivery, AIWebhookDeliveryDisplayDriver>()
            .AddNavigationProvider<AIWebhooksAdminMenu>()
            .AddPermissionProvider<AIWebhooksPermissionsProvider>();

        services.AddScoped<IAIConversationStartingHandler, AIWebhookConversationStartingHandler>();
        services.AddHttpClient(AIWebhookSender.HttpClientName);
        services.AddSingleton<IBackgroundTask, AIWebhookDeliveryBackgroundTask>();
    }
}
