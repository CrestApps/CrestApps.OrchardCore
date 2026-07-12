using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default <see cref="IContactCenterOutbox"/> implementation. Every event is persisted as a
/// pending <see cref="ContactCenterOutboxMessage"/> before dispatch. Registered
/// <see cref="IContactCenterEventHandler"/> instances are tracked individually and incomplete handlers are
/// redelivered with exponential back-off.
/// </summary>
public sealed class ContactCenterOutbox : IContactCenterOutbox
{
    /// <summary>
    /// The maximum number of dispatch attempts before a message is dead-lettered.
    /// </summary>
    public const int MaxAttempts = 10;

    /// <summary>
    /// The maximum number of due messages processed in a single retry pass.
    /// </summary>
    public const int MaxBatchSize = 100;

    private const int BaseBackoffSeconds = 30;
    private const int MaxBackoffSeconds = 1800;

    private readonly IEnumerable<IContactCenterEventHandler> _handlers;
    private readonly IContactCenterOutboxStore _outboxStore;
    private readonly IInteractionEventStore _eventStore;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterOutbox"/> class.
    /// </summary>
    /// <param name="handlers">The registered Contact Center event handlers.</param>
    /// <param name="outboxStore">The durable outbox message store.</param>
    /// <param name="eventStore">The durable interaction event store used to reload events for retry.</param>
    /// <param name="clock">The clock used to schedule retries.</param>
    /// <param name="logger">The logger instance.</param>
    public ContactCenterOutbox(
        IEnumerable<IContactCenterEventHandler> handlers,
        IContactCenterOutboxStore outboxStore,
        IInteractionEventStore eventStore,
        IClock clock,
        ILogger<ContactCenterOutbox> logger)
    {
        _handlers = handlers;
        _outboxStore = outboxStore;
        _eventStore = eventStore;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task EnqueueAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        await GetOrCreateMessageAsync(interactionEvent, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        var message = await GetOrCreateMessageAsync(interactionEvent, cancellationToken);
        var firstError = await RunHandlersAsync(interactionEvent, message, cancellationToken);

        if (firstError is null)
        {
            await _outboxStore.DeleteAsync(message, cancellationToken);

            return;
        }

        await ScheduleRetryAsync(message, firstError, cancellationToken);

        _logger.LogWarning(
            "Scheduled Contact Center event '{EventType}' ({EventId}) for retry after a handler failure: {Error}",
            interactionEvent.EventType,
            interactionEvent.ItemId,
            firstError);
    }

    /// <inheritdoc/>
    public async Task<int> DispatchDueAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var due = await _outboxStore.ListDueAsync(now, MaxBatchSize, cancellationToken);
        var redelivered = 0;

        foreach (var message in due)
        {
            if (string.IsNullOrEmpty(message.EventId))
            {
                await DeadLetterAsync(message, "The outbox message has no event reference.", cancellationToken);

                continue;
            }

            var interactionEvent = await _eventStore.FindByIdAsync(message.EventId, cancellationToken);

            if (interactionEvent is null)
            {
                await DeadLetterAsync(message, "The referenced event no longer exists.", cancellationToken);

                continue;
            }

            var firstError = await RunHandlersAsync(interactionEvent, message, cancellationToken);

            if (firstError is null)
            {
                await _outboxStore.DeleteAsync(message, cancellationToken);
                redelivered++;

                continue;
            }

            await ScheduleRetryAsync(message, firstError, cancellationToken);

            break;
        }

        return redelivered;
    }

    private async Task<ContactCenterOutboxMessage> GetOrCreateMessageAsync(
        InteractionEvent interactionEvent,
        CancellationToken cancellationToken)
    {
        var existing = await _outboxStore.FindByEventIdAsync(interactionEvent.ItemId, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var now = _clock.UtcNow;
        var message = new ContactCenterOutboxMessage
        {
            ItemId = IdGenerator.GenerateId(),
            EventId = interactionEvent.ItemId,
            EventType = interactionEvent.EventType,
            Status = OutboxMessageStatus.Pending,
            NextAttemptUtc = now,
            CreatedUtc = now,
            ModifiedUtc = now,
        };

        await _outboxStore.CreateAsync(message, cancellationToken);

        return message;
    }

    private async Task<string> RunHandlersAsync(
        InteractionEvent interactionEvent,
        ContactCenterOutboxMessage message,
        CancellationToken cancellationToken)
    {
        string firstError = null;
        var completedHandlerTypes = message.CompletedHandlerTypes.ToHashSet(StringComparer.Ordinal);

        var handlerIndex = 0;

        foreach (var handler in _handlers)
        {
            var handlerType = $"{handler.GetType().AssemblyQualifiedName ?? handler.GetType().FullName ?? handler.GetType().Name}:{handlerIndex}";
            handlerIndex++;

            if (completedHandlerTypes.Contains(handlerType))
            {
                continue;
            }

            try
            {
                await handler.HandleAsync(interactionEvent, cancellationToken);

                completedHandlerTypes.Add(handlerType);
                message.CompletedHandlerTypes = completedHandlerTypes.ToArray();
            }
            catch (Exception ex)
            {
                firstError ??= ex.Message;

                _logger.LogError(
                    ex,
                    "An error occurred while handling the Contact Center event '{EventType}' for interaction '{InteractionId}' in handler '{Handler}'.",
                    interactionEvent.EventType,
                    interactionEvent.InteractionId,
                    handler.GetType().FullName);
            }
        }

        return firstError;
    }

    private async Task ScheduleRetryAsync(
        ContactCenterOutboxMessage message,
        string error,
        CancellationToken cancellationToken)
    {
        message.AttemptCount++;
        message.LastError = error;
        message.ModifiedUtc = _clock.UtcNow;

        if (message.AttemptCount >= MaxAttempts)
        {
            message.Status = OutboxMessageStatus.DeadLettered;

            _logger.LogError(
                "Dead-lettered Contact Center event '{EventType}' ({EventId}) after {Attempts} failed dispatch attempts: {Error}",
                message.EventType,
                message.EventId,
                message.AttemptCount,
                error);
        }
        else
        {
            message.NextAttemptUtc = _clock.UtcNow.Add(GetBackoff(message.AttemptCount));
        }

        await _outboxStore.UpdateAsync(message, cancellationToken);
    }

    private async Task DeadLetterAsync(ContactCenterOutboxMessage message, string reason, CancellationToken cancellationToken)
    {
        message.Status = OutboxMessageStatus.DeadLettered;
        message.LastError = reason;
        message.ModifiedUtc = _clock.UtcNow;

        await _outboxStore.UpdateAsync(message, cancellationToken);

        _logger.LogError("Dead-lettered Contact Center outbox message '{MessageId}': {Reason}", message.ItemId, reason);
    }

    private static TimeSpan GetBackoff(int attempt)
    {
        var exponent = Math.Min(attempt - 1, 30);
        var seconds = Math.Min(BaseBackoffSeconds * Math.Pow(2, exponent), MaxBackoffSeconds);

        return TimeSpan.FromSeconds(seconds);
    }
}
