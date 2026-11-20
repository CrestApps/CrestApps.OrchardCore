using System.Data.Common;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Dispatches a persisted Contact Center event from an isolated post-commit scope.
/// </summary>
public sealed class ContactCenterEventDispatchContext
{
    private readonly IInteractionEventStore _eventStore;
    private readonly IContactCenterOutbox _outbox;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEventDispatchContext"/> class.
    /// </summary>
    /// <param name="eventStore">The interaction event store.</param>
    /// <param name="outbox">The Contact Center outbox.</param>
    /// <param name="logger">The logger.</param>
    public ContactCenterEventDispatchContext(
        IInteractionEventStore eventStore,
        IContactCenterOutbox outbox,
        ILogger<ContactCenterEventDispatchContext> logger)
    {
        _eventStore = eventStore;
        _outbox = outbox;
        _logger = logger;
    }

    /// <summary>
    /// Dispatches the persisted event with the specified identifier.
    /// </summary>
    /// <param name="eventId">The event identifier.</param>
    public Task DispatchAsync(string eventId)
        => DispatchAsync(eventId, batch: null);

    /// <summary>
    /// Dispatches the persisted event with the specified identifier, skipping the handlers that already completed for it
    /// on its change's real-time pass.
    /// </summary>
    /// <param name="eventId">The event identifier.</param>
    /// <param name="batch">The real-time pass of the change that published the event, if it had one.</param>
    internal async Task DispatchAsync(string eventId, ContactCenterRealTimeDispatchBatch batch)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventId);

        IReadOnlyCollection<string> completedHandlerIds = [];
        InteractionEvent interactionEvent = null;

        batch?.GetDelivered(eventId, out interactionEvent, out completedHandlerIds);

        interactionEvent ??= await _eventStore.FindByIdAsync(eventId);

        if (interactionEvent is null)
        {
            _logger.LogWarning(
                "Skipped deferred Contact Center event dispatch because event '{EventId}' no longer exists.",
                eventId.SanitizeLogValue());

            return;
        }

        try
        {
            if (completedHandlerIds.Count > 0)
            {
                await _outbox.DispatchAsync(interactionEvent, completedHandlerIds);
            }
            else
            {
                await _outbox.DispatchAsync(interactionEvent);
            }
        }
        catch (DbException ex)
        {
            // Dispatching straight after the commit only saves the event waiting for the outbox's background pass;
            // the event is already stored and that pass delivers it either way. A database too busy to take the
            // claim -- SQLite allows one writer, and a call ending writes from several requests at once -- is
            // therefore not an error: logged as one, it reported a failure that the next pass quietly repaired.
            _logger.LogWarning(
                ex,
                "Deferred dispatch of Contact Center event '{EventId}' could not reach the database; the outbox's background pass will deliver it.",
                eventId.SanitizeLogValue());
        }
    }

    /// <summary>
    /// Pushes every event of a change to the screens watching for it, in the order they were published, before any
    /// other handler runs for any of them. Runs as the change's first post-commit task.
    /// </summary>
    /// <param name="batch">The events the change published.</param>
    internal async Task DispatchRealTimeAsync(ContactCenterRealTimeDispatchBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        foreach (var eventId in batch.Start())
        {
            try
            {
                var interactionEvent = await _eventStore.FindByIdAsync(eventId);

                if (interactionEvent is null)
                {
                    continue;
                }

                var completedHandlerIds = await _outbox.DispatchRealTimeAsync(interactionEvent);

                batch.Complete(interactionEvent, completedHandlerIds ?? []);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The event's own dispatch, next, still runs every handler, these included.
                _logger.LogWarning(
                    ex,
                    "Could not push Contact Center event '{EventId}' ahead of its other handlers; its own dispatch delivers it.",
                    eventId.SanitizeLogValue());
            }
        }
    }
}
