using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Rings the agent's device when a voice offer is presented to them, and hangs that leg up when the offer ends any
/// way other than being accepted: declined, expired, revoked, re-offered to someone else, the caller hanging up, or
/// the agent signing out.
/// </summary>
/// <remarks>
/// The accept and decline paths release the leg themselves, synchronously; this is what covers every other way an
/// offer ends. Each transition is idempotent in the coordinator, so a redelivered event does nothing new.
/// </remarks>
public sealed class AgentPreDialEventHandler : IContactCenterEventHandler
{
    private readonly IAgentPreDialCoordinator _coordinator;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentPreDialEventHandler"/> class.
    /// </summary>
    /// <param name="coordinator">The pre-dial coordinator.</param>
    public AgentPreDialEventHandler(IAgentPreDialCoordinator coordinator)
    {
        _coordinator = coordinator;
    }

    /// <inheritdoc/>
    public string HandlerId => "ContactCenter/AgentPreDial/v1";

    /// <inheritdoc/>
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.NaturallyIdempotent;

    /// <inheritdoc/>
    public Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        return interactionEvent.EventType switch
        {
            ContactCenterConstants.Events.AgentReserved
                => _coordinator.PreDialAsync(interactionEvent.AggregateId, cancellationToken),

            ContactCenterConstants.Events.AgentReleased or
            ContactCenterConstants.Events.OfferDeclined
                => _coordinator.ReleaseAsync(interactionEvent.AggregateId, cancellationToken),

            // A failed connect re-offers the call; the leg rung for the failed offer is not the new offer's.
            ContactCenterConstants.Events.OfferRequeued
                when string.Equals(interactionEvent.AggregateType, nameof(ActivityReservation), StringComparison.Ordinal)
                => _coordinator.ReleaseAsync(interactionEvent.AggregateId, cancellationToken),

            // The caller hung up while the offer rang.
            ContactCenterConstants.Events.CallEnded
                => _coordinator.ReleaseForInteractionAsync(
                    string.IsNullOrEmpty(interactionEvent.InteractionId) ? interactionEvent.AggregateId : interactionEvent.InteractionId,
                    cancellationToken),

            ContactCenterConstants.Events.AgentSignedOut
                => _coordinator.ReleaseForAgentAsync(interactionEvent.AggregateId, cancellationToken),

            _ => Task.CompletedTask,
        };
    }
}
