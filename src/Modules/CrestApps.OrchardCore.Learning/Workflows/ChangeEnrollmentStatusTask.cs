using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Learning.Workflows;

/// <summary>
/// Moves an enrollment to another status: Active, Suspended, Completed or Withdrawn, with a reason.
/// </summary>
public sealed class ChangeEnrollmentStatusTask : EnrollmentTaskActivity<ChangeEnrollmentStatusTask>
{
    private readonly IEnrollmentService _enrollmentService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeEnrollmentStatusTask"/> class.
    /// </summary>
    /// <param name="enrollmentService">The enrollment service.</param>
    /// <param name="enrollmentStore">The enrollment store.</param>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ChangeEnrollmentStatusTask(
        IEnrollmentService enrollmentService,
        IEnrollmentStore enrollmentStore,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IStringLocalizer<ChangeEnrollmentStatusTask> stringLocalizer)
        : base(enrollmentStore, expressionEvaluator, stringLocalizer)
    {
        _enrollmentService = enrollmentService;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Change Enrollment Status"];

    /// <summary>
    /// Gets or sets the new status, a Liquid expression: Active, Suspended, Completed or Withdrawn.
    /// </summary>
    public string Status
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc />
    protected override async Task<string> ChangeAsync(Enrollment enrollment, string reason, WorkflowExecutionContext workflowContext)
    {
        if (!Enum.TryParse<EnrollmentStatus>(await EvaluateAsync(Status, workflowContext), ignoreCase: true, out var status))
        {
            return EnrollmentErrors.InvalidTransition;
        }

        return (await _enrollmentService.ChangeStatusAsync(enrollment.ItemId, status, LearningEventActivity.WorkflowActor, reason)).ErrorCode;
    }
}
