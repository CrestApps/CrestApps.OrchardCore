using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// What a call that ended before an agent answered it writes to the audit log: the offer that was ringing is
/// withdrawn, the caller leaves the queue, and a caller who hung up while waiting abandoned.
/// </summary>
public sealed partial class ProviderVoiceOfferSynchronizationService
{
    private Task RecordOfferWithdrawnAsync(
        ActivityReservation reservation,
        Interaction interaction,
        CancellationToken cancellationToken)
        => _auditRecorder.Value.RecordOfferAsync(
            ContactCenterConstants.Events.OfferCancelled,
            ContactCenterCallAudit.ForOffer(reservation, interaction, agent: null, ResolveEndedUtc(interaction, session: null), CallLifecycleReasons.CallEnded),
            ContactCenterActor.System,
            cancellationToken);

    private async Task RecordLeftQueueAsync(
        QueueItem queueItem,
        Interaction interaction,
        CallSession session,
        bool wasWaiting,
        CancellationToken cancellationToken)
    {
        // Dated by when the call ended, which is the provider's time, rather than by when this pass noticed.
        var endedUtc = ResolveEndedUtc(interaction, session);

        await _auditRecorder.Value.RecordQueueChangeAsync(
            ContactCenterConstants.Events.CallDequeued,
            queueItem,
            interaction,
            endedUtc,
            CallLifecycleReasons.CallEnded,
            cancellationToken: cancellationToken);

        // A caller who hung up while still waiting -- in the queue or being offered -- is an abandon: one who rang
        // in, or one an outbound dial reached and then left waiting for an agent. An outbound call nobody answered
        // is a dial that did not connect, which the dial records say.
        var customerAnswered = interaction.AnsweredUtc.HasValue || session?.AnsweredUtc.HasValue == true;

        if (!wasWaiting ||
            interaction.Channel != InteractionChannel.Voice ||
            (interaction.Direction != InteractionDirection.Inbound && !customerAnswered))
        {
            return;
        }

        var data = session is not null
            ? ContactCenterCallAudit.ForSession(session, interaction)
            : ContactCenterCallAudit.ForInteraction(interaction);
        data.QueueId = queueItem.QueueId;
        data.Reason = CallLifecycleReasons.CallerHungUp;
        data.HangupCause = session?.HangupCause?.ToString();
        data.DurationSeconds = ContactCenterCallAudit.QueueWaitSeconds(queueItem, endedUtc);

        await _auditRecorder.Value.RecordCallAsync(
            ContactCenterConstants.Events.CallAbandoned,
            data,
            endedUtc,
            new ContactCenterActor(ContactCenterActorType.Customer),
            $"call-abandoned:{interaction.ItemId}",
            cancellationToken);
    }

    private DateTime ResolveEndedUtc(Interaction interaction, CallSession session)
        => interaction?.EndedUtc ?? session?.EndedUtc ?? _clock.UtcNow;
}
