using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Learning.Assessments.Workflows;

/// <summary>
/// A workflow task that changes a quiz attempt, with a reason, as the actor <c>Workflow</c>.
/// </summary>
/// <typeparam name="TActivity">The task.</typeparam>
public abstract class AttemptTaskActivity<TActivity> : TaskActivity<TActivity>
    where TActivity : ITask
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;

    /// <summary>
    /// Initializes a new instance of the <see cref="AttemptTaskActivity{TActivity}"/> class.
    /// </summary>
    /// <param name="expressionEvaluator">The expression evaluator.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected AttemptTaskActivity(IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer stringLocalizer)
    {
        _expressionEvaluator = expressionEvaluator;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the string localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc />
    public override LocalizedString Category => S["Learning"];

    /// <summary>
    /// Gets or sets the attempt id, a Liquid expression.
    /// </summary>
    public string AttemptId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets why, a Liquid expression; required by the change.
    /// </summary>
    public string Reason
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the workflow output name of the result.
    /// </summary>
    public string OutputName
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcomes(S["Done"], S["NotFound"], S["Failed"]);

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var attemptId = await EvaluateAsync(AttemptId, workflowContext);

        if (attemptId is null)
        {
            return Outcome("NotFound");
        }

        var result = await ChangeAsync(attemptId, await EvaluateAsync(Reason, workflowContext), workflowContext);

        workflowContext.Output[string.IsNullOrWhiteSpace(OutputName) ? "Attempt" : OutputName.Trim()] = new Dictionary<string, object>
        {
            ["AttemptId"] = attemptId,
            ["Status"] = result.Attempt?.Status.ToString(),
            ["Score"] = result.Attempt?.Score,
            ["Error"] = result.ErrorCode,
        };

        return Outcome(result.ErrorCode == Services.AttemptErrors.NotFound ? "NotFound" : result.Succeeded ? "Done" : "Failed");
    }

    /// <summary>
    /// Makes the change.
    /// </summary>
    /// <param name="attemptId">The attempt.</param>
    /// <param name="reason">Why.</param>
    /// <param name="workflowContext">The workflow context.</param>
    /// <returns>The result.</returns>
    protected abstract Task<Services.AttemptResult> ChangeAsync(string attemptId, string reason, WorkflowExecutionContext workflowContext);

    private async Task<string> EvaluateAsync(string expression, WorkflowExecutionContext workflowContext)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var value = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(expression), workflowContext, null))?.Trim();

        return string.IsNullOrEmpty(value) ? null : value;
    }
}
