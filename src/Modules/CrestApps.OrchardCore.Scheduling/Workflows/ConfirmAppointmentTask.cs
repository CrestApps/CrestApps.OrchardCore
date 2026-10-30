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
/// Confirms a held or requested appointment.
/// </summary>
public sealed class ConfirmAppointmentTask : AppointmentTaskActivity<ConfirmAppointmentTask>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfirmAppointmentTask"/> class.
    /// </summary>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="appointmentService">The appointment service.</param>
    /// <param name="availabilityService">The availability service.</param>
    /// <param name="typeStore">The appointment type store.</param>
    /// <param name="waitlistStore">The waitlist store.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ConfirmAppointmentTask(
        IWorkflowExpressionEvaluator expressionEvaluator,
        IAppointmentService appointmentService,
        ISchedulingAvailabilityService availabilityService,
        AppointmentTypeStore typeStore,
        WaitlistStore waitlistStore,
        IClock clock,
        IStringLocalizer<ConfirmAppointmentTask> stringLocalizer)
        : base(expressionEvaluator, appointmentService, availabilityService, typeStore, waitlistStore, clock, stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Confirm Appointment"];

    /// <summary>
    /// Gets or sets the appointment id field, a Liquid expression.
    /// </summary>
    public string AppointmentId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return [new Outcome(S["Done"]), new Outcome(S["Failed"])];
    }

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var appointment = await _appointmentService.FindByIdAsync(await EvaluateAsync(AppointmentId, workflowContext));

        return appointment is null ? Outcome("Failed") : Finish(await _appointmentService.ConfirmAsync(appointment, "workflow"), workflowContext);
    }
}
