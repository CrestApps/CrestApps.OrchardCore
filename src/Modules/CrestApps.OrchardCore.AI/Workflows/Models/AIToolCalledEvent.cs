using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.AI.Workflows.Models;

/// <summary>
/// Starts a workflow when the AI calls it as a tool. Create a <c>Workflow</c> tool instance that names the workflow;
/// the AI calls it during a conversation, the workflow runs with the tool's arguments in
/// <c>Workflow.Input.Arguments</c>, and what the workflow puts in its output with <c>Set Output</c> is the tool's
/// answer.
/// </summary>
public sealed class AIToolCalledEvent : EventActivity
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolCalledEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolCalledEvent(IStringLocalizer<AIToolCalledEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override string Name => nameof(AIToolCalledEvent);

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["AI Tool Called"];

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
