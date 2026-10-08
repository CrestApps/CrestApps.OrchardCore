using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;

/// <summary>
/// Purges interactions that have ended. A live interaction is never purged no matter how old it is,
/// because age alone does not make an in-flight conversation safe to delete.
/// </summary>
public sealed class InteractionRetentionPolicy : ContactCenterRetentionPolicyBase<Interaction, InteractionIndex>
{
    private readonly ICallSessionManager _callSessionManager;
    private readonly IContactCenterEventPublisher _publisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionRetentionPolicy"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session used to find expired records.</param>
    /// <param name="interactionStore">The interaction store.</param>
    /// <param name="callSessionManager">The call session manager used to clear mirrored recording references.</param>
    /// <param name="publisher">The Contact Center event publisher used to enqueue media deletion before a row is removed.</param>
    public InteractionRetentionPolicy(
        ISession session,
        IInteractionStore interactionStore,
        ICallSessionManager callSessionManager,
        IContactCenterEventPublisher publisher)
        : base(session, interactionStore)
    {
        _callSessionManager = callSessionManager;
        _publisher = publisher;
    }

    /// <inheritdoc/>
    public override string EntityName => "Interaction";

    /// <inheritdoc/>
    protected override bool IsSubjectToLegalHold => true;

    /// <inheritdoc/>
    protected override double GetRetentionDays(ContactCenterRetentionOptions options) => options.InteractionRetentionDays;

    /// <inheritdoc/>
    protected override Expression<Func<InteractionIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc)
        => index => index.EndedUtc != null && index.EndedUtc < cutoffUtc && !index.RecordingLegalHold;

    /// <inheritdoc/>
    protected override async Task<bool> TryPrepareForDeletionAsync(Interaction record, CancellationToken cancellationToken)
    {
        // A recording under legal hold must survive retention until the hold is released; deleting the interaction
        // row would orphan the held media and destroy the evidence the hold exists to preserve. Held records are
        // already excluded by the expired predicate, so this per-record check is the safety net that spares a record
        // whose hold was set after it was fetched.
        if (record.RecordingLegalHold)
        {
            return false;
        }

        // The interaction row carries the only reference to the recording media, so media deletion must be enqueued
        // before the row is deleted or the media would be orphaned. Publishing the erasure event enqueues durable
        // media deletion on the outbox in this batch's unit of work; the event payload carries the reference, so
        // deletion survives the row removal. A stable idempotency key keeps a retry (for example after a failed
        // batch commit re-fetches the same record) from enqueuing the deletion twice.
        if (!string.IsNullOrEmpty(record.RecordingReference))
        {
            await ClearMirroredReferenceAsync(record.ItemId, cancellationToken);

            var erasedEvent = new InteractionEvent
            {
                EventType = ContactCenterConstants.Events.RecordingErased,
                InteractionId = record.ItemId,
                AggregateType = nameof(Interaction),
                AggregateId = record.ItemId,
                ActorId = ContactCenterConstants.SystemActor,
                SourceComponent = ContactCenterConstants.Components.Interactions,
                IdempotencyKey = $"retention-recording-erased:{record.ItemId}",
            };

            erasedEvent.SetData(new RecordingErasedEventData
            {
                ActorId = ContactCenterConstants.SystemActor,
                Reason = ContactCenterConstants.RecordingErasureReason.Retention,
                RecordingReference = record.RecordingReference,
            });

            await _publisher.PublishAsync(erasedEvent, cancellationToken);
        }
        else
        {
            // Defensive hygiene: an interaction with no reference of its own should never leave a mirrored handle
            // behind, but if a prior partial failure did, clear it so the deleted row cannot leave a dangling
            // pointer to media that no longer exists.
            await ClearMirroredReferenceAsync(record.ItemId, cancellationToken);
        }

        return true;
    }

    private async Task ClearMirroredReferenceAsync(string interactionId, CancellationToken cancellationToken)
    {
        var callSession = await _callSessionManager.FindByInteractionIdAsync(interactionId, cancellationToken);

        if (callSession is not null && !string.IsNullOrEmpty(callSession.RecordingReference))
        {
            callSession.RecordingReference = null;

            await _callSessionManager.UpdateAsync(callSession, cancellationToken: cancellationToken);
        }
    }
}
