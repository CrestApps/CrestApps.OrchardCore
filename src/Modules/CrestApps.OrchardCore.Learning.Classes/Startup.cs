using CrestApps.OrchardCore.Learning.Classes.Core.Services;
using CrestApps.OrchardCore.Learning.Classes.Drivers;
using CrestApps.OrchardCore.Learning.Classes.Migrations;
using CrestApps.OrchardCore.Learning.Classes.Models;
using CrestApps.OrchardCore.Learning.Classes.Services;
using CrestApps.OrchardCore.Learning.Classes.ViewModels;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Workforce;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
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

        // The queue that retries class registration work keeps its items in a collection of its own; the store creates
        // the document table only for a registered collection.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(ClassFollowUpWork.CollectionName));

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
        services.AddSingleton<IBackgroundTask, ClassFollowUpBackgroundTask>();
    }
}

/// <summary>
/// Puts a staff member's confirmed class meetings on their published shifts as Training segments, and takes them off
/// when the place is cancelled, when Workforce is enabled.
/// </summary>
[RequireFeatures(WorkforceConstants.Features.Workforce)]
public sealed class WorkforceStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<ClassShiftSegmentPlanner>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAppointmentObserver, ClassShiftSegmentObserver>());
    }
}
