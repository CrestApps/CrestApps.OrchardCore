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
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentTypes.Editors;
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
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<TaxonomyTermLookup>();
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
        services.AddScoped<IProductSearchService, SqlProductSearchService>();
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
