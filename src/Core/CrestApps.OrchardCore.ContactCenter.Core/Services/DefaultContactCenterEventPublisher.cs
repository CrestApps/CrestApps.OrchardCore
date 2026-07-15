using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterEventPublisher"/>. Events are
/// recorded in the durable interaction event history and enqueued through <see cref="IContactCenterOutbox"/>
/// before handler dispatch so application restarts cannot create an event-delivery gap.
/// </summary>
public sealed class DefaultContactCenterEventPublisher : IContactCenterEventPublisher
{
    private readonly IInteractionEventStore _eventStore;
    private readonly IContactCenterOutbox _outbox;
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultContactCenterEventPublisher"/> class.
    /// </summary>
    /// <param name="eventStore">The durable interaction event store.</param>
    /// <param name="outbox">The outbox that dispatches events to handlers with durable retry.</param>
    /// <param name="scopeExecutor">The executor used to schedule post-commit dispatch.</param>
    /// <param name="clock">The clock used to stamp events.</param>
    /// <param name="logger">The logger instance.</param>
    public DefaultContactCenterEventPublisher(
        IInteractionEventStore eventStore,
        IContactCenterOutbox outbox,
        IContactCenterScopeExecutor scopeExecutor,
        IClock clock,
        ILogger<DefaultContactCenterEventPublisher> logger)
    {
        _eventStore = eventStore;
        _outbox = outbox;
        _scopeExecutor = scopeExecutor;
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
                    OperationalLogRedactor.Pseudonymize(interactionEvent.IdempotencyKey, OperationalLogIdentifierCategory.Event));
            }

            return;
        }

        await _eventStore.CreateAsync(interactionEvent, cancellationToken);
        await _outbox.EnqueueAsync(interactionEvent, cancellationToken);

        _scopeExecutor.ScheduleAfterCommit<ContactCenterEventDispatchContext>(
            context => context.DispatchAsync(interactionEvent.ItemId));
    }
}
