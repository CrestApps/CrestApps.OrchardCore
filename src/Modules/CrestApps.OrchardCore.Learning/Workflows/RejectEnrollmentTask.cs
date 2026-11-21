using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Learning.Workflows;

/// <summary>
/// Rejects a pending enrollment with a reason.
/// </summary>
public sealed class RejectEnrollmentTask : EnrollmentTaskActivity<RejectEnrollmentTask>
{
    private readonly IEnrollmentService _enrollmentService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RejectEnrollmentTask"/> class.
    /// </summary>
    /// <param name="enrollmentService">The enrollment service.</param>
    /// <param name="enrollmentStore">The enrollment store.</param>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RejectEnrollmentTask(
        IEnrollmentService enrollmentService,
        IEnrollmentStore enrollmentStore,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IStringLocalizer<RejectEnrollmentTask> stringLocalizer)
        : base(enrollmentStore, expressionEvaluator, stringLocalizer)
    {
        _enrollmentService = enrollmentService;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Reject Enrollment"];

    /// <inheritdoc />
    protected override async Task<string> ChangeAsync(Enrollment enrollment, string reason, WorkflowExecutionContext workflowContext)
        => (await _enrollmentService.RejectAsync(enrollment.ItemId, LearningEventActivity.WorkflowActor, reason ?? S["Rejected by a workflow"].Value)).ErrorCode;
}
