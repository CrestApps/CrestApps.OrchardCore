using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Projects every published Contact Center domain event into the daily event-count metrics used for
/// operational and historical reporting.
/// </summary>
public sealed class ContactCenterMetricsProjectionHandler : IContactCenterEventHandler
{
    private readonly IContactCenterMetricsService _metricsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterMetricsProjectionHandler"/> class.
    /// </summary>
    /// <param name="metricsService">The metrics service.</param>
    public ContactCenterMetricsProjectionHandler(IContactCenterMetricsService metricsService)
    {
        _metricsService = metricsService;
    }

    /// <inheritdoc/>
    public string HandlerId => "ContactCenter/MetricsProjection/v1";

    /// <inheritdoc/>
    public Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        return _metricsService.RecordAsync(interactionEvent.EventType, interactionEvent.OccurredUtc, cancellationToken);
    }
}
