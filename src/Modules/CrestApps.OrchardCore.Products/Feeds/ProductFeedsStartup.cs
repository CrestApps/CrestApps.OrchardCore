using CrestApps.OrchardCore.Products.Core;
using CrestApps.OrchardCore.Products.Services.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Products.Feeds;

/// <summary>
/// Registers product feeds: the store's products as Google Merchant Center XML and as CSV, which Meta and TikTok
/// catalogs read too, and the Product Feed part for brands and identifiers.
/// </summary>
[Feature(ProductConstants.Feature.Feeds)]
public sealed class ProductFeedsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ProductFeedBuilder>();
        services.TryAddScoped<TaxonomyTermLookup>();
        services.AddScoped<ProductFeedService>();
        services.AddDataMigration<Migrations.ProductFeedMigrations>();
        services.AddContentPart<ProductFeedPart>()
            .UseDisplayDriver<ProductFeedPartDisplayDriver>();
        services.AddSiteDisplayDriver<ProductFeedSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(ProductFeedSettingsDisplayDriver.GroupId, ProductFeedPermissions.ManageProductFeeds);
        services.AddPermissionProvider<ProductFeedPermissionProvider>();
        services.AddNavigationProvider<ProductFeedsAdminMenu>();
    }
}
