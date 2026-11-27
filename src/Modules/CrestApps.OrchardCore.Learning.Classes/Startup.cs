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
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Notifications;
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
        services.AddDisplayDriver<ClassSessionEditViewModel, ClassSessionEditDisplayDriver>();
        services.AddDisplayDriver<ClassSessionCancelViewModel, ClassSessionCancelDisplayDriver>();
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

/// <summary>
/// The notification types of class notices: a cancelled or moved session, a place someone else gave, a freed place.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Notifications)]
public sealed class NotificationsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNotificationType(ClassesConstants.SessionCancelledNotificationType, LocalizationSource.Create<NotificationsStartup>("Class session cancelled"), type => type.Description = LocalizationSource.Create<NotificationsStartup>("When staff cancel a class session you had a place in."));
        services.AddNotificationType(ClassesConstants.NotificationTypes.Registered, LocalizationSource.Create<NotificationsStartup>("Class place given"), type => type.Description = LocalizationSource.Create<NotificationsStartup>("When someone else gives you a place in a class session."));
        services.AddNotificationType(ClassesConstants.NotificationTypes.PlaceOffered, LocalizationSource.Create<NotificationsStartup>("Class place free"), type => type.Description = LocalizationSource.Create<NotificationsStartup>("When a place frees up in a full class session you wait for."));
        services.AddNotificationType(ClassesConstants.NotificationTypes.SessionMoved, LocalizationSource.Create<NotificationsStartup>("Class session moved"), type => type.Description = LocalizationSource.Create<NotificationsStartup>("When staff move a class session you have a place in to other times."));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAppointmentObserver, ClassNotificationObserver>());
    }
}
