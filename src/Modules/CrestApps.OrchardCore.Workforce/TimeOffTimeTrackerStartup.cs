using CrestApps.OrchardCore.Workforce.BackgroundTasks;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// Keeps approved time off in Time Tracker when it is on: each type gets a stage, and each day off is kept in it.
/// </summary>
[Feature(WorkforceConstants.Features.TimeOff)]
[RequireFeatures("CrestApps.OrchardCore.TimeTracker")]
public sealed class TimeOffTimeTrackerStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWorkforceTimeOffTimeTracker();
        services.AddOptions<TimeOffTimeTrackerOptions>()
            .Configure<IOptions<AdminOptions>>((options, admin) => options.ManageUrl = "~/" + admin.Value.AdminUrlPrefix + "/workforce/time-off");
        services.AddDataMigration<TimeOffTimeTrackerMigrations>();
        services.AddSingleton<IBackgroundTask, TimeOffTimeTrackerBackgroundTask>();
    }
}
