using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Workflows.Services;

/// <summary>
/// Starts the <see cref="AIGuardrailTriggeredEvent"/> workflows when a guardrail triggers.
/// </summary>
internal sealed class WorkflowAIGuardrailHandler : IAIGuardrailHandler
{
    private readonly IWorkflowManager _workflowManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowAIGuardrailHandler"/> class.
    /// </summary>
    /// <param name="workflowManager">The workflow manager.</param>
    public WorkflowAIGuardrailHandler(IWorkflowManager workflowManager)
    {
        _workflowManager = workflowManager;
    }

    /// <inheritdoc/>
    public async Task TriggeredAsync(AIGuardrailVerdict verdict, CancellationToken cancellationToken)
    {
        var conversation = AIConversationScope.Resolve();
        var input = conversation is null
            ? new Dictionary<string, object>()
            : AIConversationWorkflowInput.Build(conversation);

        input["Guardrail"] = verdict;
        input["ProfileId"] = verdict.ProfileId;
        input["SessionId"] = verdict.SessionId;
        input["Direction"] = verdict.Direction.ToString();
        input["Category"] = verdict.Category;
        input["Topic"] = verdict.Topic;
        input["Reason"] = verdict.Reason;
        input["Text"] = verdict.Text;

        await _workflowManager.TriggerEventAsync(
            nameof(AIGuardrailTriggeredEvent),
            input,
            correlationId: verdict.SessionId);
    }
}
