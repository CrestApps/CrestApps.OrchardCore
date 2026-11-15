using CrestApps.OrchardCore.Learning.Classes.Drivers;
using CrestApps.OrchardCore.Learning.Classes.Migrations;
using CrestApps.OrchardCore.Learning.Classes.Models;
using CrestApps.OrchardCore.Learning.Classes.Services;
using CrestApps.OrchardCore.Learning.Classes.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Classes;

/// <summary>
/// Registers Learning classes: the class course item and its part, the class panel of the learner player, and the
/// sessions, roster and attendance pages of a class.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddLearningClassesCore();
        services.AddDataMigration<ClassesMigrations>();
        services.AddContentPart<ClassPart>()
            .UseDisplayDriver<ClassPartDisplayDriver>()
            .UseDisplayDriver<ClassPartLearnerDisplayDriver>();

        services.AddScoped<ClassLearnerResolver>();
        services.AddScoped<ClassesAdminScope>();
        services.AddPermissionProvider<ClassesPermissionProvider>();
        services.AddDisplayDriver<ClassSessionRow, ClassSessionRowDisplayDriver>();
        services.AddDisplayDriver<ClassRosterRow, ClassRosterRowDisplayDriver>();
        services.AddDisplayDriver<ClassWaitlistRow, ClassWaitlistRowDisplayDriver>();
    }
}
