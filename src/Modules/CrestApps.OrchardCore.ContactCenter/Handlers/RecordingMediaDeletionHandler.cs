using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Deletes the underlying recording media from the owning media store when a recording erasure is requested,
/// then records the confirmed-deletion receipt. Delivery is at-least-once, so the media store must treat an
/// already-absent recording as successfully erased and the confirmation uses a deterministic idempotency key.
/// </summary>
public sealed class RecordingMediaDeletionHandler : IContactCenterEventHandler
{
    private readonly IRecordingMediaStore _mediaStore;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingMediaDeletionHandler"/> class.
    /// </summary>
    /// <param name="mediaStore">The media store that owns recording bytes.</param>
    /// <param name="publisher">The Contact Center event publisher used to record the confirmed-deletion receipt.</param>
    /// <param name="logger">The logger instance.</param>
    public RecordingMediaDeletionHandler(
        IRecordingMediaStore mediaStore,
        IContactCenterEventPublisher publisher,
        ILogger<RecordingMediaDeletionHandler> logger)
    {
        _mediaStore = mediaStore;
        _publisher = publisher;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string HandlerId => "ContactCenter/RecordingMediaDeletion/v1";

    /// <inheritdoc/>
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.GuardedByDurableStore;

    /// <inheritdoc/>
    public async Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        if (interactionEvent.EventType != ContactCenterConstants.Events.RecordingErased)
        {
            return;
        }

        var data = interactionEvent.GetData<RecordingErasedEventData>();

        if (data is null || string.IsNullOrWhiteSpace(data.RecordingReference))
        {
            return;
        }

        if (!await _mediaStore.DeleteAsync(data.RecordingReference, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Recording media deletion could not be confirmed for interaction '{interactionEvent.InteractionId}'.");
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Deleted recording media for interaction '{InteractionId}'.",
                interactionEvent.InteractionId.SanitizeLogValue());
        }

        var confirmedEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.RecordingMediaDeleted,
            InteractionId = interactionEvent.InteractionId,
            AggregateType = nameof(Interaction),
            AggregateId = interactionEvent.AggregateId ?? interactionEvent.InteractionId,
            CorrelationId = interactionEvent.CorrelationId ?? interactionEvent.InteractionId,
            CausationId = interactionEvent.ItemId,
            ActorId = data.ActorId,
            SourceComponent = ContactCenterConstants.Components.Interactions,
            IdempotencyKey = $"recording-media-deleted:{interactionEvent.ItemId}",
        };

        confirmedEvent.SetData(new RecordingMediaDeletedEventData
        {
            ActorId = data.ActorId,
            Reason = data.Reason,
            RecordingReference = data.RecordingReference,
        });

        await _publisher.PublishAsync(confirmedEvent, cancellationToken);
    }
}
