using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Learning.Workflows;

/// <summary>
/// Approves a pending enrollment.
/// </summary>
public sealed class ApproveEnrollmentTask : EnrollmentTaskActivity<ApproveEnrollmentTask>
{
    private readonly IEnrollmentService _enrollmentService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApproveEnrollmentTask"/> class.
    /// </summary>
    /// <param name="enrollmentService">The enrollment service.</param>
    /// <param name="enrollmentStore">The enrollment store.</param>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ApproveEnrollmentTask(
        IEnrollmentService enrollmentService,
        IEnrollmentStore enrollmentStore,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IStringLocalizer<ApproveEnrollmentTask> stringLocalizer)
        : base(enrollmentStore, expressionEvaluator, stringLocalizer)
    {
        _enrollmentService = enrollmentService;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Approve Enrollment"];

    /// <inheritdoc />
    protected override async Task<string> ChangeAsync(Enrollment enrollment, string reason, WorkflowExecutionContext workflowContext)
        => (await _enrollmentService.ApproveAsync(enrollment.ItemId, LearningEventActivity.WorkflowActor)).ErrorCode;
}
