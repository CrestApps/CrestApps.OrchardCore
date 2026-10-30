using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.AI.Workflows.Models;

/// <summary>
/// Starts a conversation flow: runs when a conversation of a profile that uses this workflow as its flow takes its first
/// turn. The input is the conversation (<c>SessionId</c>, <c>ProfileId</c>, <c>Channel</c>, <c>CustomerAddress</c>,
/// <c>Variables</c> and the rest). Connect it to an <c>AI Conversation Stage</c>.
/// </summary>
public sealed class AIConversationFlowStartedEvent : EventActivity
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIConversationFlowStartedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIConversationFlowStartedEvent(IStringLocalizer<AIConversationFlowStartedEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override string Name => nameof(AIConversationFlowStartedEvent);

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["AI Conversation Flow Started"];

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
