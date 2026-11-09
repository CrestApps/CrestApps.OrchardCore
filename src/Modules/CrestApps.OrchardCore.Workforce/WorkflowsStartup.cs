using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Workforce.Recipes.Workflows;
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
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
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
        services.AddActivity<WorkforceShiftAssignedEvent, WorkforceEventActivityDisplayDriver<WorkforceShiftAssignedEvent>>();
        services.AddActivity<WorkforceShiftChangedEvent, WorkforceEventActivityDisplayDriver<WorkforceShiftChangedEvent>>();
        services.AddActivity<WorkforceShiftRemovedEvent, WorkforceEventActivityDisplayDriver<WorkforceShiftRemovedEvent>>();
        services.AddActivity<WorkforceTimeOffWaitlistPromotedEvent, WorkforceEventActivityDisplayDriver<WorkforceTimeOffWaitlistPromotedEvent>>();
        services.AddActivity<WorkforceOpenShiftPostedEvent, WorkforceEventActivityDisplayDriver<WorkforceOpenShiftPostedEvent>>();
    }
}

/// <summary>
/// Describes the Workforce workflow events to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsRecipeSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<WorkforceTimeOffRequestedEventSchema>()
            .AddWorkflowActivitySchema<WorkforceTimeOffApprovedEventSchema>()
            .AddWorkflowActivitySchema<WorkforceTimeOffDeclinedEventSchema>()
            .AddWorkflowActivitySchema<WorkforceTimeOffCanceledEventSchema>()
            .AddWorkflowActivitySchema<WorkforceTimeOffWaitlistPromotedEventSchema>()
            .AddWorkflowActivitySchema<WorkforceSchedulePublishedEventSchema>()
            .AddWorkflowActivitySchema<WorkforcePunchedInEventSchema>()
            .AddWorkflowActivitySchema<WorkforcePunchedOutEventSchema>()
            .AddWorkflowActivitySchema<WorkforceAttendanceExceptionRaisedEventSchema>()
            .AddWorkflowActivitySchema<WorkforceAttendancePointsThresholdReachedEventSchema>()
            .AddWorkflowActivitySchema<WorkforceTimesheetSubmittedEventSchema>()
            .AddWorkflowActivitySchema<WorkforceTimesheetApprovedEventSchema>()
            .AddWorkflowActivitySchema<WorkforceShiftAssignedEventSchema>()
            .AddWorkflowActivitySchema<WorkforceShiftChangedEventSchema>()
            .AddWorkflowActivitySchema<WorkforceShiftRemovedEventSchema>()
            .AddWorkflowActivitySchema<WorkforceOpenShiftPostedEventSchema>();
    }
}
