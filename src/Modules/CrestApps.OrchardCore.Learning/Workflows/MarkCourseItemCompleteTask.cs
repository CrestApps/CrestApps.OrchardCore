using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Learning.Workflows;

/// <summary>
/// Marks a course item complete for an enrollment, with a reason, as a person with the permission can.
/// </summary>
public sealed class MarkCourseItemCompleteTask : EnrollmentTaskActivity<MarkCourseItemCompleteTask>
{
    private readonly ILearnerProgressService _progressService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkCourseItemCompleteTask"/> class.
    /// </summary>
    /// <param name="progressService">The progress service.</param>
    /// <param name="enrollmentStore">The enrollment store.</param>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public MarkCourseItemCompleteTask(
        ILearnerProgressService progressService,
        IEnrollmentStore enrollmentStore,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IStringLocalizer<MarkCourseItemCompleteTask> stringLocalizer)
        : base(enrollmentStore, expressionEvaluator, stringLocalizer)
    {
        _progressService = progressService;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Mark Course Item Complete"];

    /// <summary>
    /// Gets or sets the content item id of the course item, a Liquid expression.
    /// </summary>
    public string ItemId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc />
    protected override async Task<string> ChangeAsync(Enrollment enrollment, string reason, WorkflowExecutionContext workflowContext)
    {
        var update = await _progressService.CompleteItemAsync(
            enrollment,
            await EvaluateAsync(ItemId, workflowContext),
            LearningEventActivity.WorkflowActor,
            reason ?? S["Completed by a workflow"].Value);

        return update.RefusedReason;
    }
}
