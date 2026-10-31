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
/// Books an appointment at an open time.
/// </summary>
public sealed class BookAppointmentTask : AppointmentTaskActivity<BookAppointmentTask>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BookAppointmentTask"/> class.
    /// </summary>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="appointmentService">The appointment service.</param>
    /// <param name="availabilityService">The availability service.</param>
    /// <param name="typeStore">The appointment type store.</param>
    /// <param name="waitlistStore">The waitlist store.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BookAppointmentTask(
        IWorkflowExpressionEvaluator expressionEvaluator,
        IAppointmentService appointmentService,
        ISchedulingAvailabilityService availabilityService,
        AppointmentTypeStore typeStore,
        WaitlistStore waitlistStore,
        IClock clock,
        IStringLocalizer<BookAppointmentTask> stringLocalizer)
        : base(expressionEvaluator, appointmentService, availabilityService, typeStore, waitlistStore, clock, stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Book Appointment"];

    /// <summary>
    /// Gets or sets the appointment type id field, a Liquid expression.
    /// </summary>
    public string AppointmentTypeId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the start (iso 8601) field, a Liquid expression.
    /// </summary>
    public string Start
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

    /// <summary>
    /// Gets or sets the notes field, a Liquid expression.
    /// </summary>
    public string Notes
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
        if (!TryParse(await EvaluateAsync(Start, workflowContext), out var start))
        {
            return Outcome("Failed");
        }

        var result = await _appointmentService.BookAsync(new AppointmentRequest
        {
            AppointmentTypeId = await EvaluateAsync(AppointmentTypeId, workflowContext),
            StartUtc = start,
            CustomerName = await EvaluateAsync(CustomerName, workflowContext),
            CustomerEmail = await EvaluateAsync(CustomerEmail, workflowContext),
            CustomerPhone = await EvaluateAsync(CustomerPhone, workflowContext),
            Notes = await EvaluateAsync(Notes, workflowContext),
            Source = "Workflow",
            Actor = "workflow",
        });

        return Finish(result, workflowContext);
    }
}
