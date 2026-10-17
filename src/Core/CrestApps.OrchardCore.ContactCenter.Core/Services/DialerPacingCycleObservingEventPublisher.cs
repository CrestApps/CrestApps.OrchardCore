using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Publishes through the registered <see cref="IContactCenterEventPublisher"/> and hands each event to the scope's
/// <see cref="DialerPacingCycleObservation"/>, so a pacing cycle's record says what the cycle decided without the routing
/// and dialing services that decide it having to know they are being watched.
/// </summary>
/// <remarks>
/// The event is handed over only once the publisher has returned, so the observation never counts an event publishing
/// threw on. Outside a pacing cycle the observation ignores it.
/// </remarks>
internal sealed class DialerPacingCycleObservingEventPublisher : IContactCenterEventPublisher
{
    private readonly IContactCenterEventPublisher _inner;
    private readonly DialerPacingCycleObservation _observation;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerPacingCycleObservingEventPublisher"/> class.
    /// </summary>
    /// <param name="inner">The publisher every event is published through.</param>
    /// <param name="observation">The scope's observation of the pacing cycle running on it, if any.</param>
    public DialerPacingCycleObservingEventPublisher(
        IContactCenterEventPublisher inner,
        DialerPacingCycleObservation observation)
    {
        _inner = inner;
        _observation = observation;
    }

    /// <inheritdoc/>
    public async Task PublishAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        await _inner.PublishAsync(interactionEvent, cancellationToken);

        _observation.Observe(interactionEvent);
    }
}
