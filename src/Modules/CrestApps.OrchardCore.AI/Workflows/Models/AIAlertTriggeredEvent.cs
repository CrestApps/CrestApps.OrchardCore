using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.AI.Workflows.Models;

/// <summary>
/// Runs when an AI alert rule crosses its threshold. The input holds <c>RuleId</c>, <c>RuleName</c>, <c>Metric</c>, <c>Condition</c>, <c>Threshold</c>,
/// <c>Value</c>, <c>SampleSize</c>, <c>WindowMinutes</c>, <c>ProfileId</c> and <c>Status</c>.
/// </summary>
public sealed class AIAlertTriggeredEvent : EventActivity
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIAlertTriggeredEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIAlertTriggeredEvent(IStringLocalizer<AIAlertTriggeredEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override string Name => nameof(AIAlertTriggeredEvent);

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["AI Alert Triggered"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["AI Conversations"];

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    /// <inheritdoc/>
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");

    /// <inheritdoc/>
    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}
