using CrestApps.OrchardCore.Diagnostics;
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
                OperationalLogRedactor.Pseudonymize(eventId, OperationalLogIdentifierCategory.Event));

            return;
        }

        await _outbox.DispatchAsync(interactionEvent);
    }
}
