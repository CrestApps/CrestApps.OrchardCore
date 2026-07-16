using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Projects every published Contact Center domain event into the daily event-count metrics used for
/// operational and historical reporting. Because outbox delivery is at-least-once, the projection dedupes
/// on the durable event id so a replayed event never double-counts.
/// </summary>
public sealed class ContactCenterMetricsProjectionHandler : IContactCenterEventHandler
{
    private readonly IContactCenterMetricsService _metricsService;
    private readonly IContactCenterEventDeduplicationService _deduplicationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterMetricsProjectionHandler"/> class.
    /// </summary>
    /// <param name="metricsService">The metrics service.</param>
    /// <param name="deduplicationService">The durable per-handler event deduplication service.</param>
    public ContactCenterMetricsProjectionHandler(
        IContactCenterMetricsService metricsService,
        IContactCenterEventDeduplicationService deduplicationService)
    {
        _metricsService = metricsService;
        _deduplicationService = deduplicationService;
    }

    /// <inheritdoc/>
    public string HandlerId => ContactCenterConstants.MetricsProjectionHandlerId;

    /// <inheritdoc/>
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.DeduplicatedByEventId;

    /// <inheritdoc/>
    public async Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        if (string.IsNullOrEmpty(interactionEvent.ItemId))
        {
            return;
        }

        // The reservation marker and the counter increment stage in the same session so they commit
        // atomically; a replayed event finds the marker and is skipped without double-counting.
        if (!await _deduplicationService.TryBeginAsync(HandlerId, interactionEvent.ItemId, cancellationToken))
        {
            return;
        }

        await _metricsService.RecordAsync(interactionEvent.EventType, interactionEvent.OccurredUtc, cancellationToken);
    }
}
