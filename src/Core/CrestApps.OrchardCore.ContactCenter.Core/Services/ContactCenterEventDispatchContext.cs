using System.Data.Common;
using CrestApps.Core.Support;
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
    public async Task DispatchAsync(string eventId)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventId);

        var interactionEvent = await _eventStore.FindByIdAsync(eventId);

        if (interactionEvent is null)
        {
            _logger.LogWarning(
                "Skipped deferred Contact Center event dispatch because event '{EventId}' no longer exists.",
                eventId.SanitizeLogValue());

            return;
        }

        try
        {
            await _outbox.DispatchAsync(interactionEvent);
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
}
