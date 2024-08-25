using CrestApps.OrchardCore.Payments.Core.Models;
using CrestApps.OrchardCore.Products.Drivers;
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

        services.AddDataMigration<ProductPartMigrations>();
    }
}
