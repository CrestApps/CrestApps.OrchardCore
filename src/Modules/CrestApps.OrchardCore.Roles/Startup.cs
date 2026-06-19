using CrestApps.OrchardCore.Roles.Core.Models;
using CrestApps.OrchardCore.Roles.Drivers;
using CrestApps.OrchardCore.Roles.Migrations;
using CrestApps.OrchardCore.Roles.Schemas;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Roles;

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IContentTypePartDefinitionDisplayDriver, RolePickerPartSettingsDisplayDriver>()
            .AddContentPart<RolePickerPart>()
            .UseDisplayDriver<RolePickerPartDisplayDriver>();

        services.AddDataMigration<RolePickerMigrations>();
    }
}

/// <summary>
/// Registers recipe schema contributors for the Roles feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Recipes")]
public sealed class RecipesSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, RolePickerPartSchemaDefinition>();
    }
}
