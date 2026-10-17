using CrestApps.Core.Handlers;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Shipping.Deployments;
using CrestApps.OrchardCore.Shipping.Drivers;
using CrestApps.OrchardCore.Shipping.Handlers;
using CrestApps.OrchardCore.Shipping.Migrations;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Navigation;
using CrestApps.OrchardCore.Shipping.Recipes;
using CrestApps.OrchardCore.Shipping.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Shipping;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddShippingCore();
        services.AddDataMigration<ShippingMigrations>();

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
            .AddDisplayDriver<ShippingMethod, FreeShippingMethodDisplayDriver>();

        services.AddSiteDisplayDriver<ShippingSettingsDisplayDriver>();

        // The Shipping step of an order's checkout, after Contact.
        services.AddScoped<ShippingOrderRateService>();
        services.AddScoped<ICheckoutHandler, ShippingCheckoutHandler>();
        services.AddDisplayDriver<CheckoutFlow, ShippingCheckoutFlowDisplayDriver>();

        // Lets a role with Manage shipping, but no site-settings permission, open Settings > Commerce > Shipping.
        services.AddSiteSettingsPermission(ShippingConstants.SettingsGroupId, ShippingPermissions.ManageShipping);
        services.AddPermissionProvider<ShippingPermissionsProvider>();
        services.AddNavigationProvider<ShippingAdminMenu>();
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
