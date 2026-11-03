using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Workflows.Services;

/// <summary>
/// Runs the <see cref="AIConversationStartingEvent"/> workflows of a conversation that is about to start. The
/// workflows run in this request and write their decisions to the <see cref="AIConversationStartingResult"/> in
/// their input, which the conversation reads once they finish.
/// </summary>
internal sealed class WorkflowAIConversationStartingHandler : IAIConversationStartingHandler
{
    private readonly IWorkflowManager _workflowManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowAIConversationStartingHandler"/> class.
    /// </summary>
    /// <param name="workflowManager">The workflow manager.</param>
    public WorkflowAIConversationStartingHandler(IWorkflowManager workflowManager)
    {
        _workflowManager = workflowManager;
    }

    /// <inheritdoc/>
    public async Task StartingAsync(AIConversationContext conversation, AIConversationStartingResult result, CancellationToken cancellationToken)
    {
        var input = AIConversationWorkflowInput.Build(conversation);
        input[AIConversationWorkflowInput.StartingResult] = result;

        // Current while the workflows run, so a task that reads the conversation sees this one.
        using var scope = AIConversationScope.Begin(conversation);

        await _workflowManager.TriggerEventAsync(
            nameof(AIConversationStartingEvent),
            input,
            correlationId: conversation.ActivityId ?? conversation.Session?.SessionId);
    }
}
