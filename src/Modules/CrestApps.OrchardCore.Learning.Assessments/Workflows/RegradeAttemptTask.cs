using CrestApps.OrchardCore.Learning.Assessments.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Learning.Assessments.Workflows;

/// <summary>
/// Scores a quiz attempt again with the key it kept.
/// </summary>
public sealed class RegradeAttemptTask : AttemptTaskActivity<RegradeAttemptTask>
{
    private readonly IAssessmentAttemptService _attempts;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegradeAttemptTask"/> class.
    /// </summary>
    /// <param name="attempts">The attempt service.</param>
    /// <param name="expressionEvaluator">The expression evaluator.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RegradeAttemptTask(IAssessmentAttemptService attempts, IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer<RegradeAttemptTask> stringLocalizer)
        : base(expressionEvaluator, stringLocalizer)
    {
        _attempts = attempts;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Regrade Quiz Attempt"];

    /// <inheritdoc />
    protected override Task<AttemptResult> ChangeAsync(string attemptId, string reason, WorkflowExecutionContext workflowContext)
        => _attempts.RegradeAsync(attemptId, AttemptEventActivity.WorkflowActor, reason);
}
