using CrestApps.Core.Handlers;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Shipping.Deployments;
using CrestApps.OrchardCore.Shipping.Drivers;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Shipping.Handlers;
using CrestApps.OrchardCore.Shipping.Indexes;
using CrestApps.OrchardCore.Shipping.Workflows;
using CrestApps.OrchardCore.Shipping.Migrations;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Navigation;
using CrestApps.OrchardCore.Shipping.Recipes;
using CrestApps.OrchardCore.Shipping.Reports;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Shipping.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
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

namespace CrestApps.OrchardCore.Shipping;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddShippingCore();

        // Shipments and their index live in their own YesSql collection, which has to be declared for them to be stored.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(ShippingConstants.CollectionName));
        services.AddDataMigration<ShippingMigrations>()
            .AddIndexProvider<ShipmentIndexProvider>();

        services.AddContentPart<ShippingPart>()
            .UseDisplayDriver<ShippingPartDisplayDriver>();

        services
            .AddDisplayDriver<ShippingZone, ShippingZoneDisplayDriver>()
            .AddScoped<ICatalogEntryHandler<ShippingZone>, ShippingZoneHandler>();

        // The method editor: the fields every method has, then the provider's own.
        services
            .AddDisplayDriver<ShippingMethod, ShippingMethodDisplayDriver>()
            .AddDisplayDriver<ShippingMethod, FlatRateMethodDisplayDriver>()
            .AddDisplayDriver<ShippingMethod, TableRateMethodDisplayDriver>()
            .AddDisplayDriver<ShippingMethod, FreeShippingMethodDisplayDriver>()
            .AddDisplayDriver<ShippingMethod, LocalDeliveryMethodDisplayDriver>()
            .AddDisplayDriver<ShippingMethod, CarrierRateMethodDisplayDriver>();

        // Settings > Commerce > Shipping: the default carrier provider first, then each provider's tab (from its own
        // feature), then the store's general shipping settings.
        services.AddSiteDisplayDriver<CarrierProviderSettingsDisplayDriver>();
        services.AddSiteDisplayDriver<ShippingSettingsDisplayDriver>();

        // The Shipping step of an order's checkout, after Contact.
        services.AddScoped<ShippingOrderRateService>();
        services.AddScoped<ICheckoutHandler, ShippingCheckoutHandler>();
        services.AddDisplayDriver<CheckoutFlow, ShippingCheckoutFlowDisplayDriver>();

        // Shipments on the order's admin page and on the buyer's order pages, and the email that a shipment left.
        services.AddDisplayDriver<Order, OrderShipmentsDisplayDriver>();
        services.AddScoped<ShipmentEmailBuilder>();
        services.AddScoped<IShipmentEventHandler, ShippedEmailShipmentHandler>();

        // Lets a role with Manage shipping, but no site-settings permission, open Settings > Commerce > Shipping.
        services.AddSiteSettingsPermission(ShippingConstants.SettingsGroupId, ShippingPermissions.ManageShipping);
        services.AddPermissionProvider<ShippingPermissionsProvider>();
        services.AddNavigationProvider<ShippingAdminMenu>();
    }
}

[RequireFeatures("OrchardCore.Workflows")]
public sealed class WorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<ShipmentCreatedEvent, ShipmentEventDisplayDriver<ShipmentCreatedEvent>>();
        services.AddActivity<ShipmentShippedEvent, ShipmentEventDisplayDriver<ShipmentShippedEvent>>();
        services.AddActivity<ShipmentDeliveredEvent, ShipmentEventDisplayDriver<ShipmentDeliveredEvent>>();
        services.AddActivity<ShipmentReturnedEvent, ShipmentEventDisplayDriver<ShipmentReturnedEvent>>();
        services.AddScoped<IShipmentEventHandler, ShipmentWorkflowHandler>();
    }
}

[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<ShippingZonesStep>();
    }
}

[RequireFeatures("OrchardCore.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<ShippingZonesDeploymentSource, ShippingZonesDeploymentStep, ShippingZonesDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Adds the shipping times report to Reports > Commerce.
/// </summary>
[RequireFeatures(ReportsConstants.Feature)]
public sealed class ReportsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddReport<ShippingPerformanceReport>();
}

/// <summary>
/// The carriers' rates, labels and tracking through an EasyPost account.
/// </summary>
[Feature(ShippingConstants.Features.EasyPost)]
public sealed class EasyPostStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(EasyPostCarrierProvider.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://api.easypost.com/v2/");
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        services.AddScoped<EasyPostCarrierProvider>();
        services.AddScoped<ICarrierProvider>(serviceProvider => serviceProvider.GetRequiredService<EasyPostCarrierProvider>());
        services.AddScoped<EasyPostTrackingUpdater>();
        services.AddSiteDisplayDriver<EasyPostSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(ShippingConstants.SettingsGroupId, ShippingPermissions.ManageShipping);
    }
}
