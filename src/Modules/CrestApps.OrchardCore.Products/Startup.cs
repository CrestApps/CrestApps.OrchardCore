using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Products.ContentTransfer;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using CrestApps.OrchardCore.Products.Endpoints;
using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Products.Core;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Search;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.Deployments;
using CrestApps.OrchardCore.Products.Drivers;
using CrestApps.OrchardCore.Products.Handlers;
using CrestApps.OrchardCore.Products.Indexing;
using CrestApps.OrchardCore.Products.Indexes;
using CrestApps.OrchardCore.Products.Migrations;
using CrestApps.OrchardCore.Products.Models;
using CrestApps.OrchardCore.Products.Recipes;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Products.Services.Attributes;
using CrestApps.OrchardCore.Products.Tasks;
using CrestApps.OrchardCore.Products.ViewModels;
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Services;
using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.ContentTypes.Events;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.ResourceManagement;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Products;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCatalogs()
            .AddCatalogManagers();

        services.AddContentPart<ProductPart>()
            .UseDisplayDriver<ProductPartDisplayDriver>();

        services.AddContentPart<ProductPricePart>()
            .UseDisplayDriver<ProductPricePartDisplayDriver>();

        services.AddContentPart<ProductVariantsPart>()
            .UseDisplayDriver<ProductVariantsPartDisplayDriver>();

        services.AddScoped<IContentTypePartDefinitionDisplayDriver, ProductPartSettingsDisplayDriver>();
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, ProductVariantsPartSettingsDisplayDriver>();
        services.AddScoped<IProductCurrencyProvider, CurrencyCatalogService>();
        services.AddScoped<CurrencyUsageService>();
        services.AddDisplayDriver<CurrencyEntry, CurrencyEntryDisplayDriver>();
        services.AddScoped<ICatalogEntryHandler<CurrencyEntry>, CurrencyEntryHandler>();
        services.AddPermissionProvider<CurrencyPermissionProvider>();
        services.AddNavigationProvider<CurrencyAdminMenu>();

        // Products under Commerce: the content list narrowed to the product types, kept in a per-tenant cache.
        services.AddSingleton<ProductContentTypes>();
        services.AddSingleton<IContentDefinitionEventHandler>(serviceProvider => serviceProvider.GetRequiredService<ProductContentTypes>());
        services.AddNavigationProvider<ProductsAdminMenu>();

        services.AddScoped<IProductSnapshotResolver, DefaultProductSnapshotResolver>();
        services.AddScoped<IPriceResolver, DefaultPriceResolver>();

        services.AddDataMigration<ProductPartMigrations>();

        // Every product SKU, its own and its variants', so a product can be found by any of them.
        services.AddIndexProvider<ProductSkuIndexProvider>();
        services.AddDataMigration<ProductSkuMigrations>();
        services.AddDataMigration<CurrencyMigrations>();
    }
}

[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<CurrencyStep>();
    }
}

[RequireFeatures("OrchardCore.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<CurrencyDeploymentSource, CurrencyDeploymentStep, CurrencyDeploymentStepDisplayDriver>();
    }
}

[RequireFeatures(TaxationConstants.Feature.Taxation)]
public sealed class TaxationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // When taxation is enabled, expose products as taxable items with product-aware kind mapping.
        services.AddScoped<ITaxableItemProvider, ProductTaxableItemProvider>();
    }
}

/// <summary>
/// Lets shoppers compare up to four products side by side.
/// </summary>
[Feature(ProductConstants.Feature.Comparison)]
public sealed class ComparisonStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddContentPart<ProductPart>()
            .UseDisplayDriver<ProductCompareActionDisplayDriver>();
}

/// <summary>
/// Recommends products: upsells and cross-sells chosen per product, related products and recently viewed ones.
/// </summary>
[Feature(ProductConstants.Feature.Recommendations)]
public sealed class RecommendationsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<ProductPart>()
            .UseDisplayDriver<ProductRecommendationsDisplayDriver>();
    }
}

/// <summary>
/// Imports and exports products with Content Transfer: their prices, SKUs, variants and the rest as columns, and
/// Shopify's and WooCommerce's product exports read as those columns.
/// </summary>
[RequireFeatures(ContentTransferConstants.Feature.ModuleId)]
public sealed class ContentTransferStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPartImportHandler<ProductPart, ProductPartContentImportHandler>();
        services.AddContentPartImportHandler<ProductVariantsPart, ProductVariantsPartContentImportHandler>();
        services.AddScoped<IContentTransferFileReaderTransformer, StoreExportReaderTransformer>();
        services.AddScoped<IContentImportRowFilter, ProductImportRowFilter>();
    }
}

/// <summary>
/// Sells bundles made of other products.
/// </summary>
[Feature(ProductConstants.Feature.Bundles)]
public sealed class BundlesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<ProductPart>()
            .UseDisplayDriver<ProductBundleDisplayDriver>();
        services.AddIndexProvider<ProductBundleIndexProvider>();
        services.AddDataMigration<ProductBundleMigrations>();
    }
}

/// <summary>
/// Shows several products on one page for shoppers to buy together.
/// </summary>
[Feature(ProductConstants.Feature.Grouped)]
public sealed class GroupedStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddContentPart<ProductPart>()
            .UseDisplayDriver<ProductGroupDisplayDriver>();
}

[Feature(ProductConstants.Feature.Listing)]
public sealed class ListingStartup : StartupBase
{
    private readonly IStringLocalizer S;

    public ListingStartup(IStringLocalizer<ListingStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<TaxonomyTermLookup>();

        // The product search behind the merchant's product pickers: recommendations, groups and bundles.
        services.AddScoped<ProductRecommendationService>();

        // The styles a filter can be drawn in. Another module adds its own the same way, with a
        // ProductFacet-{Style} template.
        services.Configure<ProductFacetStyleOptions>(options =>
        {
            options.AddStyle(ProductFacetStyles.List, style => style.DisplayName = S["List"]);
            options.AddStyle(ProductFacetStyles.Buttons, style =>
            {
                style.DisplayName = S["Buttons"];
                style.Kinds.Add(ProductFacetKind.Checkbox);
                style.Kinds.Add(ProductFacetKind.Radio);
            });
            options.AddStyle(ProductFacetStyles.Swatches, style =>
            {
                style.DisplayName = S["Color swatches"];
                style.Kinds.Add(ProductFacetKind.Checkbox);
                style.Kinds.Add(ProductFacetKind.Radio);
            });
        });
        services.AddScoped<IProductAttributeValueProvider, TextFieldAttributeValueProvider>();
        services.AddScoped<IProductAttributeValueProvider, MultiTextFieldAttributeValueProvider>();
        services.AddScoped<IProductAttributeValueProvider, NumericFieldAttributeValueProvider>();
        services.AddScoped<IProductAttributeValueProvider, BooleanFieldAttributeValueProvider>();
        services.AddScoped<IProductAttributeValueProvider, DateFieldAttributeValueProvider>();
        services.AddScoped<IProductAttributeValueProvider, ContentPickerFieldAttributeValueProvider>();
        services.AddScoped<IProductAttributeValueProvider, TaxonomyFieldAttributeValueProvider>();
        services.AddScoped<IProductAttributeValueProvider, TimeFieldAttributeValueProvider>();
        services.AddScoped<IProductAttributeValueProvider, LinkFieldAttributeValueProvider>();
        services.AddScoped<IProductAttributeValueProvider, RichTextFieldAttributeValueProvider>();
        services.AddScoped<IProductAttributeValueProvider, UserPickerFieldAttributeValueProvider>();

        services.AddScoped<ProductAttributeCatalog>();
        services.AddScoped<ProductSearchDocumentBuilder>();
        services.AddScoped<ProductSearchIndexer>();
        services.AddScoped<IProductSearchIndexRefresher, ProductSearchIndexRefresher>();
        services.AddScoped<SqlProductSearchService>();
        services.AddScoped<ProductSearchRouter>();
        services.AddScoped<IProductSearchService, CachedProductSearchService>();
        services.AddScoped<ProductListingService>();
        services.AddScoped<IProductSearchDocumentContributor, ProductVariantsSearchDocumentContributor>();

        services.AddIndexProvider<ProductSearchIndexProvider>();
        services.AddScoped<IContentHandler, ProductSearchContentHandler>();
        services.AddSingleton<IBackgroundTask, ProductSearchIndexBackgroundTask>();
        services.AddSingleton<IBackgroundTask, ProductSaleScheduleBackgroundTask>();

        services.AddContentPart<ProductListingPart>()
            .UseDisplayDriver<ProductListingPartDisplayDriver>();

        services.AddContentPart<ProductPart>()
            .UseDisplayDriver<ProductDetailsDisplayDriver>();

        // An external product links to where it's sold instead of offering Add to cart.
        services.AddContentPart<ProductPart>()
            .UseDisplayDriver<ExternalProductActionDisplayDriver>();

        services.AddScoped<IContentPartFieldDefinitionDisplayDriver, ProductAttributeSettingsDisplayDriver>();
        services.AddScoped<IShapePlacementProvider, ProductDetailsPlacementProvider>();
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, ProductPageSettingsDisplayDriver>();

        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, ProductListingResourceManagementOptionsConfiguration>();

        services.AddDataMigration<ProductListingMigrations>();

        services.AddNavigationProvider<ProductListingsAdminMenu>();

        // Every listing and product page shape can be overridden in Liquid as well as Razor.
        services.Configure<TemplateOptions>(options =>
        {
            options.MemberAccessStrategy.Register<ProductListingViewModel>();
            options.MemberAccessStrategy.Register<ProductListingShapeViewModel>();
            options.MemberAccessStrategy.Register<ProductFacetViewModel>();
            options.MemberAccessStrategy.Register<ProductFacetOptionViewModel>();
            options.MemberAccessStrategy.Register<ProductActiveFilterViewModel>();
            options.MemberAccessStrategy.Register<ProductSortOptionViewModel>();
            options.MemberAccessStrategy.Register<ProductPageLinkViewModel>();
            options.MemberAccessStrategy.Register<ProductCardViewModel>();
            options.MemberAccessStrategy.Register<ProductPriceDisplayViewModel>();
            options.MemberAccessStrategy.Register<ProductSpecificationViewModel>();
            options.MemberAccessStrategy.Register<ProductBreadcrumbViewModel>();
            options.MemberAccessStrategy.Register<ProductBreadcrumbItemViewModel>();
            options.MemberAccessStrategy.Register<ProductDetailsViewModel>();
            options.MemberAccessStrategy.Register<ProductDetailsShapeViewModel>();
            options.MemberAccessStrategy.Register<ProductImageViewModel>();
            options.MemberAccessStrategy.Register<ProductSearchDocument>();
            options.MemberAccessStrategy.Register<ProductAttributeValue>();
            options.MemberAccessStrategy.Register<ProductListingPart>();
        });
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.AddProductSearchEndpoints();
}

/// <summary>
/// Keeps product search indexes held by search servers (Elasticsearch, Lucene, Azure AI Search) in step with the
/// product search records. A provider module adds the index type and the search for its server.
/// </summary>
[Feature(ProductConstants.Feature.Listing)]
[RequireFeatures("OrchardCore.Indexing")]
public sealed class ListingIndexingStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ProductSearchIndexingService>();
        services.AddScoped<IProductSearchIndexHandler, IndexProfileProductSearchIndexHandler>();
        services.AddScoped<IDocumentIndexHandler, ProductSearchDocumentIndexHandler>();
        services.AddIndexProfileHandler<ProductSearchIndexProfileHandler>();
    }
}

[Feature(ProductConstants.Feature.Listing)]
[RequireFeatures("OrchardCore.Seo")]
public sealed class ListingSeoStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentHandler, ProductListingSeoHandler>();
    }
}
