using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// A supervisor acting on an agent rather than a call: setting their state, signing them out, messaging them.
/// </summary>
/// <remarks>
/// A supervisor may act on an agent signed in to, or allowed, a queue the supervisor supervises -- the same agents the
/// live dashboard shows them.
/// </remarks>
public sealed partial class ContactCenterSupervisorInterventionService
{
    /// <summary>
    /// The longest message a supervisor can send an agent.
    /// </summary>
    public const int MaximumMessageLength = 500;

    // The states a supervisor may put an agent in. Busy, Reserved and WrapUp follow work, never a choice.
    private static readonly HashSet<AgentPresenceStatus> _settableStatuses =
    [
        AgentPresenceStatus.Available,
        AgentPresenceStatus.Away,
        AgentPresenceStatus.Break,
    ];

    /// <inheritdoc/>
    public async Task<SupervisorEngagementResult> SetAgentStateAsync(
        string agentId,
        string supervisorUserId,
        ClaimsPrincipal principal,
        AgentPresenceStatus status,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!_settableStatuses.Contains(status))
        {
            return SupervisorEngagementResult.Failure("An agent can be set Available, Not ready or on a break.");
        }

        var target = await AuthorizeAgentAsync(agentId, supervisorUserId, principal, ContactCenterPermissions.InterveneInCalls, cancellationToken);

        if (target.Failure is not null)
        {
            return target.Failure;
        }

        var now = _clock.UtcNow;
        var profile = await _presenceManager.SetPresenceAsync(
            target.Agent.UserId,
            status,
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            new AgentStateChangeContext
            {
                Actor = ContactCenterActor.Supervisor(supervisorUserId),
                Source = AgentStateChangeSources.SetState,
                ChangedUtc = now,
            },
            CancellationToken.None);

        // An agent on a call gets the state when their work ends, exactly as if they had asked for it themselves.
        var deferred = profile is not null && profile.PresenceStatus != status && profile.RequestedPresenceStatus == status;

        await PublishAgentEventAsync(ContactCenterConstants.Events.SupervisorSetAgentState, target.Agent, supervisorUserId, now, new Dictionary<string, string>
        {
            ["status"] = status.ToString(),
            ["reason"] = reason ?? string.Empty,
            ["deferred"] = deferred ? "true" : "false",
        });

        var result = SupervisorEngagementResult.Success();

        if (deferred)
        {
            result.Reason = "The agent is working; the state applies when their current work ends.";
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<SupervisorEngagementResult> SignOutAgentAsync(
        string agentId,
        string supervisorUserId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var target = await AuthorizeAgentAsync(agentId, supervisorUserId, principal, ContactCenterPermissions.InterveneInCalls, cancellationToken);

        if (target.Failure is not null)
        {
            return target.Failure;
        }

        var now = _clock.UtcNow;

        await _presenceManager.SignOutAsync(target.Agent.UserId, new AgentStateChangeContext
        {
            Actor = ContactCenterActor.Supervisor(supervisorUserId),
            Source = AgentStateChangeSources.SignOut,
            ChangedUtc = now,
        }, CancellationToken.None);

        await PublishAgentEventAsync(ContactCenterConstants.Events.SupervisorSetAgentState, target.Agent, supervisorUserId, now, new Dictionary<string, string>
        {
            ["status"] = AgentStateChangeSources.SignOut,
        });

        return SupervisorEngagementResult.Success();
    }

    /// <inheritdoc/>
    public async Task<SupervisorEngagementResult> SendMessageAsync(
        string agentId,
        string supervisorUserId,
        string supervisorName,
        ClaimsPrincipal principal,
        string text,
        CancellationToken cancellationToken = default)
    {
        var message = text?.Trim();

        if (string.IsNullOrEmpty(message))
        {
            return SupervisorEngagementResult.Failure("Type a message to send.");
        }

        if (message.Length > MaximumMessageLength)
        {
            return SupervisorEngagementResult.Failure($"A message can be at most {MaximumMessageLength} characters.");
        }

        var target = await AuthorizeAgentAsync(agentId, supervisorUserId, principal, ContactCenterPermissions.MonitorContactCenter, cancellationToken);

        if (target.Failure is not null)
        {
            return target.Failure;
        }

        if (_notifier is null)
        {
            return SupervisorEngagementResult.Failure("Messages need the Contact Center real-time feature.");
        }

        var now = _clock.UtcNow;
        var messageId = Guid.NewGuid().ToString("N");

        await _notifier.NotifyAgentMessageAsync(new SupervisorMessageNotification
        {
            MessageId = messageId,
            UserId = target.Agent.UserId,
            AgentId = target.Agent.ItemId,
            FromName = string.IsNullOrWhiteSpace(supervisorName) ? null : supervisorName.Trim(),
            Text = message,
            SentUtc = now,
        }, CancellationToken.None);

        await PublishAgentEventAsync(ContactCenterConstants.Events.SupervisorMessagedAgent, target.Agent, supervisorUserId, now, new Dictionary<string, string>
        {
            ["messageId"] = messageId,
            ["text"] = message,
        });

        return SupervisorEngagementResult.Success();
    }

    private async Task<(SupervisorEngagementResult Failure, AgentProfile Agent)> AuthorizeAgentAsync(
        string agentId,
        string supervisorUserId,
        ClaimsPrincipal principal,
        Permission permission,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(agentId) || string.IsNullOrEmpty(supervisorUserId))
        {
            return (SupervisorEngagementResult.Failure("An agent and a supervisor are required."), null);
        }

        if (principal is null || !await _authorizationService.AuthorizeAsync(principal, permission))
        {
            return (SupervisorEngagementResult.Failure("You are not allowed to do that."), null);
        }

        var agent = await _agentProfileManager.FindByIdAsync(agentId, cancellationToken);

        if (agent is null || string.IsNullOrEmpty(agent.UserId))
        {
            return (SupervisorEngagementResult.Failure("The agent could not be found."), null);
        }

        foreach (var queueId in agent.QueueIds.Concat(agent.AllowedQueueIds).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (await _supervisorQueueAuthorizationService.IsAuthorizedAsync(principal, supervisorUserId, queueId, cancellationToken))
            {
                return (null, agent);
            }
        }

        return (SupervisorEngagementResult.Failure("You do not supervise any of this agent's queues."), null);
    }

    private Task PublishAgentEventAsync(
        string eventType,
        AgentProfile agent,
        string supervisorUserId,
        DateTime now,
        Dictionary<string, string> data)
    {
        data["agentId"] = agent.ItemId;
        data["supervisorId"] = supervisorUserId;

        var interactionEvent = new InteractionEvent
        {
            EventType = eventType,
            AggregateType = nameof(AgentProfile),
            AggregateId = agent.ItemId,
            ActorId = supervisorUserId,
            ActorType = ContactCenterActorType.Supervisor,
            OccurredUtc = now,
            SourceComponent = ContactCenterConstants.Components.RealTime,
        };

        interactionEvent.SetData(data);

        return _publisher.PublishAsync(interactionEvent, CancellationToken.None);
    }
}
