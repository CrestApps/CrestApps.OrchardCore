using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Learning.Workflows;

/// <summary>
/// Changes when an enrollment's access ends or when its course is due, with a reason.
/// </summary>
public sealed class ExtendEnrollmentTask : EnrollmentTaskActivity<ExtendEnrollmentTask>
{
    private readonly IEnrollmentService _enrollmentService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtendEnrollmentTask"/> class.
    /// </summary>
    /// <param name="enrollmentService">The enrollment service.</param>
    /// <param name="enrollmentStore">The enrollment store.</param>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ExtendEnrollmentTask(
        IEnrollmentService enrollmentService,
        IEnrollmentStore enrollmentStore,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IStringLocalizer<ExtendEnrollmentTask> stringLocalizer)
        : base(enrollmentStore, expressionEvaluator, stringLocalizer)
    {
        _enrollmentService = enrollmentService;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Extend Access or Due Date"];

    /// <summary>
    /// Gets or sets the new end of access, an ISO 8601 time in a Liquid expression.
    /// </summary>
    public string AccessEnds
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the new due date, an ISO 8601 time in a Liquid expression.
    /// </summary>
    public string Due
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc />
    protected override async Task<string> ChangeAsync(Enrollment enrollment, string reason, WorkflowExecutionContext workflowContext)
    {
        var result = await _enrollmentService.ExtendAsync(
            enrollment.ItemId,
            await EvaluateUtcAsync(AccessEnds, workflowContext),
            await EvaluateUtcAsync(Due, workflowContext),
            LearningEventActivity.WorkflowActor,
            reason ?? S["Changed by a workflow"].Value);

        return result.ErrorCode;
    }
}
