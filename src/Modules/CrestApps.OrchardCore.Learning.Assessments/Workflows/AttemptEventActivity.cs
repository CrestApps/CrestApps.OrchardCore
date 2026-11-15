using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.Workflows;

/// <summary>
/// A workflow event about a quiz attempt. It can be limited to some quizzes, and by default it does not start for a
/// change a workflow task made, so a workflow that voids or regrades an attempt does not start itself again.
/// </summary>
public abstract class AttemptEventActivity : EventActivity
{
    /// <summary>
    /// The actor recorded for changes a workflow task makes.
    /// </summary>
    public const string WorkflowActor = "Workflow";

    /// <summary>
    /// Initializes a new instance of the <see cref="AttemptEventActivity"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected AttemptEventActivity(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the string localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc />
    public override LocalizedString Category => S["Learning"];

    /// <summary>
    /// Gets or sets the quizzes that start the workflow; empty for every quiz.
    /// </summary>
    public IList<string> QuizIds
    {
        get => GetProperty<IList<string>>(() => []);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets whether a change a workflow task made starts the workflow too.
    /// </summary>
    public bool IncludeWorkflowChanges
    {
        get => GetProperty(() => false);
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    /// <inheritdoc />
    public override bool CanExecute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        if (!IncludeWorkflowChanges && workflowContext.Input.TryGetValue("Actor", out var actor) && string.Equals(actor?.ToString(), WorkflowActor, StringComparison.Ordinal))
        {
            return false;
        }

        return QuizIds.Count == 0 || (workflowContext.Input.TryGetValue("QuizId", out var quizId) && QuizIds.Contains(quizId?.ToString() ?? string.Empty));
    }

    /// <inheritdoc />
    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");

    /// <inheritdoc />
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}
