using CrestApps.OrchardCore.Learning.Assessments.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Learning.Assessments.Workflows;

/// <summary>
/// Voids a quiz attempt with a reason.
/// </summary>
public sealed class VoidAttemptTask : AttemptTaskActivity<VoidAttemptTask>
{
    private readonly IAssessmentAttemptService _attempts;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoidAttemptTask"/> class.
    /// </summary>
    /// <param name="attempts">The attempt service.</param>
    /// <param name="expressionEvaluator">The expression evaluator.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public VoidAttemptTask(IAssessmentAttemptService attempts, IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer<VoidAttemptTask> stringLocalizer)
        : base(expressionEvaluator, stringLocalizer)
    {
        _attempts = attempts;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Void Quiz Attempt"];

    /// <inheritdoc />
    protected override Task<AttemptResult> ChangeAsync(string attemptId, string reason, WorkflowExecutionContext workflowContext)
        => _attempts.VoidAsync(attemptId, AttemptEventActivity.WorkflowActor, reason);
}
