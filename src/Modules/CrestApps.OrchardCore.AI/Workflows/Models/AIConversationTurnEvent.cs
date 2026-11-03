using CrestApps.OrchardCore.AI.Core.Conversations;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.AI.Workflows.Models;

/// <summary>
/// Starts a workflow for each turn of an AI conversation while it is going on: what the customer said, or what the
/// AI answered. Use it to notify someone when a caller says "cancel", or to act on a turn without waiting for the
/// conversation to end. It runs between turns, so keep the workflow short: a slow one delays the next reply.
/// </summary>
public sealed class AIConversationTurnEvent : EventActivity
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIConversationTurnEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIConversationTurnEvent(IStringLocalizer<AIConversationTurnEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override string Name => nameof(AIConversationTurnEvent);

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["AI Conversation Turn"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["AI Conversations"];

    /// <summary>
    /// Gets or sets the channel this workflow is for, or empty for every channel.
    /// </summary>
    public string Channel
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the AI profile this workflow is for, or empty for every profile.
    /// </summary>
    public string ProfileId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets whose turns start the workflow: <see cref="AIConversationTurn.CustomerRole"/>,
    /// <see cref="AIConversationTurn.AssistantRole"/>, or empty for both.
    /// </summary>
    public string Role
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override bool CanExecute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => AIConversationEventFilter.Matches(workflowContext, Channel, ProfileId) &&
            (string.IsNullOrWhiteSpace(Role) ||
                (workflowContext.Input.TryGetValue("Role", out var role) && string.Equals(role?.ToString(), Role, StringComparison.OrdinalIgnoreCase)));

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
