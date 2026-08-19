using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.Deployments;
using CrestApps.OrchardCore.Products.Drivers;
using CrestApps.OrchardCore.Products.Handlers;
using CrestApps.OrchardCore.Products.Migrations;
using CrestApps.OrchardCore.Products.Models;
using CrestApps.OrchardCore.Products.Recipes;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
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

        services.AddScoped<IContentTypePartDefinitionDisplayDriver, ProductPartSettingsDisplayDriver>();
        services.AddScoped<IProductCurrencyProvider, CurrencyCatalogService>();
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
