using System.Globalization;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Scheduling.Workflows;

/// <summary>
/// A workflow task that works with appointments. Its fields are Liquid expressions.
/// </summary>
/// <typeparam name="TActivity">The task type.</typeparam>
public abstract class AppointmentTaskActivity<TActivity> : TaskActivity<TActivity>
    where TActivity : ITask
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;

    /// <summary>
    /// The appointment service.
    /// </summary>
    protected readonly IAppointmentService _appointmentService;

    /// <summary>
    /// The availability service.
    /// </summary>
    protected readonly ISchedulingAvailabilityService _availabilityService;

    /// <summary>
    /// The appointment type store.
    /// </summary>
    protected readonly AppointmentTypeStore _typeStore;

    /// <summary>
    /// The waitlist store.
    /// </summary>
    protected readonly WaitlistStore _waitlistStore;

    /// <summary>
    /// The clock.
    /// </summary>
    protected readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppointmentTaskActivity{TActivity}"/> class.
    /// </summary>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="appointmentService">The appointment service.</param>
    /// <param name="availabilityService">The availability service.</param>
    /// <param name="typeStore">The appointment type store.</param>
    /// <param name="waitlistStore">The waitlist store.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected AppointmentTaskActivity(
        IWorkflowExpressionEvaluator expressionEvaluator,
        IAppointmentService appointmentService,
        ISchedulingAvailabilityService availabilityService,
        AppointmentTypeStore typeStore,
        WaitlistStore waitlistStore,
        IClock clock,
        IStringLocalizer stringLocalizer)
    {
        _expressionEvaluator = expressionEvaluator;
        _appointmentService = appointmentService;
        _availabilityService = availabilityService;
        _typeStore = typeStore;
        _waitlistStore = waitlistStore;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the string localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc />
    public override LocalizedString Category => S["Scheduling"];

    /// <summary>
    /// Gets or sets the name of the workflow output the task writes.
    /// </summary>
    public string OutputName
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Evaluates a Liquid field.
    /// </summary>
    /// <param name="expression">The field.</param>
    /// <param name="workflowContext">The workflow.</param>
    /// <returns>The value, or <see langword="null"/> when empty.</returns>
    protected async Task<string> EvaluateAsync(string expression, WorkflowExecutionContext workflowContext)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var value = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(expression), workflowContext, null))?.Trim();

        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>
    /// Reads an ISO 8601 time as UTC.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <param name="utc">The time.</param>
    /// <returns><see langword="true"/> when the text is a time.</returns>
    protected static bool TryParse(string value, out DateTime utc)
    {
        var parsed = DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var offset);

        utc = parsed ? offset.UtcDateTime : default;

        return parsed;
    }

    /// <summary>
    /// Gets the output name to write.
    /// </summary>
    /// <param name="defaultName">The default.</param>
    /// <returns>The name.</returns>
    protected string GetOutputName(string defaultName)
    {
        return string.IsNullOrWhiteSpace(OutputName) ? defaultName : OutputName.Trim();
    }

    /// <summary>
    /// Writes a booking outcome to the workflow output and picks the outcome.
    /// </summary>
    /// <param name="result">The booking outcome.</param>
    /// <param name="workflowContext">The workflow.</param>
    /// <returns>The activity outcome.</returns>
    protected ActivityExecutionResult Finish(AppointmentResult result, WorkflowExecutionContext workflowContext)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(workflowContext);

        if (!result.Succeeded)
        {
            workflowContext.LastResult = string.Join(" ", result.Errors);

            return Outcome(result.Conflict && GetPossibleOutcomes(workflowContext, null).Any(outcome => outcome.Name == "Taken") ? "Taken" : "Failed");
        }

        var output = AppointmentWorkflowInput.ToDictionary(result.Appointment, null);

        workflowContext.Output[GetOutputName("Appointment")] = output;
        workflowContext.LastResult = output;

        return Outcome("Done");
    }
}
