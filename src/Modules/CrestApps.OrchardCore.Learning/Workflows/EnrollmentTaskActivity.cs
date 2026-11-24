using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Learning.Workflows;

/// <summary>
/// The base of the tasks that change one enrollment: reads the enrollment id and the reason, runs the change, and ends
/// in Done, NotFound or Failed.
/// </summary>
/// <typeparam name="TActivity">The task.</typeparam>
public abstract class EnrollmentTaskActivity<TActivity> : LearningTaskActivity<TActivity>
    where TActivity : ITask
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EnrollmentTaskActivity{TActivity}"/> class.
    /// </summary>
    /// <param name="enrollmentStore">The enrollment store.</param>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected EnrollmentTaskActivity(
        IEnrollmentStore enrollmentStore,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IStringLocalizer stringLocalizer)
        : base(expressionEvaluator, stringLocalizer)
    {
        EnrollmentStore = enrollmentStore;
    }

    /// <summary>
    /// Gets the enrollment store.
    /// </summary>
    protected IEnrollmentStore EnrollmentStore { get; }

    /// <summary>
    /// Gets or sets the enrollment id, a Liquid expression.
    /// </summary>
    public string EnrollmentId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets why, a Liquid expression.
    /// </summary>
    public string Reason
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return [new Outcome(S["Done"]), new Outcome(S["NotFound"]), new Outcome(S["Failed"])];
    }

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var id = await EvaluateAsync(EnrollmentId, workflowContext);
        var enrollment = id is null ? null : await EnrollmentStore.FindByIdAsync(id);

        if (enrollment is null)
        {
            return Outcome("NotFound");
        }

        var error = await ChangeAsync(enrollment, await EvaluateAsync(Reason, workflowContext), workflowContext);

        SetOutput(workflowContext, "Enrollment", new Dictionary<string, object>
        {
            ["EnrollmentId"] = enrollment.ItemId,
            ["Error"] = error,
        });

        return Outcome(error is null ? "Done" : "Failed");
    }

    /// <summary>
    /// Makes the change.
    /// </summary>
    /// <param name="enrollment">The enrollment.</param>
    /// <param name="reason">Why.</param>
    /// <param name="workflowContext">The workflow.</param>
    /// <returns>The error, or <see langword="null"/> when the change was made.</returns>
    protected abstract Task<string> ChangeAsync(Models.Enrollment enrollment, string reason, WorkflowExecutionContext workflowContext);
}
