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
/// Adds a customer to the waitlist of an appointment type.
/// </summary>
public sealed class AddToWaitlistTask : AppointmentTaskActivity<AddToWaitlistTask>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddToWaitlistTask"/> class.
    /// </summary>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="appointmentService">The appointment service.</param>
    /// <param name="availabilityService">The availability service.</param>
    /// <param name="typeStore">The appointment type store.</param>
    /// <param name="waitlistStore">The waitlist store.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AddToWaitlistTask(
        IWorkflowExpressionEvaluator expressionEvaluator,
        IAppointmentService appointmentService,
        ISchedulingAvailabilityService availabilityService,
        AppointmentTypeStore typeStore,
        WaitlistStore waitlistStore,
        IClock clock,
        IStringLocalizer<AddToWaitlistTask> stringLocalizer)
        : base(expressionEvaluator, appointmentService, availabilityService, typeStore, waitlistStore, clock, stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Add to Waitlist"];

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

    /// <summary>
    /// Gets or sets the customer name field, a Liquid expression.
    /// </summary>
    public string CustomerName
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the customer email field, a Liquid expression.
    /// </summary>
    public string CustomerEmail
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the customer phone field, a Liquid expression.
    /// </summary>
    public string CustomerPhone
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
        var type = await _typeStore.FindByIdAsync(await EvaluateAsync(AppointmentTypeId, workflowContext) ?? string.Empty);
        var name = await EvaluateAsync(CustomerName, workflowContext);

        if (type is null || string.IsNullOrEmpty(name) || !TryParse(await EvaluateAsync(From, workflowContext), out var from) || !TryParse(await EvaluateAsync(To, workflowContext), out var to) || to <= from)
        {
            return Outcome("Failed");
        }

        await _waitlistStore.CreateAsync(new WaitlistEntry
        {
            AppointmentTypeId = type.ItemId,
            FromUtc = from,
            ToUtc = to,
            CustomerName = name,
            CustomerEmail = await EvaluateAsync(CustomerEmail, workflowContext),
            CustomerPhone = await EvaluateAsync(CustomerPhone, workflowContext),
            CreatedUtc = _clock.UtcNow,
        });

        return Outcome("Done");
    }
}
