using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Learning.Workflows;

/// <summary>
/// The base of the Learning workflow events: an optional filter on courses, and a guard so that a change a workflow
/// task made does not start workflows again unless the event asks for it.
/// </summary>
public abstract class LearningEventActivity : EventActivity
{
    /// <summary>
    /// The actor Learning records for changes made by a workflow task.
    /// </summary>
    public const string WorkflowActor = "Workflow";

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningEventActivity"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected LearningEventActivity(IStringLocalizer stringLocalizer)
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
    /// Gets or sets the courses whose enrollments start the workflow. Empty means every course.
    /// </summary>
    public IList<string> CourseIds
    {
        get => GetProperty<IList<string>>(() => []);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets whether a change made by a workflow task starts the workflow too. Off by default, so a workflow does
    /// not start itself.
    /// </summary>
    public bool IncludeWorkflowChanges
    {
        get => GetProperty(() => false);
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return [new Outcome(S["Done"])];
    }

    /// <inheritdoc />
    public override bool CanExecute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        if (!IncludeWorkflowChanges && workflowContext.Input.TryGetValue("Actor", out var actor) && string.Equals(actor?.ToString(), WorkflowActor, StringComparison.Ordinal))
        {
            return false;
        }

        if (CourseIds.Count == 0)
        {
            return true;
        }

        return workflowContext.Input.TryGetValue("CourseId", out var courseId) && CourseIds.Contains(courseId?.ToString() ?? string.Empty);
    }

    /// <inheritdoc />
    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return Outcome("Done");
    }

    /// <inheritdoc />
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return Outcome("Done");
    }
}
