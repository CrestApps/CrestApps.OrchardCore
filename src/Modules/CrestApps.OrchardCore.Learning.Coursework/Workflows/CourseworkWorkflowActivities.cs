using CrestApps.OrchardCore.Learning.Coursework.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Learning.Coursework.Workflows;

/// <summary>
/// An event about a submission, optionally limited to some coursework items.
/// </summary>
public abstract class CourseworkEventActivity : EventActivity
{
    protected CourseworkEventActivity(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected IStringLocalizer S { get; }

    public override LocalizedString Category => S["Learning"];

    /// <summary>
    /// Gets or sets the content item ids of the coursework that starts the workflow; empty for every coursework item.
    /// </summary>
    public IList<string> CourseworkIds
    {
        get => GetProperty<IList<string>>(() => []);
        set => SetProperty(value);
    }

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    public override bool CanExecute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => CourseworkIds.Count == 0
            || (workflowContext.Input.TryGetValue("CourseworkId", out var courseworkId) && CourseworkIds.Contains(courseworkId?.ToString() ?? string.Empty));

    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");

    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}

/// <summary>
/// Starts when a learner hands in coursework.
/// </summary>
public sealed class CourseworkSubmittedEvent : CourseworkEventActivity
{
    public CourseworkSubmittedEvent(IStringLocalizer<CourseworkSubmittedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Name => nameof(CourseworkSubmittedEvent);

    public override LocalizedString DisplayText => S["Coursework Submitted"];
}

/// <summary>
/// Starts when coursework is graded, or graded again.
/// </summary>
public sealed class CourseworkGradedEvent : CourseworkEventActivity
{
    public CourseworkGradedEvent(IStringLocalizer<CourseworkGradedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Name => nameof(CourseworkGradedEvent);

    public override LocalizedString DisplayText => S["Coursework Graded"];
}

/// <summary>
/// Returns a submission to the learner to hand in again, with feedback.
/// </summary>
public sealed class ReturnCourseworkTask : TaskActivity<ReturnCourseworkTask>
{
    /// <summary>
    /// The actor a workflow returns work as.
    /// </summary>
    public const string Actor = "Workflow";

    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly ISubmissionService _submissions;

    public ReturnCourseworkTask(IWorkflowExpressionEvaluator expressionEvaluator, ISubmissionService submissions, IStringLocalizer<ReturnCourseworkTask> stringLocalizer)
    {
        _expressionEvaluator = expressionEvaluator;
        _submissions = submissions;
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    public override LocalizedString Category => S["Learning"];

    public override LocalizedString DisplayText => S["Return Coursework For Resubmission"];

    /// <summary>
    /// Gets or sets the submission, as a Liquid expression.
    /// </summary>
    public string SubmissionId
    {
        get => GetProperty(() => "{{ Workflow.Input.SubmissionId }}");
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the feedback to the learner, as a Liquid expression.
    /// </summary>
    public string Feedback
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"]), new Outcome(S["Failed"])];

    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var submissionId = string.IsNullOrWhiteSpace(SubmissionId) ? null : (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(SubmissionId), workflowContext, null))?.Trim();
        var feedback = string.IsNullOrWhiteSpace(Feedback) ? null : (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(Feedback), workflowContext, null))?.Trim();

        if (string.IsNullOrEmpty(submissionId))
        {
            workflowContext.LastResult = "The submission is missing.";

            return Outcome("Failed");
        }

        var result = await _submissions.ReturnAsync(submissionId, feedback, Actor);

        workflowContext.LastResult = result.Error;

        return Outcome(result.Succeeded ? "Done" : "Failed");
    }
}
