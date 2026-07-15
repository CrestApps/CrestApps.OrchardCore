using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.SignalR;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.SignalR;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Projects Contact Center call-session events back onto the telephony soft phone so the assigned
/// agent's widget reacts immediately when the server advances or ends the call.
/// </summary>
public sealed class ContactCenterSoftPhoneEventHandler : IContactCenterEventHandler
{
    private readonly IInteractionManager _interactionManager;
    private readonly ICallSessionManager _callSessionManager;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly ITelephonyInteractionStore _telephonyInteractionStore;
    private readonly IHubContext<TelephonyHub, ITelephonyClient> _hubContext;
    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterSoftPhoneEventHandler"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager used to resolve the business interaction.</param>
    /// <param name="callSessionManager">The call-session manager used to resolve the normalized voice session.</param>
    /// <param name="agentProfileManager">The agent profile manager used to resolve the assigned Orchard user.</param>
    /// <param name="telephonyInteractionStore">The telephony interaction store used by the soft phone's recent-call history.</param>
    /// <param name="hubContext">The telephony hub context used to push call-state changes to the soft phone.</param>
    /// <param name="shellSettings">The current Orchard shell settings.</param>
    public ContactCenterSoftPhoneEventHandler(
        IInteractionManager interactionManager,
        ICallSessionManager callSessionManager,
        IAgentProfileManager agentProfileManager,
        ITelephonyInteractionStore telephonyInteractionStore,
        IHubContext<TelephonyHub, ITelephonyClient> hubContext,
        ShellSettings shellSettings)
    {
        _interactionManager = interactionManager;
        _callSessionManager = callSessionManager;
        _agentProfileManager = agentProfileManager;
        _telephonyInteractionStore = telephonyInteractionStore;
        _hubContext = hubContext;
        _tenantName = shellSettings.Name;
    }

    /// <inheritdoc/>
    public string HandlerId => "ContactCenter/SoftPhoneProjection/v1";

    /// <inheritdoc/>
    public async Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        if (!ShouldHandle(interactionEvent.EventType))
        {
            return;
        }

        var interactionId = ResolveInteractionId(interactionEvent);

        if (string.IsNullOrEmpty(interactionId))
        {
            return;
        }

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        if (interaction is null || string.IsNullOrEmpty(interaction.ProviderInteractionId))
        {
            return;
        }

        var session = await _callSessionManager.FindByInteractionIdAsync(interaction.ItemId, cancellationToken);
        var agentId = session?.AgentId ?? interaction.AgentId;

        if (string.IsNullOrEmpty(agentId))
        {
            return;
        }

        var agent = await _agentProfileManager.FindByIdAsync(agentId, cancellationToken);

        if (agent is null || string.IsNullOrEmpty(agent.UserId))
        {
            return;
        }

        var call = BuildCall(interaction, session);

        await UpsertTelephonyInteractionAsync(agent, interaction, session, call, cancellationToken);
        await _hubContext.Clients
            .Group(TenantSignalRGroupName.ForUser(_tenantName, agent.UserId))
            .CallStateChanged(call);
    }

    private static bool ShouldHandle(string eventType)
    {
        return eventType == ContactCenterConstants.Events.CallSessionCreated ||
            eventType == ContactCenterConstants.Events.CallSessionUpdated ||
            eventType == ContactCenterConstants.Events.CallConnected ||
            eventType == ContactCenterConstants.Events.CallHeld ||
            eventType == ContactCenterConstants.Events.CallResumed ||
            eventType == ContactCenterConstants.Events.CallMuted ||
            eventType == ContactCenterConstants.Events.CallUnmuted ||
            eventType == ContactCenterConstants.Events.CallConferenceChanged ||
            eventType == ContactCenterConstants.Events.RecordingStarted ||
            eventType == ContactCenterConstants.Events.RecordingPaused ||
            eventType == ContactCenterConstants.Events.RecordingResumed ||
            eventType == ContactCenterConstants.Events.RecordingStopped ||
            eventType == ContactCenterConstants.Events.CallEnded;
    }

    private static string ResolveInteractionId(InteractionEvent interactionEvent)
    {
        return !string.IsNullOrEmpty(interactionEvent.InteractionId)
            ? interactionEvent.InteractionId
            : interactionEvent.AggregateId;
    }

    private async Task UpsertTelephonyInteractionAsync(
        AgentProfile agent,
        Interaction interaction,
        CallSession session,
        TelephonyCall call,
        CancellationToken cancellationToken)
    {
        var existing = await _telephonyInteractionStore.FindByCallIdAsync(agent.UserId, call.CallId, cancellationToken);
        var startedUtc = call.StartedUtc?.UtcDateTime ??
            session?.StartedUtc ??
            interaction.StartedUtc ??
            interaction.CreatedUtc;
        var endedUtc = session?.EndedUtc ?? interaction.EndedUtc;
        var outcome = ResolveOutcome(session?.State, call.State, interaction.Direction);

        if (existing is null)
        {
            existing = new TelephonyInteraction
            {
                InteractionId = interaction.ItemId,
                CallId = call.CallId,
                ProviderName = call.ProviderName,
                UserId = agent.UserId,
                UserName = agent.UserName ?? agent.DisplayName,
                From = call.From,
                To = call.To,
                Direction = call.Direction,
                StartedUtc = startedUtc,
                Outcome = outcome,
            };

            ApplyTerminalState(existing, call.State, endedUtc);
            await _telephonyInteractionStore.CreateAsync(existing, cancellationToken);

            return;
        }

        existing.ProviderName = call.ProviderName;
        existing.UserName = string.IsNullOrEmpty(existing.UserName)
            ? agent.UserName ?? agent.DisplayName
            : existing.UserName;
        existing.From = string.IsNullOrEmpty(call.From) ? existing.From : call.From;
        existing.To = string.IsNullOrEmpty(call.To) ? existing.To : call.To;
        existing.Direction = call.Direction;
        existing.StartedUtc = existing.StartedUtc == default ? startedUtc : existing.StartedUtc;
        existing.Outcome = outcome;

        ApplyTerminalState(existing, call.State, endedUtc);
        await _telephonyInteractionStore.UpdateAsync(existing, cancellationToken);
    }

    private static void ApplyTerminalState(TelephonyInteraction interaction, CallState state, DateTime? endedUtc)
    {
        if (state is not CallState.Disconnected and not CallState.Failed)
        {
            interaction.EndedUtc = null;
            interaction.DurationSeconds = 0;

            return;
        }

        interaction.EndedUtc = endedUtc ?? interaction.StartedUtc;
        interaction.DurationSeconds = Math.Max(0, (interaction.EndedUtc.Value - interaction.StartedUtc).TotalSeconds);
    }

    private static TelephonyCall BuildCall(Interaction interaction, CallSession session)
    {
        var startedUtc = session?.StartedUtc ??
            interaction.StartedUtc ??
            interaction.AnsweredUtc ??
            interaction.CreatedUtc;

        return new TelephonyCall
        {
            CallId = session?.ProviderCallId ?? interaction.ProviderInteractionId,
            From = session?.FromAddress ?? interaction.CustomerAddress,
            To = session?.ToAddress ?? ResolveServiceAddress(interaction),
            State = MapCallState(session?.State, interaction.Status),
            Direction = MapDirection(interaction.Direction),
            IsMuted = session?.IsMuted ?? false,
            IsOnHold = session?.IsOnHold ?? interaction.Status == InteractionStatus.Held,
            ProviderName = session?.ProviderName ?? interaction.ProviderName,
            StartedUtc = startedUtc == default
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(startedUtc, DateTimeKind.Utc)),
            Metadata = BuildMetadata(interaction, session),
        };
    }

    private static Dictionary<string, object> BuildMetadata(Interaction interaction, CallSession session)
    {
        var metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(interaction.ItemId))
        {
            metadata["interactionId"] = interaction.ItemId;
        }

        if (!string.IsNullOrEmpty(interaction.ActivityItemId))
        {
            metadata["activityItemId"] = interaction.ActivityItemId;
        }

        if (!string.IsNullOrEmpty(interaction.QueueId))
        {
            metadata["queueId"] = interaction.QueueId;
        }

        if (!string.IsNullOrEmpty(session?.ItemId))
        {
            metadata["callSessionId"] = session.ItemId;
        }

        metadata["recordingState"] = session?.RecordingState ?? interaction.RecordingState;
        metadata["participantCount"] = session?.ParticipantCount ?? 0;
        metadata["isConference"] = session?.IsConference ?? false;

        if (!string.IsNullOrEmpty(session?.RecordingReference ?? interaction.RecordingReference))
        {
            metadata["recordingReference"] = session?.RecordingReference ?? interaction.RecordingReference;
        }

        return metadata;
    }

    private static string ResolveServiceAddress(Interaction interaction)
    {
        return interaction.TechnicalMetadata.TryGetValue("serviceAddress", out var value)
            ? value?.ToString()
            : null;
    }

    private static CallState MapCallState(ContactCenterCallState? sessionState, InteractionStatus interactionStatus)
    {
        if (interactionStatus == InteractionStatus.Ringing)
        {
            return CallState.Ringing;
        }

        if (sessionState.HasValue)
        {
            return sessionState.Value switch
            {
                ContactCenterCallState.Dialing => CallState.Connecting,
                ContactCenterCallState.Ringing => CallState.Ringing,
                ContactCenterCallState.Connected => CallState.Connected,
                ContactCenterCallState.OnHold => CallState.OnHold,
                ContactCenterCallState.Ending => CallState.Disconnected,
                ContactCenterCallState.Ended => CallState.Disconnected,
                ContactCenterCallState.Transferred => CallState.Disconnected,
                ContactCenterCallState.Canceled => CallState.Disconnected,
                ContactCenterCallState.NoAnswer => CallState.Failed,
                ContactCenterCallState.Rejected => CallState.Failed,
                ContactCenterCallState.Failed => CallState.Failed,
                _ => CallState.Idle,
            };
        }

        return interactionStatus switch
        {
            InteractionStatus.Ringing => CallState.Ringing,
            InteractionStatus.Connected => CallState.Connected,
            InteractionStatus.Held => CallState.OnHold,
            InteractionStatus.Ended => CallState.Disconnected,
            InteractionStatus.Failed => CallState.Failed,
            InteractionStatus.Transferring => CallState.Connected,
            _ => CallState.Idle,
        };
    }

    private static CallDirection MapDirection(InteractionDirection direction)
    {
        return direction switch
        {
            InteractionDirection.Inbound => CallDirection.Inbound,
            _ => CallDirection.Outbound,
        };
    }

    private static CallOutcome ResolveOutcome(ContactCenterCallState? sessionState, CallState callState, InteractionDirection direction)
    {
        if (sessionState.HasValue)
        {
            return sessionState.Value switch
            {
                ContactCenterCallState.Ended or ContactCenterCallState.Transferred => CallOutcome.Completed,
                ContactCenterCallState.NoAnswer => direction == InteractionDirection.Inbound ? CallOutcome.Missed : CallOutcome.Failed,
                ContactCenterCallState.Rejected => CallOutcome.Rejected,
                ContactCenterCallState.Canceled => CallOutcome.Canceled,
                ContactCenterCallState.Failed => CallOutcome.Failed,
                _ => CallOutcome.InProgress,
            };
        }

        return callState == CallState.Failed
            ? CallOutcome.Failed
            : callState == CallState.Disconnected
                ? CallOutcome.Completed
                : CallOutcome.InProgress;
    }
}
