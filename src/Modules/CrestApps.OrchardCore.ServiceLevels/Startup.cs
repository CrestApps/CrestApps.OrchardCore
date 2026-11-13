using CrestApps.OrchardCore.BusinessHours.Core;
using CrestApps.OrchardCore.ServiceLevels.BackgroundTasks;
using CrestApps.OrchardCore.ServiceLevels.Core;
using CrestApps.OrchardCore.ServiceLevels.Core.Indexes;
using CrestApps.OrchardCore.ServiceLevels.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ServiceLevels;

/// <summary>
/// Registers the Service Levels feature: the stores and indexes, the tracker, and the monitor task.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(ServiceLevelsConstants.CollectionName));
        services.AddDataMigration<ServiceLevelsMigrations>()
            .AddIndexProvider<ServiceLevelPolicyIndexProvider>()
            .AddIndexProvider<ServiceLevelTimerIndexProvider>();

        // Timers count on Business Hours schedules; with no schedule source enabled they count around the clock.
        services.AddOpenHours();
        services.AddServiceLevelsCore();

        services.AddPermissionProvider<ServiceLevelsPermissionsProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, ServiceLevelMonitorBackgroundTask>());
    }
}
