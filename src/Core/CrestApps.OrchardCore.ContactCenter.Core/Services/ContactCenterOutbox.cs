using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Telemetry;
using CrestApps.OrchardCore.WorkQueues;
using CrestApps.OrchardCore.YesSql.Core.WorkQueues;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.BackgroundTasks;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default <see cref="IContactCenterOutbox"/> implementation. Every event is persisted as a
/// pending <see cref="ContactCenterOutboxMessage"/> before dispatch. Registered
/// <see cref="IContactCenterEventHandler"/> instances are tracked individually and incomplete handlers are
/// redelivered with exponential back-off. Claiming, fencing, retrying and dead-lettering the messages is the shared
/// leased work queue's job (<see cref="ILeasedWorkQueue{T}"/>); this class runs the handlers.
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

    private static readonly TimeSpan _claimLease = TimeSpan.FromMinutes(5);
    private static readonly Func<int, TimeSpan> _backoff = LeasedWorkBackoff.Exponential(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(30));

    // Completed messages are removed: nothing reads the completed state as domain data.
    private static readonly LeasedWorkQueueOptions _queueOptions = new()
    {
        CollectionName = ContactCenterStorage.CollectionName,
        MaxAttempts = MaxAttempts,
        RemoveCompletedItems = true,
    };

    private readonly IReadOnlyList<IContactCenterEventHandler> _handlers;
    private readonly IContactCenterOutboxStore _outboxStore;
    private readonly CatalogLeasedWorkQueue<ContactCenterOutboxMessage> _queue;
    private readonly IInteractionEventStore _eventStore;
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

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
    /// <param name="httpContextAccessor">The accessor a background HTTP context is set on for handlers run outside a request.</param>
    public ContactCenterOutbox(
        IEnumerable<IContactCenterEventHandler> handlers,
        IContactCenterOutboxStore outboxStore,
        IInteractionEventStore eventStore,
        IContactCenterScopeExecutor scopeExecutor,
        IContactCenterFeatureWorkManager workManager,
        ISession session,
        IClock clock,
        ILogger<ContactCenterOutbox> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _handlers = ValidateHandlers(handlers);
        _outboxStore = outboxStore;

        // Each message runs in an isolated scope with a session of its own (see the scope executor), so the queue
        // commits claims and settlements in that session.
        _queue = new CatalogLeasedWorkQueue<ContactCenterOutboxMessage>(outboxStore, session, clock, _queueOptions, outboxStore.GetDueAsync);
        _eventStore = eventStore;
        _scopeExecutor = scopeExecutor;
        _workManager = workManager;
        _clock = clock;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
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

        if (workLease is null)
        {
            return;
        }

        ContactCenterOutboxMessage claimed;

        try
        {
            claimed = await TryClaimAsync(message, cancellationToken);

            if (claimed is null)
            {
                return;
            }
        }
        catch (ConcurrencyException)
        {
            // The redelivery pass claimed the message first and is running its handlers; this inline dispatch has
            // nothing left to do. Thrown out of the deferred task, the lost claim was logged as an error.
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Contact Center event '{EventType}' ({EventId}) was claimed by another dispatcher first.",
                    interactionEvent.EventType,
                    interactionEvent.ItemId.SanitizeLogValue());
            }

            return;
        }

        (var firstError, var handlerUnavailable) = await RunHandlersAsync(interactionEvent, claimed, cancellationToken);
        await SettleInFreshScopeAsync(
            claimed,
            claimed.OwnerToken,
            claimed.FenceToken,
            firstError,
            handlerUnavailable,
            cancellationToken);

        if (firstError is not null)
        {
            _logger.LogWarning(
                "Scheduled Contact Center event '{EventType}' ({EventId}) for retry after a handler failure: {Error}",
                interactionEvent.EventType,
                interactionEvent.ItemId.SanitizeLogValue(),
                firstError.SanitizeLogValue());
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
        var due = await _outboxStore.GetDueAsync(now, MaxBatchSize, cancellationToken);
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
                    messageId.SanitizeLogValue(),
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

        // A message that does not exist or is not due is left as it is.
        var message = await _queue.TryClaimAsync(messageId, _claimLease, cancellationToken);

        if (message is null)
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

        // The queue settles only while this worker still holds the claim; a lost claim surfaces as the concurrency
        // failure the caller's isolated scope already expects.
        var claim = new ContactCenterOutboxMessage
        {
            ItemId = messageId,
            OwnerToken = ownerToken,
            FenceToken = fenceToken,
        };

        if (error is null)
        {
            // Removal is a single unit of work. If this transaction fails the message stays claimed and claim expiry
            // redelivers it. The per-handler checkpoint is not persisted on this path, so that redelivery replays every
            // handler and is safe only because handlers are required to be idempotent; the checkpoint exists to avoid
            // replay after a *failed* dispatch, which is persisted.
            if (!await _queue.CompleteAsync(claim, cancellationToken: cancellationToken))
            {
                throw new ConcurrencyException(new Document());
            }

            return true;
        }

        ContactCenterOutboxMessage settled = null;
        var status = await _queue.RetryAsync(
            claim,
            error,
            _backoff,
            countAttempt: !handlerUnavailable,
            update: message =>
            {
                message.CompletedHandlerTypes = completedHandlerIds.ToArray();
                settled = message;
            },
            cancellationToken: cancellationToken);

        if (status is null)
        {
            throw new ConcurrencyException(new Document());
        }

        if (status == LeasedWorkItemStatus.DeadLettered)
        {
            ContactCenterDiagnostics.RecordOutboxDeadLettered("retry_exhausted");

            _logger.LogError(
                "Dead-lettered Contact Center event '{EventType}' ({EventId}) after {Attempts} failed dispatch attempts: {Error}",
                settled.EventType,
                settled.EventId.SanitizeLogValue(),
                settled.AttemptCount,
                error.SanitizeLogValue());
        }

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

        await _queue.EnqueueAsync(message, cancellationToken);

        return message;
    }

    private async Task<ContactCenterOutboxMessage> TryClaimAsync(
        ContactCenterOutboxMessage message,
        CancellationToken cancellationToken)
    {
        // Check the copy in hand first, so a message that is not due costs no write.
        if (!LeasedWorkQueueBase<ContactCenterOutboxMessage>.IsClaimable(message, _clock.UtcNow))
        {
            return null;
        }

        return await _queue.TryClaimAsync(message.ItemId, _claimLease, cancellationToken);
    }

    private async Task<(string Error, bool HandlerUnavailable)> RunHandlersAsync(
        InteractionEvent interactionEvent,
        ContactCenterOutboxMessage message,
        CancellationToken cancellationToken)
    {
        // Handlers run after the commit that published the event, which can be after the request that made it has ended
        // (an answer command its timeout gave up waiting on keeps running), or on the background retry pass. Without an
        // HTTP context then, triggering a workflow failed with a NullReferenceException in Orchard's
        // PreviewTemplatesProvider, which reads HttpContext.Items when the activity library is built, and the workflow
        // never ran. They get a background context for the tenant, as Orchard's own background tasks do.
        if (_httpContextAccessor.HttpContext is not null ||
            ShellScope.Context is null)
        {
            return await RunHandlersInContextAsync(interactionEvent, message, cancellationToken);
        }

        _httpContextAccessor.HttpContext = ShellScope.Context.CreateHttpContext();

        try
        {
            return await RunHandlersInContextAsync(interactionEvent, message, cancellationToken);
        }
        finally
        {
            _httpContextAccessor.HttpContext = null;
        }
    }

    private async Task<(string Error, bool HandlerUnavailable)> RunHandlersInContextAsync(
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
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                handlerFailed = true;
                firstError ??= ex.Message;

                _logger.LogError(
                    ex,
                    "An error occurred while handling the Contact Center event '{EventType}' for interaction '{InteractionId}' in handler '{Handler}'.",
                    interactionEvent.EventType,
                    interactionEvent.InteractionId.SanitizeLogValue(),
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

    private async Task DeadLetterAsync(ContactCenterOutboxMessage message, string reason, CancellationToken cancellationToken)
    {
        if (!await _queue.AbandonAsync(message, reason, cancellationToken: cancellationToken))
        {
            return;
        }

        ContactCenterDiagnostics.RecordOutboxDeadLettered("unrecoverable");

        _logger.LogError("Dead-lettered Contact Center outbox message '{MessageId}': {Reason}", message.ItemId.SanitizeLogValue(), reason);
    }
}
