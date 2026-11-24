using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using CrestApps.OrchardCore.ContactCenter.Workflows.Services;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Users;
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
/// the platform. <c>AgentId</c>, <c>AgentUserId</c> and <c>AgentUserName</c> say which agent the change is about,
/// whoever made it, so a workflow acting on the agent reads them rather than the actor. On a call the dialer placed
/// for an agent -- a Power or Progressive dial, or a Predictive one once an agent is connected -- they name that agent,
/// read from the call when the event's own payload does not.
/// </remarks>
public sealed class ContactCenterWorkflowEventHandler : IContactCenterEventHandler
{
    private readonly IWorkflowManager _workflowManager;
    private readonly IContactCenterEventDeduplicationService _deduplicationService;
    private readonly IContactCenterWorkflowSubscriptions _subscriptions;
    private readonly IInteractionManager _interactionManager;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly UserManager<IUser> _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterWorkflowEventHandler"/> class.
    /// </summary>
    /// <param name="workflowManager">The workflow manager used to trigger the workflow event.</param>
    /// <param name="deduplicationService">The durable per-handler event deduplication service.</param>
    /// <param name="subscriptions">Tells whether any workflow listens for an event type.</param>
    /// <param name="interactionManager">The interactions, read for the agent a call was placed or connected for.</param>
    /// <param name="agentProfileManager">The agent profiles, read for the user of the agent the event is about.</param>
    /// <param name="userManager">The users, read for the user name of the agent the event is about.</param>
    public ContactCenterWorkflowEventHandler(
        IWorkflowManager workflowManager,
        IContactCenterEventDeduplicationService deduplicationService,
        IContactCenterWorkflowSubscriptions subscriptions,
        IInteractionManager interactionManager = null,
        IAgentProfileManager agentProfileManager = null,
        UserManager<IUser> userManager = null)
    {
        _workflowManager = workflowManager;
        _deduplicationService = deduplicationService;
        _subscriptions = subscriptions;
        _interactionManager = interactionManager;
        _agentProfileManager = agentProfileManager;
        _userManager = userManager;
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

        // Most events -- agent heartbeats, presence changes -- have no workflow listening. Each one used to write a
        // deduplication row and query the workflow engine for nothing, several times a second on a busy tenant, so an
        // event no enabled workflow listens for stops here, before anything is read or written.
        if (!await _subscriptions.IsSubscribedAsync(interactionEvent.EventType))
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
        string agentUserName;
        (agentId, agentUserId, agentUserName) = await CompleteAgentAsync(interactionEvent, agentId, agentUserId, cancellationToken);

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
            ["AgentUserName"] = agentUserName,
            ["SourceComponent"] = interactionEvent.SourceComponent,

            // What the event itself says -- a dialer attempt's outcome and the number it called, a disposition and the
            // contact it was for -- so a workflow can act on it, for example by texting the customer, rather than only
            // knowing that something happened.
            ["Data"] = ReadData(interactionEvent.Data),
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
                    userId = ReadString(document.RootElement, "UserId") ?? ReadString(document.RootElement, "AgentUserId");
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

    /// <summary>
    /// Fills in what the event's payload and actor did not say about the agent: the agent the call was placed or
    /// connected for, that agent's user, and the user's name.
    /// </summary>
    /// <remarks>
    /// The dialer reserves an agent before a Power or Progressive call and records them on the call, and a Predictive
    /// call records the agent it connects once a person answers. Most dialer events carry no agent of their own, so a
    /// workflow that wanted to give the follow-up to the agent the dialer called for had nothing to read. A Predictive
    /// call nobody answered never had an agent, so it names none.
    /// </remarks>
    private async Task<(string AgentId, string AgentUserId, string AgentUserName)> CompleteAgentAsync(
        InteractionEvent interactionEvent,
        string agentId,
        string agentUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(agentId) &&
            string.IsNullOrEmpty(agentUserId) &&
            _interactionManager is not null &&
            !string.IsNullOrEmpty(interactionEvent.InteractionId))
        {
            var interaction = await _interactionManager.FindByIdAsync(interactionEvent.InteractionId, cancellationToken);

            agentId = interaction?.AgentId;
        }

        if (string.IsNullOrEmpty(agentUserId) && !string.IsNullOrEmpty(agentId) && _agentProfileManager is not null)
        {
            var agent = await _agentProfileManager.FindByIdAsync(agentId, cancellationToken);

            agentUserId = agent?.UserId;
        }

        string agentUserName = null;

        if (!string.IsNullOrEmpty(agentUserId) && _userManager is not null)
        {
            var user = await _userManager.FindByIdAsync(agentUserId);

            agentUserName = user is null ? null : await _userManager.GetUserNameAsync(user);
        }

        return (agentId, agentUserId, agentUserName);
    }

    /// <summary>
    /// The event's payload as a flat set of values a workflow can read, such as <c>Workflow.Input.Data.PhoneNumber</c>.
    /// </summary>
    /// <remarks>
    /// Only the payload's own text, number and true/false values are carried: nested objects and lists are left out,
    /// and a payload that is not an object gives an empty set.
    /// </remarks>
    internal static Dictionary<string, object> ReadData(string data)
    {
        var values = new Dictionary<string, object>(StringComparer.Ordinal);

        if (string.IsNullOrEmpty(data))
        {
            return values;
        }

        try
        {
            using var document = JsonDocument.Parse(data);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return values;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                switch (property.Value.ValueKind)
                {
                    case JsonValueKind.String:
                        values[property.Name] = property.Value.GetString();
                        break;
                    case JsonValueKind.Number:
                        values[property.Name] = property.Value.TryGetInt64(out var whole) ? whole : property.Value.GetDouble();
                        break;
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        values[property.Name] = property.Value.GetBoolean();
                        break;
                }
            }
        }
        catch (JsonException)
        {
            // A payload that cannot be read carries nothing a workflow could use.
        }

        return values;
    }

    private static string ReadString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(value.GetString())
            ? value.GetString()
            : null;
}
