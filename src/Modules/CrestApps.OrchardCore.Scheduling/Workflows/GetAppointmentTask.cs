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
/// Reads an appointment into the workflow output.
/// </summary>
public sealed class GetAppointmentTask : AppointmentTaskActivity<GetAppointmentTask>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetAppointmentTask"/> class.
    /// </summary>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="appointmentService">The appointment service.</param>
    /// <param name="availabilityService">The availability service.</param>
    /// <param name="typeStore">The appointment type store.</param>
    /// <param name="waitlistStore">The waitlist store.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public GetAppointmentTask(
        IWorkflowExpressionEvaluator expressionEvaluator,
        IAppointmentService appointmentService,
        ISchedulingAvailabilityService availabilityService,
        AppointmentTypeStore typeStore,
        WaitlistStore waitlistStore,
        IClock clock,
        IStringLocalizer<GetAppointmentTask> stringLocalizer)
        : base(expressionEvaluator, appointmentService, availabilityService, typeStore, waitlistStore, clock, stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Get Appointment"];

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
        return [new Outcome(S["Found"]), new Outcome(S["NotFound"])];
    }

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var appointment = await _appointmentService.FindByIdAsync(await EvaluateAsync(AppointmentId, workflowContext));

        if (appointment is null)
        {
            return Outcome("NotFound");
        }

        var output = AppointmentWorkflowInput.ToDictionary(appointment, await _typeStore.FindByIdAsync(appointment.AppointmentTypeId));

        workflowContext.Output[GetOutputName("Appointment")] = output;
        workflowContext.LastResult = output;

        return Outcome("Found");
    }
}
