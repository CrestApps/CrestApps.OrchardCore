using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Telemetry;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;
using YesSql;

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
    private static readonly TimeSpan _claimLease = TimeSpan.FromMinutes(5);

    private readonly IReadOnlyList<IContactCenterEventHandler> _handlers;
    private readonly IContactCenterOutboxStore _outboxStore;
    private readonly IInteractionEventStore _eventStore;
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterOutbox"/> class.
    /// </summary>
    /// <param name="handlers">The registered Contact Center event handlers.</param>
    /// <param name="outboxStore">The durable outbox message store.</param>
    /// <param name="eventStore">The durable interaction event store used to reload events for retry.</param>
    /// <param name="scopeExecutor">The executor used to isolate each due message in a fresh child scope.</param>
    /// <param name="workManager">The feature work manager used to fence dispatch during feature quiescence.</param>
    /// <param name="session">The tenant session used to commit claims before handler execution.</param>
    /// <param name="clock">The clock used to schedule retries.</param>
    /// <param name="logger">The logger instance.</param>
    public ContactCenterOutbox(
        IEnumerable<IContactCenterEventHandler> handlers,
        IContactCenterOutboxStore outboxStore,
        IInteractionEventStore eventStore,
        IContactCenterScopeExecutor scopeExecutor,
        IContactCenterFeatureWorkManager workManager,
        ISession session,
        IClock clock,
        ILogger<ContactCenterOutbox> logger)
    {
        _handlers = ValidateHandlers(handlers);
        _outboxStore = outboxStore;
        _eventStore = eventStore;
        _scopeExecutor = scopeExecutor;
        _workManager = workManager;
        _session = session;
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

            if (!Enum.IsDefined(handler.ReplaySafety) || handler.ReplaySafety == ContactCenterHandlerReplaySafety.Unspecified)
            {
                throw new InvalidOperationException(
                    $"The Contact Center event handler '{handlerId}' must declare an explicit ReplaySafety contract because outbox delivery is at-least-once.");
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
        using var workLease = _workManager.TryEnter(ContactCenterConstants.Feature.Area);

        if (workLease is null || !await TryClaimAsync(message, cancellationToken))
        {
            return;
        }

        var ownerToken = message.OwnerToken;
        var fenceToken = message.FenceToken;
        (var firstError, var handlerUnavailable) = await RunHandlersAsync(interactionEvent, message, cancellationToken);
        await SettleInFreshScopeAsync(
            message,
            ownerToken,
            fenceToken,
            firstError,
            handlerUnavailable,
            cancellationToken);

        if (firstError is not null)
        {
            _logger.LogWarning(
                "Scheduled Contact Center event '{EventType}' ({EventId}) for retry after a handler failure: {Error}",
                interactionEvent.EventType,
                OperationalLogRedactor.Pseudonymize(interactionEvent.ItemId, OperationalLogIdentifierCategory.Event),
                OperationalLogRedactor.Redact(firstError, OperationalLogFieldKind.FreeText));
        }
    }

    /// <inheritdoc/>
    public async Task<int> DispatchDueAsync(CancellationToken cancellationToken = default)
    {
        using var workLease = _workManager.TryEnter(ContactCenterConstants.Feature.Area);

        if (workLease is null)
        {
            return 0;
        }

        var now = _clock.UtcNow;
        var due = await _outboxStore.ListDueAsync(now, MaxBatchSize, cancellationToken);
        var redelivered = 0;

        foreach (var message in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var messageId = message.ItemId;

            try
            {
                var completed = false;

                // Isolate every due message in its own fresh Orchard child scope and YesSql session so a
                // poison message or a canceled session can never poison the remaining batch.
                await _scopeExecutor.ExecuteAsync<IContactCenterOutbox>(async outbox =>
                {
                    if (await outbox.DispatchDueMessageAsync(messageId, cancellationToken))
                    {
                        completed = true;
                    }
                });

                if (completed)
                {
                    redelivered++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // The message's isolated scope failed (for example an optimistic-concurrency loss committed
                // by another worker). The message stays pending and is reprocessed by the next pass in a
                // fresh scope, so continue draining the rest of the batch instead of blocking it.
                _logger.LogWarning(
                    "Isolated dispatch of Contact Center outbox message '{MessageId}' failed with {ExceptionType}; the message stays pending for the next pass.",
                    OperationalLogRedactor.Pseudonymize(messageId, OperationalLogIdentifierCategory.Event),
                    ex.GetType().Name);
            }
        }

        ContactCenterDiagnostics.RecordOutboxRedelivered(redelivered);

        return redelivered;
    }

    /// <inheritdoc/>
    public async Task<bool> DispatchDueMessageAsync(string messageId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(messageId);

        var message = await _outboxStore.FindByIdAsync(messageId, cancellationToken);

        if (message is null || !await TryClaimAsync(message, cancellationToken))
        {
            return false;
        }

        var ownerToken = message.OwnerToken;
        var fenceToken = message.FenceToken;

        if (string.IsNullOrEmpty(message.EventId))
        {
            await DeadLetterAsync(message, "The outbox message has no event reference.", cancellationToken);

            return false;
        }

        var interactionEvent = await _eventStore.FindByIdAsync(message.EventId, cancellationToken);

        if (interactionEvent is null)
        {
            await DeadLetterAsync(message, "The referenced event no longer exists.", cancellationToken);

            return false;
        }

        (var firstError, var handlerUnavailable) = await RunHandlersAsync(interactionEvent, message, cancellationToken);

        return await SettleInFreshScopeAsync(
            message,
            ownerToken,
            fenceToken,
            firstError,
            handlerUnavailable,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DispatchHandlerAsync(
        InteractionEvent interactionEvent,
        string handlerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);
        ArgumentException.ThrowIfNullOrEmpty(handlerId);

        var handler = _handlers.FirstOrDefault(candidate =>
            string.Equals(candidate.HandlerId, handlerId, StringComparison.Ordinal));

        if (handler is null)
        {
            throw new InvalidOperationException(
                $"The Contact Center event handler '{handlerId}' is not registered in the isolated dispatch scope.");
        }

        await handler.HandleAsync(interactionEvent, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> SettleClaimAsync(
        string messageId,
        string ownerToken,
        long fenceToken,
        IReadOnlyCollection<string> completedHandlerIds,
        string error,
        bool handlerUnavailable,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(messageId);
        ArgumentException.ThrowIfNullOrEmpty(ownerToken);
        ArgumentNullException.ThrowIfNull(completedHandlerIds);

        var message = await _outboxStore.FindByIdAsync(messageId, cancellationToken);

        if (message is null ||
            message.Status != OutboxMessageStatus.Claimed ||
            !string.Equals(message.OwnerToken, ownerToken, StringComparison.Ordinal) ||
            message.FenceToken != fenceToken)
        {
            throw new ConcurrencyException(new Document());
        }

        message.CompletedHandlerTypes = completedHandlerIds.ToArray();

        if (error is null)
        {
            await CompleteAsync(message, cancellationToken);

            return true;
        }

        await ScheduleRetryAsync(message, error, !handlerUnavailable, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        return false;
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
            ExpectedHandlerIds = _handlers.Select(handler => handler.HandlerId).ToArray(),
            NextAttemptUtc = now,
            CreatedUtc = now,
            ModifiedUtc = now,
        };

        await _outboxStore.CreateAsync(message, cancellationToken);

        return message;
    }

    private async Task<bool> TryClaimAsync(
        ContactCenterOutboxMessage message,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        if ((message.Status != OutboxMessageStatus.Pending &&
                message.Status != OutboxMessageStatus.Claimed) ||
            message.NextAttemptUtc > now)
        {
            return false;
        }

        message.Status = OutboxMessageStatus.Claimed;
        message.OwnerToken = Guid.NewGuid().ToString("N");
        message.FenceToken++;
        message.NextAttemptUtc = now.Add(_claimLease);
        message.ModifiedUtc = now;
        await _outboxStore.UpdateAsync(message, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<(string Error, bool HandlerUnavailable)> RunHandlersAsync(
        InteractionEvent interactionEvent,
        ContactCenterOutboxMessage message,
        CancellationToken cancellationToken)
    {
        string firstError = null;
        var handlerFailed = false;
        var handlerUnavailable = false;
        var persistedCompleted = message.CompletedHandlerTypes.ToHashSet(StringComparer.Ordinal);
        var completedHandlerIds = new HashSet<string>(persistedCompleted, StringComparer.Ordinal);
        var expectedHandlerIds = message.ExpectedHandlerIds?.Count > 0
            ? message.ExpectedHandlerIds
            : _handlers.Select(handler => handler.HandlerId).ToArray();

        foreach (var handlerId in expectedHandlerIds)
        {
            var handler = _handlers.FirstOrDefault(candidate =>
                string.Equals(candidate.HandlerId, handlerId, StringComparison.Ordinal));

            if (handler is null)
            {
                handlerUnavailable = true;
                firstError ??= $"The required Contact Center event handler '{handlerId}' is not available in the current feature shell.";

                continue;
            }

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
                // Commit each handler effect and its replay marker in an isolated child scope. A handler
                // exception rolls back that unit without canceling this message scope, allowing the retry
                // checkpoint to be persisted and later messages to continue.
                await _scopeExecutor.ExecuteAsync<IContactCenterOutbox>(outbox =>
                    outbox.DispatchHandlerAsync(interactionEvent, handlerId, cancellationToken));

                completedHandlerIds.Add(handlerId);
            }
            catch (Exception ex)
            {
                handlerFailed = true;
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

        return (firstError, handlerUnavailable && !handlerFailed);
    }

    private async Task<bool> SettleInFreshScopeAsync(
        ContactCenterOutboxMessage message,
        string ownerToken,
        long fenceToken,
        string error,
        bool handlerUnavailable,
        CancellationToken cancellationToken)
    {
        var completed = false;

        await _scopeExecutor.ExecuteAsync<IContactCenterOutbox>(async outbox =>
        {
            completed = await outbox.SettleClaimAsync(
                message.ItemId,
                ownerToken,
                fenceToken,
                message.CompletedHandlerTypes.ToArray(),
                error,
                handlerUnavailable,
                cancellationToken);
        });

        return completed;
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
        bool countAttempt,
        CancellationToken cancellationToken)
    {
        if (countAttempt)
        {
            message.AttemptCount++;
        }

        message.LastError = error;
        message.ModifiedUtc = _clock.UtcNow;
        message.OwnerToken = null;

        if (message.AttemptCount >= MaxAttempts)
        {
            message.Status = OutboxMessageStatus.DeadLettered;
            ContactCenterDiagnostics.RecordOutboxDeadLettered("retry_exhausted");

            _logger.LogError(
                "Dead-lettered Contact Center event '{EventType}' ({EventId}) after {Attempts} failed dispatch attempts: {Error}",
                message.EventType,
                OperationalLogRedactor.Pseudonymize(message.EventId, OperationalLogIdentifierCategory.Event),
                message.AttemptCount,
                OperationalLogRedactor.Redact(error, OperationalLogFieldKind.FreeText));
        }
        else
        {
            message.Status = OutboxMessageStatus.Pending;
            message.NextAttemptUtc = _clock.UtcNow.Add(GetBackoff(Math.Max(1, message.AttemptCount)));
        }

        await _outboxStore.UpdateAsync(message, cancellationToken);
    }

    private async Task CompleteAsync(
        ContactCenterOutboxMessage message,
        CancellationToken cancellationToken)
    {
        message.Status = OutboxMessageStatus.Completed;
        message.OwnerToken = null;
        message.ModifiedUtc = _clock.UtcNow;
        await _outboxStore.UpdateAsync(message, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        await _outboxStore.DeleteAsync(message, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
    }

    private async Task DeadLetterAsync(ContactCenterOutboxMessage message, string reason, CancellationToken cancellationToken)
    {
        message.Status = OutboxMessageStatus.DeadLettered;
        message.LastError = reason;
        message.ModifiedUtc = _clock.UtcNow;
        message.OwnerToken = null;
        ContactCenterDiagnostics.RecordOutboxDeadLettered("unrecoverable");

        await _outboxStore.UpdateAsync(message, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        _logger.LogError("Dead-lettered Contact Center outbox message '{MessageId}': {Reason}", OperationalLogRedactor.Pseudonymize(message.ItemId, OperationalLogIdentifierCategory.Event), reason);
    }

    private static TimeSpan GetBackoff(int attempt)
    {
        var exponent = Math.Min(attempt - 1, 30);
        var seconds = Math.Min(BaseBackoffSeconds * Math.Pow(2, exponent), MaxBackoffSeconds);

        return TimeSpan.FromSeconds(seconds);
    }
}
