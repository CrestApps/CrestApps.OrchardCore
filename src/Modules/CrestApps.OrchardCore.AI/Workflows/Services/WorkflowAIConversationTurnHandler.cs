using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Workflows.Services;

/// <summary>
/// Starts the <see cref="AIConversationTurnEvent"/> workflows for each saved turn.
/// </summary>
internal sealed class WorkflowAIConversationTurnHandler : IAIConversationTurnHandler
{
    private readonly IWorkflowManager _workflowManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowAIConversationTurnHandler"/> class.
    /// </summary>
    /// <param name="workflowManager">The workflow manager.</param>
    public WorkflowAIConversationTurnHandler(IWorkflowManager workflowManager)
    {
        _workflowManager = workflowManager;
    }

    /// <inheritdoc/>
    public async Task TurnAsync(AIConversationContext conversation, AIConversationTurn turn, CancellationToken cancellationToken)
    {
        var input = AIConversationWorkflowInput.Build(conversation);
        input["Role"] = turn.Role;
        input["Text"] = turn.Text;

        await _workflowManager.TriggerEventAsync(
            nameof(AIConversationTurnEvent),
            input,
            correlationId: conversation.Session?.SessionId);
    }
}
