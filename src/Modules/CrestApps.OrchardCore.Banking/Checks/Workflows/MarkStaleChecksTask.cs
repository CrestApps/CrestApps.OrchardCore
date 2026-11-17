using CrestApps.OrchardCore.Banking.Checks.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Banking.Checks.Workflows;

/// <summary>
/// Marks stale every printed check older than a number of days, such as from a Timer Event once a day. Writes how
/// many checks became stale to the workflow output <c>StaleChecks</c>.
/// </summary>
public sealed class MarkStaleChecksTask : TaskActivity<MarkStaleChecksTask>
{
    /// <summary>
    /// The age a check becomes stale at when the task does not say: six months, as most banks in the United States.
    /// </summary>
    public const int DefaultAgeInDays = 180;

    /// <summary>
    /// The workflow output that receives the number of checks that became stale.
    /// </summary>
    public const string OutputName = "StaleChecks";

    private readonly ICheckService _checkService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkStaleChecksTask"/> class.
    /// </summary>
    /// <param name="checkService">The check rules.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public MarkStaleChecksTask(ICheckService checkService, IStringLocalizer<MarkStaleChecksTask> stringLocalizer)
    {
        _checkService = checkService;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Mark Stale Checks"];

    /// <inheritdoc />
    public override LocalizedString Category => S["Checks"];

    /// <summary>
    /// Gets or sets how many days after its check date a printed check becomes stale.
    /// </summary>
    public int AgeInDays
    {
        get => GetProperty(() => DefaultAgeInDays);
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var count = await _checkService.MarkStaleChecksAsync(Math.Max(1, AgeInDays));

        workflowContext.Output[OutputName] = count;
        workflowContext.LastResult = count;

        return Outcome("Done");
    }
}
