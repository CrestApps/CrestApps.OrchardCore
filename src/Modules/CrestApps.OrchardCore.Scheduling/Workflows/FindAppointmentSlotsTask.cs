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
/// Lists the open times of an appointment type between two times.
/// </summary>
public sealed class FindAppointmentSlotsTask : AppointmentTaskActivity<FindAppointmentSlotsTask>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FindAppointmentSlotsTask"/> class.
    /// </summary>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="appointmentService">The appointment service.</param>
    /// <param name="availabilityService">The availability service.</param>
    /// <param name="typeStore">The appointment type store.</param>
    /// <param name="waitlistStore">The waitlist store.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public FindAppointmentSlotsTask(
        IWorkflowExpressionEvaluator expressionEvaluator,
        IAppointmentService appointmentService,
        ISchedulingAvailabilityService availabilityService,
        AppointmentTypeStore typeStore,
        WaitlistStore waitlistStore,
        IClock clock,
        IStringLocalizer<FindAppointmentSlotsTask> stringLocalizer)
        : base(expressionEvaluator, appointmentService, availabilityService, typeStore, waitlistStore, clock, stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Find Appointment Slots"];

    /// <summary>
    /// Gets or sets the appointment type id field, a Liquid expression.
    /// </summary>
    public string AppointmentTypeId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the from (iso 8601) field, a Liquid expression.
    /// </summary>
    public string From
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the to (iso 8601) field, a Liquid expression.
    /// </summary>
    public string To
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return [new Outcome(S["Found"]), new Outcome(S["None"])];
    }

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var type = await _typeStore.FindByIdAsync(await EvaluateAsync(AppointmentTypeId, workflowContext) ?? string.Empty);

        if (type is null || !TryParse(await EvaluateAsync(From, workflowContext), out var from) || !TryParse(await EvaluateAsync(To, workflowContext), out var to))
        {
            return Outcome("None");
        }

        var slots = await _availabilityService.FindSlotsAsync(type, from, to, maxResults: 100);
        var output = slots.Select(slot => new Dictionary<string, object> { ["StartUtc"] = AppointmentWorkflowInput.Format(slot.StartUtc), ["EndUtc"] = AppointmentWorkflowInput.Format(slot.EndUtc) }).ToList();

        workflowContext.Output[GetOutputName("AppointmentSlots")] = output;
        workflowContext.LastResult = output;

        return Outcome(output.Count == 0 ? "None" : "Found");
    }
}
