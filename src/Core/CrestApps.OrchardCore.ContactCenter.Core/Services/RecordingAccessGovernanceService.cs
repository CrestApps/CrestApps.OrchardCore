using CrestApps.OrchardCore.ContactCenter.Core.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IRecordingAccessGovernanceService"/>.
/// </summary>
public sealed class RecordingAccessGovernanceService : IRecordingAccessGovernanceService
{
    private static readonly string[] _recordingMetadataKeys =
    [
        ContactCenterConstants.RecordingMetadata.ProviderRecordingId,
        ContactCenterConstants.RecordingMetadata.StorageReference,
        ContactCenterConstants.RecordingMetadata.Format,
        ContactCenterConstants.RecordingMetadata.DurationSeconds,
        ContactCenterConstants.RecordingMetadata.RetrievalPath,
    ];

    private readonly IInteractionManager _interactionManager;
    private readonly ICallSessionManager _callSessionManager;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingAccessGovernanceService"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="callSessionManager">The call session manager used to clear the mirrored recording reference.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="clock">The clock used to stamp erasure instants.</param>
    public RecordingAccessGovernanceService(
        IInteractionManager interactionManager,
        ICallSessionManager callSessionManager,
        IContactCenterEventPublisher publisher,
        IClock clock)
    {
        _interactionManager = interactionManager;
        _callSessionManager = callSessionManager;
        _publisher = publisher;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<bool> RecordAccessAsync(
        string interactionId,
        string actorId,
        string purpose,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(interactionId))
        {
            return false;
        }

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        if (interaction is null || string.IsNullOrEmpty(interaction.RecordingReference))
        {
            return false;
        }

        var accessedEvent = BuildEvent(interaction, ContactCenterConstants.Events.RecordingAccessed, actorId);

        accessedEvent.SetData(new RecordingAccessedEventData
        {
            ActorId = actorId,
            Purpose = purpose,
            RecordingReference = interaction.RecordingReference,
        });

        await _publisher.PublishAsync(accessedEvent, CancellationToken.None);

        return true;
    }

    /// <inheritdoc/>
    public async Task<RecordingErasureDecision> EraseAsync(
        string interactionId,
        string actorId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(interactionId))
        {
            return RecordingErasureDecision.Deny(ContactCenterConstants.RecordingErasureDenyReason.NoRecording);
        }

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        // A missing interaction cannot be audited because there is no aggregate to attach the event to; the request is
        // denied without an event.
        if (interaction is null)
        {
            return RecordingErasureDecision.Deny(ContactCenterConstants.RecordingErasureDenyReason.NoRecording);
        }

        // An existing interaction with no recording reference is a denied erasure request that must still be audited
        // for completeness (including repeat requests after a prior successful erasure).
        if (string.IsNullOrEmpty(interaction.RecordingReference))
        {
            // Defensive hygiene: the reference and its call-session mirror are cleared in the same unit of work
            // during erasure, so an interaction with no reference should never leave a mirrored handle behind. If a
            // prior partial failure did, clear it now so it cannot resurrect access to media that no longer exists.
            await ClearMirroredReferenceAsync(interaction.ItemId, cancellationToken);

            await PublishErasureDeniedAsync(interaction, actorId, ContactCenterConstants.RecordingErasureDenyReason.NoRecording);

            return RecordingErasureDecision.Deny(ContactCenterConstants.RecordingErasureDenyReason.NoRecording);
        }

        // Legal hold overrides a subject erasure request: a recording under hold must be preserved until the hold is
        // released, so the request is denied and audited rather than silently ignored.
        if (interaction.RecordingLegalHold)
        {
            await PublishErasureDeniedAsync(interaction, actorId, ContactCenterConstants.RecordingErasureDenyReason.LegalHold);

            return RecordingErasureDecision.Deny(ContactCenterConstants.RecordingErasureDenyReason.LegalHold);
        }

        var erasedReference = interaction.RecordingReference;

        // The orchestration layer never stores recording media, so erasure clears the opaque retrieval handle and its
        // retrieval metadata and stamps the erasure instant; the published event carries the reference so the owning
        // media store can delete the underlying media. The pointer clears, the erasure tombstone (the stamped
        // RecordingErasedUtc), and the outbox media-deletion enqueue all share the ambient unit of work, so they
        // commit together or not at all.
        interaction.RecordingReference = null;
        interaction.RecordingErasedUtc = _clock.UtcNow;

        if (interaction.TechnicalMetadata is not null)
        {
            foreach (var key in _recordingMetadataKeys)
            {
                interaction.TechnicalMetadata.Remove(key);
            }
        }

        await _interactionManager.UpdateAsync(interaction, cancellationToken: CancellationToken.None);

        // The recording reference is mirrored onto the call session, so it must be cleared in the same unit of work;
        // otherwise the mirrored handle would survive erasure and could resurrect access to the deleted media.
        await ClearMirroredReferenceAsync(interaction.ItemId, cancellationToken);

        var erasedEvent = BuildEvent(interaction, ContactCenterConstants.Events.RecordingErased, actorId);

        erasedEvent.SetData(new RecordingErasedEventData
        {
            ActorId = actorId,
            Reason = reason,
            RecordingReference = erasedReference,
        });

        await _publisher.PublishAsync(erasedEvent, CancellationToken.None);

        return RecordingErasureDecision.Erase();
    }

    private async Task ClearMirroredReferenceAsync(string interactionId, CancellationToken cancellationToken)
    {
        var callSession = await _callSessionManager.FindByInteractionIdAsync(interactionId, cancellationToken);

        if (callSession is not null && !string.IsNullOrEmpty(callSession.RecordingReference))
        {
            callSession.RecordingReference = null;

            await _callSessionManager.UpdateAsync(callSession, cancellationToken: CancellationToken.None);
        }
    }

    private async Task PublishErasureDeniedAsync(Interaction interaction, string actorId, string denyReasonCode)
    {
        var deniedEvent = BuildEvent(interaction, ContactCenterConstants.Events.RecordingErasureDenied, actorId);

        deniedEvent.SetData(new RecordingErasureDeniedEventData
        {
            ActorId = actorId,
            DenyReasonCode = denyReasonCode,
        });

        await _publisher.PublishAsync(deniedEvent, CancellationToken.None);
    }

    private static InteractionEvent BuildEvent(Interaction interaction, string eventType, string actorId)
        => new()
        {
            EventType = eventType,
            InteractionId = interaction.ItemId,
            AggregateType = nameof(Interaction),
            AggregateId = interaction.ItemId,
            ActorId = actorId,
            SourceComponent = ContactCenterConstants.Components.Interactions,
        };
}
