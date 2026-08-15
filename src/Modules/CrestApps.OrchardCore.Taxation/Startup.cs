using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Taxation.Drivers;
using CrestApps.OrchardCore.Taxation.Migrations;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Taxation;

/// <summary>
/// Registers the taxation framework services, the <see cref="TaxationPart"/>, and its content
/// integration.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTaxationCore();

        services.AddContentPart<TaxationPart>()
            .UseDisplayDriver<TaxationPartDisplayDriver>();

        services.AddScoped<IContentTypePartDefinitionDisplayDriver, TaxationPartSettingsDisplayDriver>();

        services.AddDataMigration<TaxationPartMigrations>();

        services.AddTaxableItemProvider<ContentItemTaxableItemProvider>();
    }
}
