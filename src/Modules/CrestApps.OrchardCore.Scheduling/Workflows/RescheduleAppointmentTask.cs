using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Scheduling.Workflows;

/// <summary>
/// Moves an appointment to another open time.
/// </summary>
public sealed class RescheduleAppointmentTask : AppointmentTaskActivity<RescheduleAppointmentTask>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RescheduleAppointmentTask"/> class.
    /// </summary>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="appointmentService">The appointment service.</param>
    /// <param name="availabilityService">The availability service.</param>
    /// <param name="typeStore">The appointment type store.</param>
    /// <param name="waitlistStore">The waitlist store.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RescheduleAppointmentTask(
        IWorkflowExpressionEvaluator expressionEvaluator,
        IAppointmentService appointmentService,
        ISchedulingAvailabilityService availabilityService,
        AppointmentTypeStore typeStore,
        WaitlistStore waitlistStore,
        IClock clock,
        IStringLocalizer<RescheduleAppointmentTask> stringLocalizer)
        : base(expressionEvaluator, appointmentService, availabilityService, typeStore, waitlistStore, clock, stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Reschedule Appointment"];

    /// <summary>
    /// Gets or sets the appointment id field, a Liquid expression.
    /// </summary>
    public string AppointmentId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the new start (iso 8601) field, a Liquid expression.
    /// </summary>
    public string Start
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return [new Outcome(S["Done"]), new Outcome(S["Taken"]), new Outcome(S["Failed"])];
    }

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var appointment = await _appointmentService.FindByIdAsync(await EvaluateAsync(AppointmentId, workflowContext));

        if (appointment is null || !TryParse(await EvaluateAsync(Start, workflowContext), out var start))
        {
            return Outcome("Failed");
        }

        return Finish(await _appointmentService.RescheduleAsync(appointment, start, actor: "workflow"), workflowContext);
    }
}
