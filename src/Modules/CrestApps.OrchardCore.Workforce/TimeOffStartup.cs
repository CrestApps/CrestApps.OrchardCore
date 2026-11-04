using CrestApps.OrchardCore.Workforce.BackgroundTasks;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Drivers;
using CrestApps.OrchardCore.Workforce.Migrations;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Navigation;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// Registers time-off types, policies, balances, requests, allotments and blackout dates, the nightly accrual, and
/// their admin screens.
/// </summary>
[Feature(WorkforceConstants.Features.TimeOff)]
public sealed class TimeOffStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWorkforceTimeOffCore();
        services.AddDataMigration<TimeOffMigrations>()
            .AddIndexProvider<TimeOffTypeIndexProvider>()
            .AddIndexProvider<TimeOffPolicyIndexProvider>()
            .AddIndexProvider<TimeOffPolicyAssignmentIndexProvider>()
            .AddIndexProvider<TimeOffLedgerEntryIndexProvider>()
            .AddIndexProvider<TimeOffRequestIndexProvider>()
            .AddIndexProvider<TimeOffAllotmentIndexProvider>()
            .AddIndexProvider<BlackoutPeriodIndexProvider>();

        services.AddDisplayDriver<TimeOffRequestRowViewModel, TimeOffRequestRowDisplayDriver>();
        services.AddDisplayDriver<TimeOffType, TimeOffTypeRowDisplayDriver>();
        services.AddDisplayDriver<TimeOffPolicy, TimeOffPolicyRowDisplayDriver>();
        services.AddDisplayDriver<TimeOffAllotment, TimeOffAllotmentRowDisplayDriver>();
        services.AddDisplayDriver<BlackoutPeriod, BlackoutPeriodRowDisplayDriver>();
        services.AddDisplayDriver<TimeOffLedgerRowViewModel, TimeOffLedgerRowDisplayDriver>();
        services.AddDisplayDriver<WorkerProfile, WorkerTimeOffRowDisplayDriver>();
        services.AddSingleton<IBackgroundTask, TimeOffAccrualBackgroundTask>();
        services.AddPermissionProvider<TimeOffPermissionsProvider>();
        services.AddNavigationProvider<TimeOffAdminMenu>();
    }
}
