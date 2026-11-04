using CrestApps.OrchardCore.Commerce.Webhooks.Migrations;
using CrestApps.OrchardCore.Webhooks.BackgroundTasks;
using CrestApps.OrchardCore.Webhooks.Core;
using CrestApps.OrchardCore.Webhooks.Core.Indexes;
using CrestApps.OrchardCore.Webhooks.Core.Models;
using CrestApps.OrchardCore.Webhooks.Drivers;
using CrestApps.OrchardCore.Webhooks.Navigation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Webhooks;

/// <summary>
/// Registers the <b>Webhooks</b> feature: subscriptions, the delivery outbox and its retry task, the publisher modules
/// send their events through, and the administration under Configuration › Webhooks.
/// </summary>
[Feature(WebhooksConstants.Feature.Webhooks)]
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWebhooks();

        services
            .AddDataMigration<WebhookMigrations>()
            .AddIndexProvider<WebhookSubscriptionIndexProvider>()
            .AddIndexProvider<WebhookDeliveryIndexProvider>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, WebhookDeliveriesBackgroundTask>());

        services.AddPermissionProvider<WebhookPermissionProvider>();
        services.AddNavigationProvider<WebhooksAdminMenu>();
        services.AddDisplayDriver<WebhookSubscription, WebhookSubscriptionDisplayDriver>();
        services.AddDisplayDriver<WebhookDelivery, WebhookDeliveryDisplayDriver>();
    }
}
