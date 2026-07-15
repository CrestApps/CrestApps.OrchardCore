using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Diagnostics;
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

    private readonly IReadOnlyList<IContactCenterEventHandler> _handlers;
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
        _handlers = ValidateHandlers(handlers);
        _outboxStore = outboxStore;
        _eventStore = eventStore;
        _clock = clock;
        _logger = logger;
    }

    private static IReadOnlyList<IContactCenterEventHandler> ValidateHandlers(IEnumerable<IContactCenterEventHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        var materialized = handlers as IReadOnlyList<IContactCenterEventHandler> ?? handlers.ToArray();
        var seenHandlerIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var handler in materialized)
        {
            var handlerId = handler.HandlerId;

            if (string.IsNullOrWhiteSpace(handlerId))
            {
                throw new InvalidOperationException(
                    $"The Contact Center event handler '{handler.GetType().FullName}' must expose a non-empty stable HandlerId.");
            }

            if (!seenHandlerIds.Add(handlerId))
            {
                throw new InvalidOperationException(
                    $"The Contact Center event handler id '{handlerId}' is registered by more than one handler. Handler ids must be unique so a failed handler is never skipped by another handler that shares its id.");
            }
        }

        return materialized;
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
            OperationalLogRedactor.Pseudonymize(interactionEvent.ItemId, OperationalLogIdentifierCategory.Event),
            OperationalLogRedactor.Redact(firstError, OperationalLogFieldKind.FreeText));
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
        var persistedCompleted = message.CompletedHandlerTypes.ToHashSet(StringComparer.Ordinal);
        var completedHandlerIds = new HashSet<string>(persistedCompleted, StringComparer.Ordinal);

        foreach (var handler in _handlers)
        {
            var handlerId = handler.HandlerId;

            if (TryResolveCompletedCheckpoint(handler, handlerId, persistedCompleted, out var legacyCheckpoint))
            {
                if (legacyCheckpoint is not null)
                {
                    completedHandlerIds.Remove(legacyCheckpoint);
                }

                completedHandlerIds.Add(handlerId);

                continue;
            }

            try
            {
                await handler.HandleAsync(interactionEvent, cancellationToken);

                completedHandlerIds.Add(handlerId);
            }
            catch (Exception ex)
            {
                firstError ??= ex.Message;

                _logger.LogError(
                    OperationalLogRedactor.RedactException(ex),
                    "An error occurred while handling the Contact Center event '{EventType}' for interaction '{InteractionId}' in handler '{Handler}'.",
                    interactionEvent.EventType,
                    OperationalLogRedactor.Pseudonymize(interactionEvent.InteractionId, OperationalLogIdentifierCategory.Interaction),
                    handlerId);
            }
        }

        // Persist the checkpoint using stable handler ids, repairing any legacy CLR-name/index entries so
        // completed handlers are never replayed after a rename, reorder, or assembly version change.
        message.CompletedHandlerTypes = completedHandlerIds.ToArray();

        return firstError;
    }

    private static bool TryResolveCompletedCheckpoint(
        IContactCenterEventHandler handler,
        string handlerId,
        HashSet<string> persistedCompleted,
        out string legacyCheckpoint)
    {
        legacyCheckpoint = null;

        if (!string.IsNullOrEmpty(handlerId) && persistedCompleted.Contains(handlerId))
        {
            return true;
        }

        // Deploy-safe legacy alias: pre-versioned checkpoints stored "{AssemblyQualifiedName}:{index}".
        // Treat a handler whose runtime type matches such a legacy entry as already completed.
        var legacyTypeName = handler.GetType().FullName;

        if (string.IsNullOrEmpty(legacyTypeName))
        {
            return false;
        }

        foreach (var entry in persistedCompleted)
        {
            if (entry.StartsWith(legacyTypeName + ",", StringComparison.Ordinal) ||
                entry.StartsWith(legacyTypeName + ":", StringComparison.Ordinal))
            {
                legacyCheckpoint = entry;

                return true;
            }
        }

        return false;
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
                OperationalLogRedactor.Pseudonymize(message.EventId, OperationalLogIdentifierCategory.Event),
                message.AttemptCount,
                OperationalLogRedactor.Redact(error, OperationalLogFieldKind.FreeText));
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

        _logger.LogError("Dead-lettered Contact Center outbox message '{MessageId}': {Reason}", OperationalLogRedactor.Pseudonymize(message.ItemId, OperationalLogIdentifierCategory.Event), reason);
    }

    private static TimeSpan GetBackoff(int attempt)
    {
        var exponent = Math.Min(attempt - 1, 30);
        var seconds = Math.Min(BaseBackoffSeconds * Math.Pow(2, exponent), MaxBackoffSeconds);

        return TimeSpan.FromSeconds(seconds);
    }
}
