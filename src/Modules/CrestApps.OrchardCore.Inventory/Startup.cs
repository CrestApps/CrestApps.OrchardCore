using CrestApps.OrchardCore.Commerce.Webhooks;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Inventory.Handlers;
using CrestApps.OrchardCore.Inventory.ContentTransfer;
using CrestApps.OrchardCore.Inventory.BackgroundTasks;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Deployments;
using CrestApps.OrchardCore.Inventory.Drivers;
using CrestApps.OrchardCore.Inventory.Endpoints;
using CrestApps.OrchardCore.Inventory.Migrations;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Inventory.Navigation;
using CrestApps.OrchardCore.Inventory.Recipes;
using CrestApps.OrchardCore.Inventory.Reports;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Inventory.Services;
using CrestApps.OrchardCore.Inventory.Workflows;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.Core.Transfer;
using CrestApps.OrchardCore.Inventory.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using OrchardCore.Workflows.Helpers;

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

        // Webhooks hear about stock when the Commerce Webhooks feature is on.
        services.AddScoped<IStockLevelHandler, StockWebhookHandler>();
        services.AddScoped<IStockLevelObserver, StockWebhookObserver>();
        services.AddCommerceWebhookEvent(CommerceWebhookEvents.StockChanged, "Stock changed", "Stock");
        services.AddCommerceWebhookEvent(CommerceWebhookEvents.StockLow, "Stock running low", "Stock");
        services.AddCommerceWebhookEvent(CommerceWebhookEvents.StockSoldOut, "Stock sold out", "Stock");
        services.AddScoped<InventoryLocationService>();
        services.AddScoped<IInventoryLocationService>(serviceProvider => serviceProvider.GetRequiredService<InventoryLocationService>());
        services.AddScoped<IFulfillmentLocationProvider>(serviceProvider => serviceProvider.GetRequiredService<InventoryLocationService>());
        services.AddScoped<InventoryProductSynchronizer>();
        services.AddScoped<InventorySyncService>();
        services.AddScoped<StockCountImporter>();

        // Running low, selling out and coming back are told to the stock level handlers once the change is committed.
        services.AddScoped<StockLevelTracker>();
        services.AddScoped<IStockLevelObserver>(serviceProvider => serviceProvider.GetRequiredService<StockLevelTracker>());
        services.AddScoped<StockLevelDispatcher>();

        // The cart, product pages and listings ask what can be bought.
        services.AddScoped<IProductAvailabilityProvider, InventoryAvailabilityProvider>();

        services.AddContentPart<InventoryPart>()
            .UseDisplayDriver<InventoryPartDisplayDriver>();

        // Stock items follow their products as they are saved.
        services.AddScoped<IContentHandler, InventoryContentHandler>();

        // The rows of the inventory admin list.
        services.AddDisplayDriver<StockItem, StockItemDisplayDriver>();

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

/// <summary>
/// Imports and exports how a product's stock is kept, and how many are available, with Content Transfer.
/// </summary>
[RequireFeatures(ContentTransferConstants.Feature.ModuleId)]
public sealed class ContentTransferStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<InventoryCountApplier>();
        services.AddContentPartImportHandler<InventoryPart, InventoryPartContentImportHandler>();
    }
}

/// <summary>
/// Works out a bundle's stock from its parts, and refreshes the bundles a product is in when its stock changes.
/// </summary>
[RequireFeatures(ProductConstants.Feature.Bundles)]
public sealed class BundlesStartup : StartupBase
{
    // Before the main startup, so bundles are answered for before the stock items.
    public override int Order => -10;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<InventoryAvailabilityProvider>();
        services.AddScoped<IProductAvailabilityProvider, BundleAvailabilityProvider>();
        services.AddScoped<IStockLevelHandler, BundleSearchIndexHandler>();
    }
}

/// <summary>
/// Says in product search records what is in stock, for the listings' "In stock" filter, and keeps it current.
/// </summary>
[RequireFeatures(ProductConstants.Feature.Listing)]
public sealed class ListingStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IProductSearchDocumentContributor, InventorySearchDocumentContributor>();
        services.AddScoped<IStockLevelHandler, StockSearchIndexHandler>();
    }
}

/// <summary>
/// Notifies the chosen roles when a product runs low or sells out.
/// </summary>
[RequireFeatures("OrchardCore.Notifications")]
public sealed class NotificationsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IStockLevelHandler, LowStockNotificationHandler>();
    }
}

[RequireFeatures("OrchardCore.Workflows")]
public sealed class WorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<LowStockEvent, LowStockEventDisplayDriver>();
        services.AddScoped<IStockLevelHandler, LowStockWorkflowHandler>();
    }
}

[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<StockStep>();
    }
}

[RequireFeatures("OrchardCore.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<StockDeploymentSource, StockDeploymentStep, StockDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Adds the stock on hand and stock movements reports to Reports > Commerce.
/// </summary>
[RequireFeatures(ReportsConstants.Feature)]
public sealed class ReportsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddReport<StockOnHandReport>();
        services.AddReport<StockMovementsReport>();
    }
}

/// <summary>
/// Lets shoppers ask to hear when a sold-out product is back, and emails them when it is.
/// </summary>
[Feature(InventoryConstants.Features.StockAlerts)]
public sealed class StockAlertsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<StockAlertMigrations>()
            .AddIndexProvider<StockAlertIndexProvider>();
        services.AddScoped<StockAlertService>();
        services.AddScoped<IStockLevelHandler, BackInStockAlertHandler>();
        services.AddScoped<CrestApps.OrchardCore.Customers.PersonalData.ICommercePersonalDataContributor, StockAlertPersonalDataContributor>();
        services.AddContentPart<ProductPart>()
            .UseDisplayDriver<StockAlertDisplayDriver>();
    }
}

/// <summary>
/// Orders stock from suppliers and receives it at a location.
/// </summary>
[Feature(InventoryConstants.Features.PurchaseOrders)]
public sealed class PurchaseOrdersStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<PurchaseOrderMigrations>()
            .AddIndexProvider<PurchaseOrderIndexProvider>();
        services.AddScoped<IPurchaseOrderStore, PurchaseOrderStore>();
        services.AddScoped<PurchaseOrderReceiver>();
        services.AddDisplayDriver<PurchaseOrder, PurchaseOrderDisplayDriver>();
        services.AddNavigationProvider<PurchaseOrdersAdminMenu>();
    }

    public override void Configure(Microsoft.AspNetCore.Builder.IApplicationBuilder app, Microsoft.AspNetCore.Routing.IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.AddStockItemSearchEndpoint();
}

[Feature(InventoryConstants.Features.Api)]
public sealed class ApiStartup : StartupBase
{
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapInventoryApi();
        routes.MapInventoryAdminApi();
    }
}
