using CrestApps.OrchardCore.Commerce.Webhooks.BackgroundTasks;
using CrestApps.OrchardCore.Commerce.Webhooks.Drivers;
using CrestApps.OrchardCore.Commerce.Webhooks.Indexes;
using CrestApps.OrchardCore.Commerce.Webhooks.Migrations;
using CrestApps.OrchardCore.Commerce.Webhooks.Models;
using CrestApps.OrchardCore.Commerce.Webhooks.Navigation;
using CrestApps.OrchardCore.Commerce.Webhooks.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Commerce.Webhooks;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Webhooks and their deliveries live in their own YesSql collection, which has to be declared to be stored.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(WebhookConstants.CollectionName));
        services.AddDataMigration<WebhookMigrations>()
            .AddIndexProvider<WebhookSubscriptionIndexProvider>()
            .AddIndexProvider<WebhookDeliveryIndexProvider>();
        services.AddScoped<IWebhookSubscriptionStore, WebhookSubscriptionStore>();
        services.AddScoped<IWebhookDeliveryStore, WebhookDeliveryStore>();
        services.AddScoped<WebhookDispatcher>();
        services.AddScoped<ICommerceWebhookPublisher, CommerceWebhookPublisher>();
        services.AddSingleton<IBackgroundTask, WebhookDeliveriesBackgroundTask>();

        // A receiver that doesn't answer in time is tried again later rather than holding a worker.
        services.AddHttpClient(WebhookConstants.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(15));

        services.AddPermissionProvider<WebhookPermissionProvider>();
        services.AddNavigationProvider<WebhooksAdminMenu>();
        services.AddDisplayDriver<WebhookSubscription, WebhookSubscriptionDisplayDriver>();
        services.AddDisplayDriver<WebhookDelivery, WebhookDeliveryDisplayDriver>();
    }
}
