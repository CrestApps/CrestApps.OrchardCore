using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines a handler that reacts to published Contact Center domain events. Handlers allow components
/// to react to the interaction lifecycle without being directly coupled to the component that raised the event.
/// </summary>
public interface IContactCenterEventHandler
{
    /// <summary>
    /// Handles the specified Contact Center domain event.
    /// </summary>
    /// <param name="interactionEvent">The event to handle.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default);
}
