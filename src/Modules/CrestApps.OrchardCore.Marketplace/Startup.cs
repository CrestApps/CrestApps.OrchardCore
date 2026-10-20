using CrestApps.OrchardCore.Marketplace.BackgroundTasks;
using CrestApps.OrchardCore.Marketplace.Drivers;
using CrestApps.OrchardCore.Marketplace.Handlers;
using CrestApps.OrchardCore.Marketplace.Indexes;
using CrestApps.OrchardCore.Marketplace.Migrations;
using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Marketplace.Navigation;
using CrestApps.OrchardCore.Marketplace.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Marketplace;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Vendors, their earnings and payouts live in their own YesSql collection, which has to be declared to be stored.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(MarketplaceConstants.CollectionName));
        services.AddDataMigration<MarketplaceMigrations>()
            .AddIndexProvider<VendorIndexProvider>()
            .AddIndexProvider<VendorEarningIndexProvider>()
            .AddIndexProvider<VendorPayoutIndexProvider>();
        services.AddScoped<IVendorStore, VendorStore>();
        services.AddScoped<IVendorEarningStore, VendorEarningStore>();
        services.AddScoped<IVendorPayoutStore, VendorPayoutStore>();
        services.AddScoped<MarketplaceService>();
        services.AddScoped<IOrderEventHandler, MarketplaceOrderEventHandler>();
        services.AddSingleton<IBackgroundTask, MarketplaceBackgroundTask>();

        services.AddContentPart<VendorPart>()
            .UseDisplayDriver<VendorPartDisplayDriver>();
        services.AddDisplayDriver<Vendor, VendorDisplayDriver>();
        services.AddSiteDisplayDriver<MarketplaceSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(MarketplaceConstants.SettingsGroupId, MarketplacePermissions.ManageMarketplace);

        services.AddPermissionProvider<MarketplacePermissionProvider>();
        services.AddNavigationProvider<MarketplaceAdminMenu>();
        services.AddNavigationProvider<MarketplaceAccountMenu>();
    }
}

/// <summary>
/// Pays vendors out to their Stripe Connect accounts.
/// </summary>
[Feature(MarketplaceConstants.StripeConnectFeature)]
public sealed class StripeConnectStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<StripeConnectVendorService>();
        services.AddScoped<IVendorPayoutProvider, StripeConnectPayoutProvider>();
    }
}
