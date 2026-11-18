using CrestApps.OrchardCore.BusinessHours.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.BusinessHours.Workflows;

/// <summary>
/// Checks whether a business hours schedule is open now, so a workflow can act differently out of hours. A schedule
/// that no source knows, or that is disabled, takes the Unknown outcome.
/// </summary>
public sealed class CheckBusinessHoursTask : TaskActivity<CheckBusinessHoursTask>
{
    /// <summary>
    /// The outcome taken while the schedule is open.
    /// </summary>
    public const string OpenOutcome = "Open";

    /// <summary>
    /// The outcome taken while the schedule is closed.
    /// </summary>
    public const string ClosedOutcome = "Closed";

    /// <summary>
    /// The outcome taken when the schedule is not known.
    /// </summary>
    public const string UnknownOutcome = "Unknown";

    private readonly IOpenHoursService _openHoursService;
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckBusinessHoursTask"/> class.
    /// </summary>
    /// <param name="openHoursService">The open-hours service.</param>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CheckBusinessHoursTask(
        IOpenHoursService openHoursService,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IClock clock,
        IStringLocalizer<CheckBusinessHoursTask> stringLocalizer)
    {
        _openHoursService = openHoursService;
        _expressionEvaluator = expressionEvaluator;
        _clock = clock;
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    /// <inheritdoc />
    public override LocalizedString Category => S["Business Hours"];

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Check Business Hours"];

    /// <summary>
    /// Gets or sets the schedule's id.
    /// </summary>
    public string ScheduleId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets a time zone to read the schedule in instead of its own, such as the contact's, as a Liquid
    /// expression; optional.
    /// </summary>
    public string TimeZoneExpression
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Open"]), new Outcome(S["Closed"]), new Outcome(S["Unknown"])];

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        string timeZoneId = null;

        if (!string.IsNullOrWhiteSpace(TimeZoneExpression))
        {
            timeZoneId = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(TimeZoneExpression), workflowContext, null))?.Trim();
        }

        var open = await _openHoursService.IsOpenAsync(ScheduleId, _clock.UtcNow, string.IsNullOrEmpty(timeZoneId) ? null : timeZoneId);

        return open switch
        {
            true => Outcome(OpenOutcome),
            false => Outcome(ClosedOutcome),
            _ => Outcome(UnknownOutcome),
        };
    }
}
