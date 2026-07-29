using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Brings a persisted Contact Center domain event up to the schema version the running code understands,
/// before any caller reads its payload.
/// </summary>
public interface IInteractionEventUpcastService
{
    /// <summary>
    /// Converts the event's payload to <see cref="ContactCenterConstants.CurrentEventSchemaVersion"/> in place,
    /// applying one registered <see cref="IInteractionEventUpcaster"/> per version step.
    /// </summary>
    /// <param name="interactionEvent">The event read from storage.</param>
    /// <exception cref="InteractionEventUpcastException">
    /// Thrown when the event was written by a newer release than the one reading it, or when no upcaster is
    /// registered for a version step the event has to cross. Both cases mean the payload cannot be interpreted,
    /// and returning it unconverted would hand a caller a payload it would silently misread.
    /// </exception>
    void Upcast(InteractionEvent interactionEvent);
}
