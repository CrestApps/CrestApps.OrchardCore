using CrestApps.OrchardCore.Fulfillment.BackgroundTasks;
using CrestApps.OrchardCore.Fulfillment.Core.Indexes;
using CrestApps.OrchardCore.Fulfillment.Drivers;
using CrestApps.OrchardCore.Fulfillment.Migrations;
using CrestApps.OrchardCore.Fulfillment.Navigation;
using CrestApps.OrchardCore.Orders.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Fulfillment;

/// <summary>
/// Registers the Fulfillment partners settings page, the record of orders sent to partners, the order card and the
/// background sender.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(FulfillmentConstants.CollectionName));
        services.AddFulfillmentCore();
        services
            .AddIndexProvider<FulfillmentRequestIndexProvider>()
            .AddDataMigration<FulfillmentMigrations>();

        services.AddPermissionProvider<FulfillmentPermissionsProvider>();
        services.AddSiteDisplayDriver<FulfillmentPartnersDisplayDriver>();
        services.AddSiteDisplayDriver<FulfillmentSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(FulfillmentConstants.SettingsGroupId, FulfillmentPermissions.ManageFulfillmentPartners);
        services.AddDisplayDriver<Order, OrderFulfillmentDisplayDriver>();
        services.AddNavigationProvider<FulfillmentAdminMenu>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, FulfillmentBackgroundTask>());
    }
}
