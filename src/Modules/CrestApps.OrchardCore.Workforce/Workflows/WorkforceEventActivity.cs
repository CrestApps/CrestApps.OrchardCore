using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Workforce.Workflows;

/// <summary>
/// The base of the Workforce workflow events: optionally limited to some teams.
/// </summary>
public abstract class WorkforceEventActivity : EventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforceEventActivity"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected WorkforceEventActivity(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the string localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc />
    public override LocalizedString Category => S["Workforce"];

    /// <summary>
    /// Gets or sets the teams the event starts the workflow for; empty for every team.
    /// </summary>
    public IList<string> TeamIds
    {
        get => GetProperty<IList<string>>(() => []);
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
        if (TeamIds.Count == 0)
        {
            return true;
        }

        return workflowContext.Input.TryGetValue("TeamId", out var teamId) && teamId is string id && TeamIds.Contains(id, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return Outcome("Done");
    }
}
