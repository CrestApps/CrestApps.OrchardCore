using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The events one change published, in order, as its real-time pass runs them: the pass pushes all of them to the
/// screens first, and each event's full dispatch then skips the handlers that already completed for it.
/// </summary>
internal sealed class ContactCenterRealTimeDispatchBatch
{
    private readonly Lock _lock = new();
    private readonly List<string> _eventIds = [];
    private readonly Dictionary<string, Delivered> _delivered = new(StringComparer.Ordinal);
    private bool _started;

    /// <summary>
    /// Adds an event to the batch.
    /// </summary>
    /// <param name="eventId">The event identifier.</param>
    /// <returns><see langword="false"/> when the real-time pass has already started and the event must not wait for it.</returns>
    public bool TryAdd(string eventId)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventId);

        lock (_lock)
        {
            if (_started)
            {
                return false;
            }

            _eventIds.Add(eventId);

            return true;
        }
    }

    /// <summary>
    /// Starts the real-time pass: no event joins the batch after this.
    /// </summary>
    /// <returns>The events of the batch, in the order they were published.</returns>
    public IReadOnlyList<string> Start()
    {
        lock (_lock)
        {
            _started = true;

            return [.. _eventIds];
        }
    }

    /// <summary>
    /// Records what the real-time pass did for an event.
    /// </summary>
    /// <param name="interactionEvent">The event, as loaded once the change committed.</param>
    /// <param name="completedHandlerIds">The handlers that completed for it.</param>
    public void Complete(InteractionEvent interactionEvent, IReadOnlyCollection<string> completedHandlerIds)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);
        ArgumentNullException.ThrowIfNull(completedHandlerIds);

        lock (_lock)
        {
            _delivered[interactionEvent.ItemId] = new Delivered(interactionEvent, completedHandlerIds);
        }
    }

    /// <summary>
    /// Gets what the real-time pass did for an event.
    /// </summary>
    /// <param name="eventId">The event identifier.</param>
    /// <param name="interactionEvent">The event the pass loaded, or <see langword="null"/> when it did not reach it.</param>
    /// <param name="completedHandlerIds">The handlers that completed for it; empty when the pass did not reach it.</param>
    public void GetDelivered(string eventId, out InteractionEvent interactionEvent, out IReadOnlyCollection<string> completedHandlerIds)
    {
        lock (_lock)
        {
            if (eventId is not null && _delivered.TryGetValue(eventId, out var delivered))
            {
                interactionEvent = delivered.Event;
                completedHandlerIds = delivered.CompletedHandlerIds;

                return;
            }
        }

        interactionEvent = null;
        completedHandlerIds = [];
    }

    private sealed record Delivered(InteractionEvent Event, IReadOnlyCollection<string> CompletedHandlerIds);
}
