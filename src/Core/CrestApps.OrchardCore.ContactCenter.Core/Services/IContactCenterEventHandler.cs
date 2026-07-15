using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines a handler that reacts to published Contact Center domain events. Handlers allow components
/// to react to the interaction lifecycle without being directly coupled to the component that raised the event.
/// </summary>
public interface IContactCenterEventHandler
{
    /// <summary>
    /// Gets the stable, versioned technical identifier of this handler. The identifier must be unique
    /// across all registered handlers and stable across deployments; it must not depend on the CLR type
    /// name, assembly version, or registration order so that outbox delivery checkpoints remain valid
    /// when handlers are renamed, reordered, or shipped from a new assembly version.
    /// </summary>
    string HandlerId { get; }

    /// <summary>
    /// Handles the specified Contact Center domain event.
    /// </summary>
    /// <param name="interactionEvent">The event to handle.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default);
}
