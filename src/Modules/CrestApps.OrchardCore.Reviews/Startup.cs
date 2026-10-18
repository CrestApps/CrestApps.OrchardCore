using CrestApps.OrchardCore.Products.Core;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Reviews.Drivers;
using CrestApps.OrchardCore.Reviews.Indexes;
using CrestApps.OrchardCore.Reviews.Migrations;
using CrestApps.OrchardCore.Reviews.Models;
using CrestApps.OrchardCore.Reviews.Navigation;
using CrestApps.OrchardCore.Reviews.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using CrestApps.OrchardCore.Reviews.Reports;
using CrestApps.OrchardCore.Reports;

namespace CrestApps.OrchardCore.Reviews;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Reviews live in their own collection, which has to be declared for its documents to be stored.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(ReviewsConstants.CollectionName));
        services.AddDataMigration<ReviewMigrations>()
            .AddIndexProvider<ProductReviewIndexProvider>();

        services.AddScoped<ReviewService>();
        services.AddScoped<ProductRatingReader>();
        services.AddContentPart<ProductPart>()
            .UseDisplayDriver<ProductReviewsDisplayDriver>();
        services.AddDisplayDriver<ProductReview, ProductReviewDisplayDriver>();

        services.AddSiteDisplayDriver<ReviewSettingsDisplayDriver>();
        services.AddNavigationProvider<ReviewsAdminMenu>();
        services.AddPermissionProvider<ReviewsPermissionsProvider>();
        services.AddSiteSettingsPermission(ReviewsConstants.SettingsGroupId, ReviewsPermissions.ManageReviews);
    }
}

/// <summary>
/// Puts products' ratings in their search records, and adds "Customer rating" to automatic filters and sorts.
/// </summary>
[RequireFeatures(ProductConstants.Feature.Listing)]
public sealed class ListingStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IProductSearchDocumentContributor, ReviewSearchDocumentContributor>();
        services.AddScoped<IProductListingEntryProvider, ReviewListingEntryProvider>();
    }
}

/// <summary>
/// Adds the product reviews report to Reports > Commerce.
/// </summary>
[RequireFeatures(ReportsConstants.Feature)]
public sealed class ReportsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddReport<ProductReviewsReport>();
}
