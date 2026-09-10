using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Publishes Contact Center domain events. Publishing records the event in the durable interaction
/// event history and dispatches it to the registered <see cref="IContactCenterEventHandler"/> instances.
/// </summary>
public interface IContactCenterEventPublisher
{
    /// <summary>
    /// Publishes the specified Contact Center domain event.
    /// </summary>
    /// <param name="interactionEvent">The event to publish.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task PublishAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default);
}
