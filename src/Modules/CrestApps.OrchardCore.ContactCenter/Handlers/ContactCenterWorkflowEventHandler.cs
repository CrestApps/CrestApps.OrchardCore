using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Bridges Contact Center domain events to OrchardCore Workflows by triggering the
/// <see cref="ContactCenterEvent"/> workflow event for every published domain event. Because outbox
/// delivery is at-least-once, the bridge dedupes on the durable event id so a replayed event never starts
/// a duplicate workflow.
/// </summary>
/// <remarks>
/// <c>ActorId</c> and <c>ActorType</c> say who made the change: the agent, a supervisor, a workflow, the provider or
/// the platform. <c>AgentId</c> and <c>AgentUserId</c> say which agent the change is about, whoever made it, so a
/// workflow acting on the agent reads them rather than the actor.
/// </remarks>
public sealed class ContactCenterWorkflowEventHandler : IContactCenterEventHandler
{
    private readonly IWorkflowManager _workflowManager;
    private readonly IContactCenterEventDeduplicationService _deduplicationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterWorkflowEventHandler"/> class.
    /// </summary>
    /// <param name="workflowManager">The workflow manager used to trigger the workflow event.</param>
    /// <param name="deduplicationService">The durable per-handler event deduplication service.</param>
    public ContactCenterWorkflowEventHandler(
        IWorkflowManager workflowManager,
        IContactCenterEventDeduplicationService deduplicationService)
    {
        _workflowManager = workflowManager;
        _deduplicationService = deduplicationService;
    }

    /// <inheritdoc/>
    public string HandlerId => "ContactCenter/WorkflowBridge/v1";

    /// <inheritdoc/>
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.DeduplicatedByEventId;

    /// <inheritdoc/>
    public async Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        if (string.IsNullOrEmpty(interactionEvent.ItemId))
        {
            return;
        }

        // Reserve the event before triggering so a replayed delivery cannot start a second workflow; the
        // reservation and the triggered workflow state commit together in the outbox session.
        if (!await _deduplicationService.TryBeginAsync(HandlerId, interactionEvent.ItemId, cancellationToken))
        {
            return;
        }

        var (agentId, agentUserId) = ResolveAgent(interactionEvent);

        var input = new Dictionary<string, object>
        {
            ["EventType"] = interactionEvent.EventType,
            ["InteractionId"] = interactionEvent.InteractionId,
            ["AggregateType"] = interactionEvent.AggregateType,
            ["AggregateId"] = interactionEvent.AggregateId,
            ["ActorId"] = interactionEvent.ActorId,
            ["ActorType"] = interactionEvent.ActorType.ToString(),
            ["AgentId"] = agentId,
            ["AgentUserId"] = agentUserId,
            ["SourceComponent"] = interactionEvent.SourceComponent,
        };

        await _workflowManager.TriggerEventAsync(
            nameof(ContactCenterEvent),
            input,
            correlationId: interactionEvent.InteractionId ?? interactionEvent.AggregateId);
    }

    /// <summary>
    /// Finds the agent an event is about: the agent it is recorded against, or the one its payload names.
    /// </summary>
    /// <remarks>
    /// Older presence events carried the agent's user id as their actor whoever made the change, and some
    /// workflows read it from there. The actor now says who made the change, so the agent is resolved on its own:
    /// from the aggregate when the event is recorded against the agent, and otherwise from the payload's agent and
    /// user ids, which every agent, offer, reservation and call payload carries. An agent acting for themselves is
    /// the last resort for the user id.
    /// </remarks>
    internal static (string AgentId, string AgentUserId) ResolveAgent(InteractionEvent interactionEvent)
    {
        string agentId = null;
        string userId = null;

        if (!string.IsNullOrEmpty(interactionEvent.Data))
        {
            try
            {
                using var document = JsonDocument.Parse(interactionEvent.Data);

                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    agentId = ReadString(document.RootElement, "AgentId") ?? ReadString(document.RootElement, "SelectedAgentId");
                    userId = ReadString(document.RootElement, "UserId");
                }
            }
            catch (JsonException)
            {
                // A payload that is not an object names no agent; the aggregate and the actor still may.
            }
        }

        if (string.Equals(interactionEvent.AggregateType, nameof(AgentProfile), StringComparison.Ordinal))
        {
            agentId = interactionEvent.AggregateId ?? agentId;
        }

        if (userId is null && interactionEvent.ActorType == ContactCenterActorType.Agent)
        {
            userId = interactionEvent.ActorId;
        }

        return (agentId, userId);
    }

    private static string ReadString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(value.GetString())
            ? value.GetString()
            : null;
}
