using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Records confirmed recording-media deletion receipts in Orchard's human-visible audit trail.
/// </summary>
public sealed class RecordingMediaDeletionAuditTrailHandler : IContactCenterEventHandler
{
    private readonly IAuditTrailManager _auditTrailManager;
    private readonly IContactCenterEventDeduplicationService _deduplicationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingMediaDeletionAuditTrailHandler"/> class.
    /// </summary>
    /// <param name="auditTrailManager">The Orchard audit trail manager.</param>
    /// <param name="deduplicationService">The durable per-handler event deduplication service.</param>
    public RecordingMediaDeletionAuditTrailHandler(
        IAuditTrailManager auditTrailManager,
        IContactCenterEventDeduplicationService deduplicationService)
    {
        _auditTrailManager = auditTrailManager;
        _deduplicationService = deduplicationService;
    }

    /// <inheritdoc/>
    public string HandlerId => "ContactCenter/RecordingMediaDeletionAuditTrail/v1";

    /// <inheritdoc/>
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.DeduplicatedByEventId;

    /// <inheritdoc/>
    public async Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        if (interactionEvent.EventType != ContactCenterConstants.Events.RecordingMediaDeleted ||
            string.IsNullOrEmpty(interactionEvent.ItemId))
        {
            return;
        }

        var data = interactionEvent.GetData<RecordingMediaDeletedEventData>();

        if (data is null ||
            !await _deduplicationService.TryBeginAsync(HandlerId, interactionEvent.ItemId, cancellationToken))
        {
            return;
        }

        await _auditTrailManager.RecordEventAsync(new AuditTrailContext<RecordingMediaDeletedEventData>(
            ContactCenterAuditTrailEventConfiguration.RecordingMediaDeleted,
            ContactCenterAuditTrailEventConfiguration.CategoryName,
            interactionEvent.InteractionId,
            data.ActorId,
            userName: null,
            data));
    }
}
