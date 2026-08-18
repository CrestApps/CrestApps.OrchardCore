using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.Drivers;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Products;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<ProductPart>()
            .UseDisplayDriver<ProductPartDisplayDriver>();

        services.AddScoped<IContentTypePartDefinitionDisplayDriver, ProductPartSettingsDisplayDriver>();

        services.AddScoped<IProductSnapshotResolver, DefaultProductSnapshotResolver>();
        services.AddScoped<IPriceResolver, DefaultPriceResolver>();

        services.AddDataMigration<ProductPartMigrations>();
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
