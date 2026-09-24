using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// What the provider's call stream writes to the audit log: every state the call reaches, dated by the provider's
/// own time, with how long each hold lasted and, when the call ends, exactly how and why it ended.
/// </summary>
public sealed partial class ProviderVoiceEventService
{
    private ContactCenterAuditRecorder _auditRecorder;

    // Built over the publisher this service already writes through, so the call stream's records go through the
    // same door as every other writer's without widening a constructor the file-size ratchet has frozen.
    private ContactCenterAuditRecorder AuditRecorder
        => _auditRecorder ??= new ContactCenterAuditRecorder(_publisher, _clock);

    /// <summary>
    /// Puts the call on hold, remembering when the hold began. A hold already running keeps its start, so a
    /// provider repeating the state does not shorten it.
    /// </summary>
    private static void StartHold(CallSession session, DateTime now)
    {
        if (!session.IsOnHold || !session.HoldStartedUtc.HasValue)
        {
            session.HoldStartedUtc = now;
        }

        session.IsOnHold = true;
    }

    /// <summary>
    /// Takes the call off hold, adding the hold that just ended to the call's hold time. The hold's start is kept,
    /// so the resume can say how long the hold lasted.
    /// </summary>
    private static void EndHold(CallSession session, DateTime now)
    {
        if (session.IsOnHold && session.HoldStartedUtc.HasValue && now > session.HoldStartedUtc.Value)
        {
            session.HoldSeconds += (now - session.HoldStartedUtc.Value).TotalSeconds;
        }

        session.IsOnHold = false;
    }

    private async Task PublishStateEventAsync(
        string eventType,
        CallSession session,
        Interaction interaction,
        ProviderVoiceEvent providerEvent,
        VoiceCallState previousState,
        DateTime occurredUtc,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var data = CreateStateEventData(eventType, session, interaction, providerEvent, previousState, occurredUtc);

        // The same event, to the same handlers, as before; it now also says when the provider saw the change and
        // carries the change itself.
        var interactionEvent = new InteractionEvent
        {
            EventType = eventType,
            InteractionId = interaction.ItemId,
            AggregateType = nameof(CallSession),
            AggregateId = interaction.ItemId,
            ActorId = session.AgentId,
            ActorType = ContactCenterActorType.Provider,
            SourceComponent = ContactCenterConstants.Components.CallSessions,
            IdempotencyKey = idempotencyKey,
            OccurredUtc = occurredUtc,
        };

        interactionEvent.SetData(data);

        await _publisher.PublishAsync(interactionEvent, cancellationToken);

        // An outbound call that ended before anybody answered is a dial that did not connect.
        if (eventType == ContactCenterConstants.Events.CallEnded &&
            session.Direction == InteractionDirection.Outbound &&
            !session.AnsweredUtc.HasValue &&
            !interaction.AnsweredUtc.HasValue)
        {
            var failed = CreateStateEventData(eventType, session, interaction, providerEvent, previousState, occurredUtc);
            failed.Reason = failed.HangupCause ?? CallLifecycleReasons.NotAnswered;

            await AuditRecorder.RecordCallAsync(
                ContactCenterConstants.Events.DialFailed,
                failed,
                occurredUtc,
                ContactCenterActor.Provider(session.ProviderName),
                $"dial:{ContactCenterConstants.Events.DialFailed}:{interaction.ItemId}:{session.ProviderCallId}",
                cancellationToken);
        }
    }

    private static CallLifecycleEventData CreateStateEventData(
        string eventType,
        CallSession session,
        Interaction interaction,
        ProviderVoiceEvent providerEvent,
        VoiceCallState previousState,
        DateTime occurredUtc)
    {
        var data = ContactCenterCallAudit.ForSession(session, interaction);
        data.ProviderLegId = providerEvent.ProviderLegId;
        data.PreviousState = previousState.ToString();
        data.ProviderOccurredUtc = providerEvent.OccurredUtc;

        if (eventType == ContactCenterConstants.Events.CallResumed && session.HoldStartedUtc.HasValue)
        {
            data.DurationSeconds = Math.Max(0, (occurredUtc - session.HoldStartedUtc.Value).TotalSeconds);
        }
        else if (eventType == ContactCenterConstants.Events.CallEnded)
        {
            data.HangupCause = session.HangupCause?.ToString();
            data.ProviderHangupCause = ReadMetadata(providerEvent, ContactCenterConstants.TelephonyMetadata.ProviderHangupCause);
            data.SipHangupCause = ReadMetadata(providerEvent, ContactCenterConstants.TelephonyMetadata.SipHangupCause);
            data.HangupSource = ReadMetadata(providerEvent, ContactCenterConstants.TelephonyMetadata.HangupSource);

            var startedUtc = session.StartedUtc ?? session.CreatedUtc;
            var endedUtc = session.EndedUtc ?? occurredUtc;

            data.DurationSeconds = startedUtc == default ? null : Math.Max(0, (endedUtc - startedUtc).TotalSeconds);
            data.Details["talkSeconds"] = session.TalkSeconds.ToString(CultureInfo.InvariantCulture);
            data.Details["holdSeconds"] = session.HoldSeconds.ToString(CultureInfo.InvariantCulture);

            if (session.AnsweredUtc.HasValue)
            {
                data.Details["answeredUtc"] = session.AnsweredUtc.Value.ToString("O", CultureInfo.InvariantCulture);
            }
        }

        return data;
    }

    private static string ReadMetadata(ProviderVoiceEvent providerEvent, string key)
        => providerEvent.Metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
}
