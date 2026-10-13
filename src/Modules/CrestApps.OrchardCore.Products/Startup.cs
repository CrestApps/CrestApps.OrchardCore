using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Products.Core;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.Deployments;
using CrestApps.OrchardCore.Products.Drivers;
using CrestApps.OrchardCore.Products.Handlers;
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

        services.AddScoped<IContentTypePartDefinitionDisplayDriver, ProductPartSettingsDisplayDriver>();
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

        services.AddScoped<ProductAttributeCatalog>();
        services.AddScoped<ProductSearchDocumentBuilder>();
        services.AddScoped<ProductSearchIndexer>();
        services.AddScoped<SqlProductSearchService>();
        services.AddScoped<IProductSearchService, CachedProductSearchService>();
        services.AddScoped<ProductListingService>();

        services.AddIndexProvider<ProductSearchIndexProvider>();
        services.AddScoped<IContentHandler, ProductSearchContentHandler>();
        services.AddSingleton<IBackgroundTask, ProductSearchIndexBackgroundTask>();

        services.AddContentPart<ProductListingPart>()
            .UseDisplayDriver<ProductListingPartDisplayDriver>();

        services.AddContentPart<ProductPart>()
            .UseDisplayDriver<ProductDetailsDisplayDriver>();

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
