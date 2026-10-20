using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Markets.Drivers;
using CrestApps.OrchardCore.Markets.Indexes;
using CrestApps.OrchardCore.Markets.Migrations;
using CrestApps.OrchardCore.Markets.Models;
using CrestApps.OrchardCore.Markets.Navigation;
using CrestApps.OrchardCore.Markets.Services;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Admin.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Markets;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Markets live in their own YesSql collection, which has to be declared to be stored.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(MarketsConstants.CollectionName));
        services.AddDataMigration<MarketMigrations>()
            .AddIndexProvider<MarketIndexProvider>();
        services.AddScoped<IMarketStore, MarketStore>();

        // The shopper's market decides the currency every price is shown and charged in.
        services.AddScoped<ShopperMarketService>();
        services.AddScoped<IShopperCurrency>(serviceProvider => serviceProvider.GetRequiredService<ShopperMarketService>());
        services.AddScoped<ICheckoutTaxProfileHandler, MarketCheckoutTaxProfileHandler>();

        // A product can be left out of markets: not sold to their shoppers.
        services.AddScoped<IProductSaleRestriction, MarketSaleRestriction>();
        services.AddContentPart<ProductPart>()
            .UseDisplayDriver<ProductMarketsDisplayDriver>();

        services.AddPermissionProvider<MarketsPermissionProvider>();
        services.AddNavigationProvider<MarketsAdminMenu>();
        services.AddDisplayDriver<Market, MarketDisplayDriver>();
        services.AddDisplayDriver<Navbar, MarketNavbarDisplayDriver>();
    }
}
