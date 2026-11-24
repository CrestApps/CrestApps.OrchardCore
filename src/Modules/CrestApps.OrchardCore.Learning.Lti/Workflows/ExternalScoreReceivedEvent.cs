using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Learning.Lti.Workflows;

/// <summary>
/// Starts a workflow when an external tool posts a score, for every grading progress or the ones listed.
/// </summary>
public sealed class ExternalScoreReceivedEvent : EventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalScoreReceivedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ExternalScoreReceivedEvent(IStringLocalizer<ExternalScoreReceivedEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    /// <inheritdoc />
    public override string Name => nameof(ExternalScoreReceivedEvent);

    /// <inheritdoc />
    public override LocalizedString Category => S["Learning"];

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["External Score Received"];

    /// <summary>
    /// Gets or sets the grading progress values that start the workflow, such as <c>FullyGraded</c>; empty for all.
    /// </summary>
    public IList<string> GradingProgress
    {
        get => GetProperty<IList<string>>(() => []);
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    /// <inheritdoc />
    public override bool CanExecute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => GradingProgress.Count == 0
            || (workflowContext.Input.TryGetValue("GradingProgress", out var value) && GradingProgress.Contains(value?.ToString() ?? string.Empty, StringComparer.Ordinal));

    /// <inheritdoc />
    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");

    /// <inheritdoc />
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}
