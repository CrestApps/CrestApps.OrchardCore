using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
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
    private readonly Lock _batchLock = new();

    private ContactCenterRealTimeDispatchBatch _realTimeBatch;

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

        var recordedUtc = _clock.UtcNow;

        if (interactionEvent.OccurredUtc == default)
        {
            interactionEvent.OccurredUtc = recordedUtc;
        }

        if (interactionEvent.RecordedUtc == default)
        {
            interactionEvent.RecordedUtc = recordedUtc;
        }

        if (string.IsNullOrEmpty(interactionEvent.ItemId))
        {
            interactionEvent.ItemId = IdGenerator.GenerateId();
        }

        if (interactionEvent.SchemaVersion <= 0)
        {
            interactionEvent.SchemaVersion = ContactCenterStorage.CurrentEventSchemaVersion;
        }

        if (string.IsNullOrEmpty(interactionEvent.ActorId))
        {
            interactionEvent.ActorId = ContactCenterConstants.SystemActor;
        }

        if (interactionEvent.ActorType == ContactCenterActorType.Unspecified &&
            string.Equals(interactionEvent.ActorId, ContactCenterConstants.SystemActor, StringComparison.Ordinal))
        {
            interactionEvent.ActorType = ContactCenterActorType.System;
        }

        if (!string.IsNullOrEmpty(interactionEvent.IdempotencyKey) &&
            await _eventStore.ExistsByIdempotencyKeyAsync(interactionEvent.IdempotencyKey, cancellationToken))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                // The key names the event; its type is left out of the log, since some types (a shared voicemail's)
                // describe a caller's message.
                _logger.LogDebug(
                    "Skipping duplicate Contact Center event with idempotency key '{IdempotencyKey}'.",
                    interactionEvent.IdempotencyKey.SanitizeLogValue());
            }

            return;
        }

        StampTraceParent(interactionEvent);

        await _eventStore.CreateAsync(interactionEvent, cancellationToken);
        await _outbox.EnqueueAsync(interactionEvent, cancellationToken);

        var eventId = interactionEvent.ItemId;
        var batch = JoinRealTimeBatch(eventId);

        _scopeExecutor.ScheduleAfterCommit<ContactCenterEventDispatchContext>(
            context => context.DispatchAsync(eventId, batch));
    }

    // The first event a change publishes schedules the change's real-time pass, ahead of every event's own dispatch, so
    // the agent's screens hear of all of them as soon as the change commits rather than one by one after each event's
    // slower handlers. The publisher lives in one scope, as do the post-commit tasks it schedules. Should a batch ever be
    // run later than an event's own dispatch, that dispatch finds nothing delivered and runs every handler itself: the
    // screens may hear of the event twice, never not at all.
    private ContactCenterRealTimeDispatchBatch JoinRealTimeBatch(string eventId)
    {
        lock (_batchLock)
        {
            if (_realTimeBatch is not null &&
                _realTimeBatch.TryAdd(eventId))
            {
                return _realTimeBatch;
            }

            var batch = new ContactCenterRealTimeDispatchBatch();

            batch.TryAdd(eventId);

            if (!_scopeExecutor.ScheduleAfterCommit<ContactCenterEventDispatchContext>(context => context.DispatchRealTimeAsync(batch)))
            {
                return null;
            }

            _realTimeBatch = batch;

            return batch;
        }
    }

    // The outbox runs the handlers after the commit, often on the retry pass long after the request ended, so the event
    // carries the trace that published it and the dispatch links back to it rather than starting an unrelated trace.
    private static void StampTraceParent(InteractionEvent interactionEvent)
    {
        var traceParent = TraceContextPropagation.Capture();

        if (traceParent is null)
        {
            return;
        }

        interactionEvent.EntityProperties ??= [];
        interactionEvent.EntityProperties.TryAdd(TraceContextPropagation.TraceParentKey, traceParent);
    }
}
