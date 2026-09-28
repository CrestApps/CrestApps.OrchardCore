using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Decides what the events of a supervisor's own leg mean for their engagement and for the call.
/// </summary>
public sealed class SupervisorLegEventSink : ISupervisorLegEventSink
{
    private readonly IInteractionManager _interactionManager;
    private readonly ICallSessionManager _callSessionManager;
    private readonly IContactCenterAgentLegFailureService _agentLegFailureService;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly ISupervisorEngagementNotifier _notifier;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SupervisorLegEventSink"/> class.
    /// </summary>
    public SupervisorLegEventSink(
        IInteractionManager interactionManager,
        ICallSessionManager callSessionManager,
        IContactCenterAgentLegFailureService agentLegFailureService,
        IContactCenterEventPublisher publisher,
        IEnumerable<ISupervisorEngagementNotifier> notifiers,
        IClock clock)
    {
        _interactionManager = interactionManager;
        _callSessionManager = callSessionManager;
        _agentLegFailureService = agentLegFailureService;
        _publisher = publisher;
        _notifier = notifiers?.FirstOrDefault();
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<bool> OnAnsweredAsync(
        string providerName,
        string providerCallId,
        string supervisorLegId,
        CancellationToken cancellationToken = default)
    {
        var (interaction, session) = await FindAsync(providerName, providerCallId, cancellationToken);
        var engagement = FindEngagement(session, supervisorLegId);

        if (engagement is null)
        {
            return false;
        }

        engagement.ConnectedUtc ??= _clock.UtcNow;

        await _callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);
        await NotifyAsync(SupervisorEngagementNotification.Connected, interaction, session, engagement, reason: null);

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> OnEndedAsync(
        string providerName,
        string providerCallId,
        string supervisorLegId,
        DateTime? endedUtc,
        HangupCause? hangupCause,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(supervisorLegId))
        {
            return false;
        }

        var (interaction, session) = await FindAsync(providerName, providerCallId, cancellationToken);

        if (session is null)
        {
            return false;
        }

        // A supervisor who took the call over is the agent on it now, and their hanging up is the agent hanging up.
        if (session.Legs.Any(leg =>
            leg is not null &&
            leg.Role == CallPartyRole.Agent &&
            string.Equals(leg.ProviderLegId, supervisorLegId, StringComparison.Ordinal)))
        {
            return await _agentLegFailureService.RecordEndedAsync(
                providerName,
                providerCallId,
                supervisorLegId,
                endedUtc,
                hangupCause,
                cancellationToken);
        }

        var engagement = FindEngagement(session, supervisorLegId);

        if (engagement is null)
        {
            return false;
        }

        var now = endedUtc ?? _clock.UtcNow;

        CallTopologyProjector.EndMonitorSession(session, engagement.SupervisorUserId, now);

        await _callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);

        // The supervisor's phone hung up, or never answered: the platform did not stop it, so the reason says so.
        var reason = engagement.ConnectedUtc.HasValue ? "supervisor-left" : "supervisor-unreachable";
        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.SupervisorMonitorStopped,
            InteractionId = interaction.ItemId,
            AggregateType = nameof(Interaction),
            AggregateId = interaction.ItemId,
            ActorId = engagement.SupervisorUserId,
            SourceComponent = ContactCenterConstants.Components.RealTime,
        };

        interactionEvent.SetData(new Dictionary<string, string>
        {
            ["mode"] = engagement.Mode.ToString(),
            ["supervisorId"] = engagement.SupervisorUserId,
            ["reason"] = reason,
        });

        await _publisher.PublishAsync(interactionEvent, CancellationToken.None);
        await NotifyAsync(SupervisorEngagementNotification.Ended, interaction, session, engagement, reason);

        return true;
    }

    private async Task<(Interaction Interaction, CallSession Session)> FindAsync(
        string providerName,
        string providerCallId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providerCallId))
        {
            return (null, null);
        }

        var interaction = string.IsNullOrWhiteSpace(providerName)
            ? await _interactionManager.FindByProviderInteractionIdAsync(providerCallId, cancellationToken)
            : await _interactionManager.FindByProviderInteractionIdAsync(providerName, providerCallId, cancellationToken);

        if (interaction is null)
        {
            return (null, null);
        }

        return (interaction, await _callSessionManager.FindByInteractionIdAsync(interaction.ItemId, cancellationToken));
    }

    private static MonitorSession FindEngagement(CallSession session, string supervisorLegId)
        => session is null || string.IsNullOrWhiteSpace(supervisorLegId)
            ? null
            : session.ActiveMonitorSessions.FirstOrDefault(monitorSession =>
                string.Equals(monitorSession.ProviderLegId, supervisorLegId, StringComparison.Ordinal));

    private Task NotifyAsync(string state, Interaction interaction, CallSession session, MonitorSession engagement, string reason)
        => _notifier is null
            ? Task.CompletedTask
            : _notifier.NotifyEngagementAsync(new SupervisorEngagementNotification
            {
                State = state,
                InteractionId = interaction?.ItemId,
                SupervisorUserId = engagement.SupervisorUserId,
                AgentId = session?.AgentId,
                Mode = engagement.Mode.ToString(),
                Reason = reason,
                ServerTimeUtc = _clock.UtcNow,
            }, CancellationToken.None);
}
