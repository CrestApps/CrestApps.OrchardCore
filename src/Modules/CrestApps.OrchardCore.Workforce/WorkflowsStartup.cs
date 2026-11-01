using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// Starts workflows from Workforce events: time off, schedules, punches, attendance and timesheets.
/// </summary>
[RequireFeatures("OrchardCore.Workflows")]
public sealed class WorkflowsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IWorkforceEvents, WorkflowWorkforceEvents>());

        services.AddActivity<WorkforceTimeOffRequestedEvent, WorkforceEventActivityDisplayDriver<WorkforceTimeOffRequestedEvent>>();
        services.AddActivity<WorkforceTimeOffApprovedEvent, WorkforceEventActivityDisplayDriver<WorkforceTimeOffApprovedEvent>>();
        services.AddActivity<WorkforceTimeOffDeclinedEvent, WorkforceEventActivityDisplayDriver<WorkforceTimeOffDeclinedEvent>>();
        services.AddActivity<WorkforceTimeOffCanceledEvent, WorkforceEventActivityDisplayDriver<WorkforceTimeOffCanceledEvent>>();
        services.AddActivity<WorkforceSchedulePublishedEvent, WorkforceEventActivityDisplayDriver<WorkforceSchedulePublishedEvent>>();
        services.AddActivity<WorkforcePunchedInEvent, WorkforceEventActivityDisplayDriver<WorkforcePunchedInEvent>>();
        services.AddActivity<WorkforcePunchedOutEvent, WorkforceEventActivityDisplayDriver<WorkforcePunchedOutEvent>>();
        services.AddActivity<WorkforceAttendanceExceptionRaisedEvent, WorkforceEventActivityDisplayDriver<WorkforceAttendanceExceptionRaisedEvent>>();
        services.AddActivity<WorkforceAttendancePointsThresholdReachedEvent, WorkforceEventActivityDisplayDriver<WorkforceAttendancePointsThresholdReachedEvent>>();
        services.AddActivity<WorkforceTimesheetSubmittedEvent, WorkforceEventActivityDisplayDriver<WorkforceTimesheetSubmittedEvent>>();
        services.AddActivity<WorkforceTimesheetApprovedEvent, WorkforceEventActivityDisplayDriver<WorkforceTimesheetApprovedEvent>>();
    }
}
