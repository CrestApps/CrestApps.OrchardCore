using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
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
    private readonly IOmnichannelActivityStore _activityStore;
    private readonly ILogger _logger;
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
    /// <param name="activityStore">
    /// The activity store, read to tell a system-paced dial (whose call is the dialer's until the agent joins it) from
    /// one the agent placed.
    /// </param>
    /// <param name="logger">The logger.</param>
    public ContactCenterSoftPhoneEventHandler(
        IInteractionManager interactionManager,
        ICallSessionManager callSessionManager,
        IAgentProfileManager agentProfileManager,
        ITelephonyInteractionStore telephonyInteractionStore,
        IHubContext<TelephonyHub, ITelephonyClient> hubContext,
        ShellSettings shellSettings,
        IOmnichannelActivityStore activityStore,
        ILogger<ContactCenterSoftPhoneEventHandler> logger)
    {
        _interactionManager = interactionManager;
        _callSessionManager = callSessionManager;
        _agentProfileManager = agentProfileManager;
        _telephonyInteractionStore = telephonyInteractionStore;
        _hubContext = hubContext;
        _activityStore = activityStore;
        _logger = logger;
        _tenantName = shellSettings.Name;
    }

    /// <inheritdoc/>
    public string HandlerId => "ContactCenter/SoftPhoneProjection/v1";

    /// <inheritdoc/>
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.NaturallyIdempotent;

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

        // The agent's leg answering is projected only for a dialer call, where it is the moment the call becomes the
        // agent's (see IsPacedDialAwaitingAgentAsync). Other calls reach the soft phone through their own events.
        if (interactionEvent.EventType == ContactCenterConstants.Events.AgentLegAnswered &&
            !DialerCallMetadata.IsCampaignDial(interaction))
        {
            return;
        }

        if (await IsPacedDialAwaitingAgentAsync(interaction, cancellationToken))
        {
            // The end is the one worth naming: it is where a dead number used to reach the agent.
            if (interactionEvent.EventType == ContactCenterConstants.Events.CallEnded && _logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Kept the end of paced dialer call '{InteractionId}' (activity '{ActivityId}') off agent '{AgentId}''s soft phone because no agent joined it; the dialer settles the attempt.",
                    interaction.ItemId.SanitizeLogValue(),
                    interaction.ActivityItemId.SanitizeLogValue(),
                    interaction.AgentId.SanitizeLogValue());
            }

            return;
        }

        var isVoicemail = IsVoicemailProjection(interaction);

        // A message in a queue's shared box belongs to the queue's team, not to any one agent. Without this it landed in
        // the personal Voicemail tab of whichever agent the call was last offered to, as well as in the team's box.
        if (isVoicemail && VoicemailDelivery.GetSharedQueueId(interaction) is not null)
        {
            return;
        }

        var session = await _callSessionManager.FindByInteractionIdAsync(interaction.ItemId, cancellationToken);
        var agentId = ResolveAgentId(interaction, session, isVoicemail);

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

        if (isVoicemail)
        {
            // The platform answered the provider leg only to record a voicemail. The target agent never took the
            // call, so surface it as a terminal, missed call rather than a live one -- and mark it terminal so the
            // recording leg's provider events cannot reactivate the soft phone.
            call.State = CallState.Disconnected;
        }

        await UpsertTelephonyInteractionAsync(agent, interaction, session, call, isVoicemail, cancellationToken);
        await _hubContext.Clients
            .Group(TenantSignalRGroupName.ForUser(_tenantName, agent.UserId))
            .CallStateChanged(call);
    }

    // While the dialer is still reaching the customer of a system-paced dial (Power, Progressive, Predictive), the call is
    // the dialer's, not the agent's: the agent is only held for it, as the agent workspace already treats it. Projected
    // onto the soft phone, every attempt that never reached anyone showed there -- dialing, then gone -- and a dead
    // number told the agent it was not in service, although the dialer dispositions those attempts itself and the
    // agent has nothing to do with them. The soft phone hears of the call once the agent joins it. A preview dial is
    // the agent's own from the start, so it still shows.
    private async Task<bool> IsPacedDialAwaitingAgentAsync(Interaction interaction, CancellationToken cancellationToken)
    {
        if (!DialerCallMetadata.IsAwaitingAgent(interaction))
        {
            return false;
        }

        var activity = await _activityStore.FindByIdAsync(interaction.ActivityItemId, cancellationToken);

        return AgentOfferKindHelper.FromActivitySource(activity?.Source) == AgentOfferKind.AutoDial;
    }

    private static bool IsVoicemailProjection(Interaction interaction)
    {
        return interaction.TechnicalMetadata is not null &&
            interaction.TechnicalMetadata.TryGetValue(ContactCenterConstants.Voicemail.ProjectionMetadataKey, out var value) &&
            IsTrue(value);
    }

    private static bool IsTrue(object value)
    {
        return value switch
        {
            bool boolean => boolean,
            _ => bool.TryParse(value?.ToString(), out var parsed) && parsed,
        };
    }

    private static string ResolveAgentId(Interaction interaction, CallSession session, bool isVoicemail)
    {
        if (isVoicemail &&
            interaction.TechnicalMetadata is not null &&
            interaction.TechnicalMetadata.TryGetValue(ContactCenterConstants.Voicemail.RecipientAgentMetadataKey, out var recipient) &&
            recipient is not null &&
            !string.IsNullOrWhiteSpace(recipient.ToString()))
        {
            return recipient.ToString();
        }

        return session?.AgentId ?? interaction.AgentId;
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
            eventType == ContactCenterConstants.Events.CallEnded ||
            eventType == ContactCenterConstants.Events.CallSentToVoicemail ||

            // A paced dial is kept off the soft phone until the agent joins it, and the join is the agent's leg
            // answering: the customer's own answer came before it, so nothing else would put the call on the phone.
            eventType == ContactCenterConstants.Events.AgentLegAnswered ||

            // The call is the supervisor's once they take it over (the session names them as its agent): live, nothing
            // put it on their soft phone, which showed only a banner with nothing to mute, hold or hang up.
            eventType == ContactCenterConstants.Events.SupervisorTookOver;
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
        bool isVoicemail,
        CancellationToken cancellationToken)
    {
        var existing = await _telephonyInteractionStore.FindByCallIdAsync(agent.UserId, call.CallId, cancellationToken);
        var startedUtc = call.StartedUtc?.UtcDateTime ??
            session?.StartedUtc ??
            interaction.StartedUtc ??
            interaction.CreatedUtc;
        var endedUtc = session?.EndedUtc ?? interaction.EndedUtc;
        var outcome = isVoicemail
            ? CallOutcome.Missed
            : ResolveOutcome(session?.State, call.State, interaction.Direction);

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
                IsVoicemail = isVoicemail,
            };

            ApplyAnswerState(existing, call);
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

        if (isVoicemail)
        {
            existing.IsVoicemail = true;
        }

        ApplyAnswerState(existing, call);
        ApplyTerminalState(existing, call.State, endedUtc);
        await _telephonyInteractionStore.UpdateAsync(existing, cancellationToken);
    }

    // The projected state is the agent's own -- ringing while the offer rings, however live the caller's leg is --
    // so it is what says whether the agent has joined the call. An outbound call's ringing is the far end's.
    private static void ApplyAnswerState(TelephonyInteraction interaction, TelephonyCall call)
    {
        if (call.State == CallState.Ringing && call.Direction == CallDirection.Inbound)
        {
            interaction.AwaitingAnswer = true;
        }
        else if (call.State is CallState.Connected or CallState.OnHold or CallState.Disconnected or CallState.Failed)
        {
            interaction.AwaitingAnswer = false;
        }
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

        // The customer is the interaction's customer address. The session's addresses follow whichever provider leg
        // reported last -- for a call the platform bridged to the agent, the leg it dialed from its own number -- so
        // taken as they are, the soft phone showed the tenant's own caller id as the party on the line.
        var (from, to) = ResolveParties(interaction, session);

        return new TelephonyCall
        {
            CallId = session?.ProviderCallId ?? interaction.ProviderInteractionId,
            From = from,
            To = to,
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

    private static (string From, string To) ResolveParties(Interaction interaction, CallSession session)
    {
        var customer = interaction.CustomerAddress;

        if (string.IsNullOrWhiteSpace(customer))
        {
            return (session?.FromAddress, session?.ToAddress ?? ResolveServiceAddress(interaction));
        }

        // The platform's side is whichever known address is not the customer's.
        var platform = new[] { ResolveServiceAddress(interaction), session?.FromAddress, session?.ToAddress }
            .FirstOrDefault(address => !string.IsNullOrWhiteSpace(address) && !IsSameAddress(address, customer));

        return interaction.Direction == InteractionDirection.Inbound
            ? (customer, platform)
            : (platform, customer);
    }

    private static bool IsSameAddress(string left, string right)
    {
        var leftKey = PhoneNumberComparisonKey.For(default, left);

        return leftKey.Length > 0
            ? string.Equals(leftKey, PhoneNumberComparisonKey.For(default, right), StringComparison.Ordinal)
            : string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
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

        // Why the call ended, so the soft phone can tell the agent a number is not in service instead of the call
        // simply disappearing.
        if (session?.HangupCause is { } hangupCause)
        {
            metadata["hangupCause"] = hangupCause.ToString();
        }

        return metadata;
    }

    private static string ResolveServiceAddress(Interaction interaction)
    {
        return interaction.TechnicalMetadata.TryGetValue("serviceAddress", out var value)
            ? value?.ToString()
            : null;
    }

    private static CallState MapCallState(VoiceCallState? sessionState, InteractionStatus interactionStatus)
    {
        if (interactionStatus == InteractionStatus.Ringing)
        {
            return CallState.Ringing;
        }

        if (sessionState.HasValue)
        {
            return VoiceCallStateProjection.ToTelephonyCallState(sessionState.Value);
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

    private static CallOutcome ResolveOutcome(VoiceCallState? sessionState, CallState callState, InteractionDirection direction)
    {
        if (sessionState.HasValue)
        {
            return sessionState.Value switch
            {
                VoiceCallState.Ended or VoiceCallState.Transferred => CallOutcome.Completed,
                VoiceCallState.NoAnswer => direction == InteractionDirection.Inbound ? CallOutcome.Missed : CallOutcome.Failed,
                VoiceCallState.Rejected => CallOutcome.Rejected,
                VoiceCallState.Canceled => CallOutcome.Canceled,
                VoiceCallState.Failed => CallOutcome.Failed,
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
