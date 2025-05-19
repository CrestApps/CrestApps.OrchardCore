using CrestApps.OrchardCore.Roles.Core.Models;
using CrestApps.OrchardCore.Roles.Drivers;
using CrestApps.OrchardCore.Roles.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Roles;

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

