using CrestApps.OrchardCore.Workforce.BackgroundTasks;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Drivers;
using CrestApps.OrchardCore.Workforce.Migrations;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Navigation;
using CrestApps.OrchardCore.Workforce.Portal;
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
/// Registers the time clock: punches, timesheets, attendance, payroll export and their admin screens.
/// </summary>
[Feature(WorkforceConstants.Features.TimeClock)]
public sealed class TimeClockStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWorkforceTimeClockCore();
        services.AddScoped<TimesheetViews>();
        services.AddDataMigration<TimeClockMigrations>()
            .AddIndexProvider<TimeClockPolicyIndexProvider>()
            .AddIndexProvider<PayPeriodCalendarIndexProvider>()
            .AddIndexProvider<PayCodeIndexProvider>()
            .AddIndexProvider<TimeClockKioskIndexProvider>()
            .AddIndexProvider<TimePunchIndexProvider>()
            .AddIndexProvider<TimesheetIndexProvider>()
            .AddIndexProvider<AttendanceExceptionIndexProvider>()
            .AddIndexProvider<PunchCorrectionIndexProvider>()
            .AddIndexProvider<PayrollExportBatchIndexProvider>();

        services.AddDisplayDriver<TimeClockPolicy, TimeClockPolicyRowDisplayDriver>();
        services.AddDisplayDriver<PayPeriodCalendar, PayPeriodCalendarRowDisplayDriver>();
        services.AddDisplayDriver<PayCode, PayCodeRowDisplayDriver>();
        services.AddDisplayDriver<TimeClockKiosk, TimeClockKioskRowDisplayDriver>();
        services.AddDisplayDriver<TimesheetRowViewModel, TimesheetRowDisplayDriver>();
        services.AddDisplayDriver<AttendanceRowViewModel, AttendanceRowDisplayDriver>();
        services.AddDisplayDriver<PayrollExportBatch, PayrollExportBatchRowDisplayDriver>();
        services.AddDisplayDriver<WorkerProfile, WorkerTimeClockRowDisplayDriver>();
        services.AddSingleton<IBackgroundTask, TimesheetRefreshBackgroundTask>();
        services.AddPermissionProvider<TimeClockPermissionsProvider>();
        services.AddNavigationProvider<TimeClockAdminMenu>();
    }
}
