using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterEventPublisher"/>. Events are
/// recorded in the durable interaction event history and then dispatched to every registered handler.
/// Handler failures are logged and do not prevent the event from being recorded or other handlers from running.
/// </summary>
public sealed class DefaultContactCenterEventPublisher : IContactCenterEventPublisher
{
    private readonly IInteractionEventStore _eventStore;
    private readonly IEnumerable<IContactCenterEventHandler> _handlers;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultContactCenterEventPublisher"/> class.
    /// </summary>
    /// <param name="eventStore">The durable interaction event store.</param>
    /// <param name="handlers">The registered event handlers.</param>
    /// <param name="clock">The clock used to stamp events.</param>
    /// <param name="logger">The logger instance.</param>
    public DefaultContactCenterEventPublisher(
        IInteractionEventStore eventStore,
        IEnumerable<IContactCenterEventHandler> handlers,
        IClock clock,
        ILogger<DefaultContactCenterEventPublisher> logger)
    {
        _eventStore = eventStore;
        _handlers = handlers;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task PublishAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        if (interactionEvent.OccurredUtc == default)
        {
            interactionEvent.OccurredUtc = _clock.UtcNow;
        }

        if (string.IsNullOrEmpty(interactionEvent.ItemId))
        {
            interactionEvent.ItemId = IdGenerator.GenerateId();
        }

        if (interactionEvent.SchemaVersion <= 0)
        {
            interactionEvent.SchemaVersion = ContactCenterConstants.CurrentEventSchemaVersion;
        }

        if (string.IsNullOrEmpty(interactionEvent.ActorId))
        {
            interactionEvent.ActorId = ContactCenterConstants.SystemActor;
        }

        if (!string.IsNullOrEmpty(interactionEvent.IdempotencyKey) &&
            await _eventStore.ExistsByIdempotencyKeyAsync(interactionEvent.IdempotencyKey, cancellationToken))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Skipping duplicate Contact Center event '{EventType}' with idempotency key '{IdempotencyKey}'.",
                    interactionEvent.EventType,
                    interactionEvent.IdempotencyKey);
            }

            return;
        }

        await _eventStore.CreateAsync(interactionEvent, cancellationToken);

        foreach (var handler in _handlers)
        {
            try
            {
                await handler.HandleAsync(interactionEvent, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "An error occurred while handling the Contact Center event '{EventType}' for interaction '{InteractionId}' in handler '{Handler}'.",
                    interactionEvent.EventType,
                    interactionEvent.InteractionId,
                    handler.GetType().FullName);
            }
        }
    }
}
