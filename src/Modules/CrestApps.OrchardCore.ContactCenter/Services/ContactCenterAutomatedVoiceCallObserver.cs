using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Writes an automated voice agent's call to the Contact Center audit log: answered, who answered, and how the
/// conversation ended.
/// </summary>
/// <remarks>
/// An automated call often has no Contact Center interaction until it is handed to a person, so each record is tied
/// to the interaction when there is one and otherwise to the provider's call, with the CRM activity on every record.
/// </remarks>
public sealed class ContactCenterAutomatedVoiceCallObserver : IAutomatedVoiceCallObserver
{
    private readonly IInteractionManager _interactionManager;
    private readonly IContactCenterAuditRecorder _auditRecorder;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterAutomatedVoiceCallObserver"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager used to tie the call to its interaction.</param>
    /// <param name="auditRecorder">The recorder that writes each moment to the audit log.</param>
    public ContactCenterAutomatedVoiceCallObserver(
        IInteractionManager interactionManager,
        IContactCenterAuditRecorder auditRecorder)
    {
        _interactionManager = interactionManager;
        _auditRecorder = auditRecorder;
    }

    /// <inheritdoc/>
    public async Task ObserveAsync(AutomatedVoiceCallObservation observation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);

        if (string.IsNullOrEmpty(observation.ActivityItemId) && string.IsNullOrEmpty(observation.ProviderCallId))
        {
            return;
        }

        var eventType = observation.Kind switch
        {
            AutomatedVoiceCallObservationKind.Answered => ContactCenterConstants.Events.AiCallAnswered,
            AutomatedVoiceCallObservationKind.AnswererDetected => ContactCenterConstants.Events.AiAnswererDetected,
            AutomatedVoiceCallObservationKind.ConversationEnded => ContactCenterConstants.Events.AiConversationEnded,
            _ => null,
        };

        if (eventType is null)
        {
            return;
        }

        var interaction = string.IsNullOrEmpty(observation.ActivityItemId)
            ? null
            : await _interactionManager.FindByActivityIdAsync(observation.ActivityItemId, cancellationToken);

        var data = interaction is not null
            ? ContactCenterCallAudit.ForInteraction(interaction)
            : new CallLifecycleEventData();

        data.ActivityItemId ??= observation.ActivityItemId;
        data.ProviderName ??= observation.ProviderName;
        data.ProviderCallId ??= observation.ProviderCallId;
        data.Reason = observation.Outcome;

        if (!string.IsNullOrEmpty(observation.Answerer))
        {
            data.Details["answerer"] = observation.Answerer;
        }

        if (!string.IsNullOrEmpty(observation.DispositionId))
        {
            data.Details["dispositionId"] = observation.DispositionId;
        }

        // Each moment happens once per call, and providers redeliver the events that report them.
        await _auditRecorder.RecordCallAsync(
            eventType,
            data,
            observation.OccurredUtc,
            new ContactCenterActor(ContactCenterActorType.AiAgent),
            $"ai:{eventType}:{observation.ActivityItemId}:{observation.ProviderCallId}",
            cancellationToken);
    }
}
