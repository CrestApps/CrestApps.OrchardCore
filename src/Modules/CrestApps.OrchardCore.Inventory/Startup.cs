using CrestApps.OrchardCore.Inventory.BackgroundTasks;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Drivers;
using CrestApps.OrchardCore.Inventory.Handlers;
using CrestApps.OrchardCore.Inventory.Migrations;
using CrestApps.OrchardCore.Inventory.Navigation;
using CrestApps.OrchardCore.Inventory.Services;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Inventory;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Stock items and movements live in their own YesSql collection, which has to be declared for them to be stored.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(InventoryConstants.CollectionName));
        services.AddDataMigration<InventoryMigrations>()
            .AddIndexProvider<StockItemIndexProvider>()
            .AddIndexProvider<StockMovementIndexProvider>()
            .AddIndexProvider<StockReservationIndexProvider>();

        services.AddScoped<IStockItemStore, StockItemStore>();
        services.AddScoped<IStockMovementStore, StockMovementStore>();
        services.AddScoped<IInventoryService, DefaultInventoryService>();
        services.AddScoped<InventoryProductSynchronizer>();
        services.AddScoped<InventorySyncService>();

        services.AddContentPart<InventoryPart>()
            .UseDisplayDriver<InventoryPartDisplayDriver>();

        // Stock items follow their products as they are saved.
        services.AddScoped<IContentHandler, InventoryContentHandler>();

        services.AddSiteDisplayDriver<InventorySettingsDisplayDriver>();

        // Lets a role with Manage inventory, but no site-settings permission, open Settings > Commerce > Inventory.
        services.AddSiteSettingsPermission(InventoryConstants.SettingsGroupId, InventoryPermissions.ManageInventory);
        services.AddScoped<IPermissionProvider, InventoryPermissionsProvider>();
        services.AddNavigationProvider<InventoryAdminMenu>();
    }
}

/// <summary>
/// Holds stock for orders while they are paid for, sells it when they are paid, and releases it otherwise.
/// </summary>
[RequireFeatures(OrderConstants.Features.Orders)]
public sealed class OrdersStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IOrderEventHandler, InventoryOrderEventHandler>();
        services.AddSingleton<IBackgroundTask, ExpiredStockHoldsBackgroundTask>();
    }
}
