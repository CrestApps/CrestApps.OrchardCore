using System.Security.Cryptography;
using System.Text;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Webhooks.Core.Models;
using CrestApps.OrchardCore.Webhooks.Models;
using CrestApps.OrchardCore.WorkQueues;
using CrestApps.OrchardCore.YesSql.Core.WorkQueues;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Webhooks.Core.Services;

/// <summary>
/// Provides durable, idempotent provider webhook acceptance and retryable payload dispatch. Claiming, fencing, retrying
/// and dead-lettering the deliveries is the shared leased work queue's job (<see cref="ILeasedWorkQueue{T}"/>); settled
/// deliveries stay as completed idempotency tombstones.
/// </summary>
public sealed class ProviderWebhookInbox : IProviderWebhookInbox
{
    /// <summary>
    /// The maximum number of failed processing attempts before a message is dead-lettered.
    /// </summary>
    public const int MaxAttempts = 10;

    /// <summary>
    /// The maximum number of due messages processed in one background pass.
    /// </summary>
    public const int MaxBatchSize = 100;

    /// <summary>
    /// The maximum number of processed tombstones purged in one cleanup pass.
    /// </summary>
    public const int MaxTombstoneCleanupBatchSize = 100;

    /// <summary>
    /// The maximum provider-scoped delivery identifier length supported by the durable index.
    /// </summary>
    public const int MaxDeliveryIdLength = 256;

    private static readonly TimeSpan _acceptanceLockTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _acceptanceLockExpiration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _dispatchLockTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _dispatchLockExpiration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _claimLease = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _missingHandlerDelay = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The number of days a settled delivery is kept as an idempotency tombstone. A provider redelivery arriving
    /// within this window is recognised as a duplicate purely because the row still exists, so retention must
    /// never delete one sooner.
    /// </summary>
    public const double TombstoneRetentionDays = 7;

    private static readonly Func<int, TimeSpan> _backoff = LeasedWorkBackoff.Exponential(TimeSpan.FromSeconds(15), TimeSpan.FromMinutes(30));

    private static readonly LeasedWorkQueueOptions _queueOptions = new()
    {
        CollectionName = ProviderWebhookInboxStorage.CollectionName,
        MaxAttempts = MaxAttempts,
    };

    private readonly IReadOnlyList<IProviderWebhookInboxHandler> _handlers;
    private readonly ProviderWebhookInboxOptions _options;
    private readonly IProviderWebhookInboxStore _store;
    private readonly CatalogLeasedWorkQueue<ProviderWebhookInboxMessage> _queue;
    private readonly ISession _session;
    private readonly IDistributedLock _distributedLock;
    private readonly IWebhookProviderNameNormalizer _providerNameNormalizer;
    private readonly IProviderWebhookInboxScopeExecutor _scopeExecutor;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderWebhookInbox"/> class.
    /// </summary>
    /// <param name="handlers">The feature-scoped normalized payload handlers.</param>
    /// <param name="store">The durable inbox message store.</param>
    /// <param name="session">The tenant YesSql session used to commit acceptance and processing state.</param>
    /// <param name="distributedLock">The distributed lock used for idempotent acceptance and single-message dispatch.</param>
    /// <param name="providerNameNormalizer">The normalizer used to canonicalize provider aliases before keying deliveries.</param>
    /// <param name="scopeExecutor">The executor used to isolate each due message in a fresh child scope.</param>
    /// <param name="clock">The clock used to stamp acceptance and retry times.</param>
    /// <param name="options">The inbox options, which hold how long settled deliveries are kept.</param>
    /// <param name="logger">The logger instance.</param>
    public ProviderWebhookInbox(
        IEnumerable<IProviderWebhookInboxHandler> handlers,
        IProviderWebhookInboxStore store,
        ISession session,
        IDistributedLock distributedLock,
        IWebhookProviderNameNormalizer providerNameNormalizer,
        IProviderWebhookInboxScopeExecutor scopeExecutor,
        IClock clock,
        IOptions<ProviderWebhookInboxOptions> options,
        ILogger<ProviderWebhookInbox> logger)
    {
        _handlers = ValidateHandlers(handlers);
        _store = store;
        _session = session;

        // Each delivery runs in an isolated scope with a session of its own (see the scope executor), so the queue
        // commits claims and settlements in that session.
        _queue = new CatalogLeasedWorkQueue<ProviderWebhookInboxMessage>(store, session, clock, _queueOptions, store.GetDueAsync);
        _distributedLock = distributedLock;
        _providerNameNormalizer = providerNameNormalizer;
        _scopeExecutor = scopeExecutor;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    private static IReadOnlyList<IProviderWebhookInboxHandler> ValidateHandlers(IEnumerable<IProviderWebhookInboxHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        var materialized = handlers as IReadOnlyList<IProviderWebhookInboxHandler> ?? handlers.ToArray();
        var seenTechnicalNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var handler in materialized)
        {
            var technicalName = handler.TechnicalName;

            if (string.IsNullOrWhiteSpace(technicalName))
            {
                throw new InvalidOperationException(
                    $"The provider webhook inbox handler '{handler.GetType().FullName}' must expose a non-empty stable TechnicalName.");
            }

            if (!Enum.IsDefined(handler.ReplaySafety) || handler.ReplaySafety == WebhookHandlerReplaySafety.Unspecified)
            {
                throw new InvalidOperationException(
                    $"The provider webhook inbox handler '{technicalName}' must declare an explicit ReplaySafety contract because provider delivery is at-least-once.");
            }

            if (!seenTechnicalNames.Add(technicalName))
            {
                throw new InvalidOperationException(
                    $"The provider webhook inbox handler technical name '{technicalName}' is registered by more than one handler. Technical names must be unique so a persisted payload routes to exactly one handler.");
            }
        }

        return materialized;
    }

    /// <inheritdoc/>
    public async Task<ProviderWebhookInboxAcceptanceResult> AcceptAsync(
        ProviderWebhookInboxDelivery delivery,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentException.ThrowIfNullOrEmpty(delivery.ProviderName);
        ArgumentException.ThrowIfNullOrEmpty(delivery.DeliveryId);
        ArgumentException.ThrowIfNullOrEmpty(delivery.HandlerName);
        ArgumentException.ThrowIfNullOrEmpty(delivery.Payload);

        if (delivery.DeliveryId.Length > MaxDeliveryIdLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(delivery),
                $"The provider delivery identifier cannot exceed {MaxDeliveryIdLength} characters.");
        }

        // Canonicalize the provider identity before building the delivery lock and idempotency key so that
        // provider aliases resolve to a single stable identity and share one durable uniqueness constraint.
        var providerName = _providerNameNormalizer.Normalize(delivery.ProviderName);

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            GetDeliveryLockKey(providerName, delivery.DeliveryId),
            _acceptanceLockTimeout,
            _acceptanceLockExpiration);

        if (!locked)
        {
            return new ProviderWebhookInboxAcceptanceResult
            {
                Status = ProviderWebhookInboxAcceptanceStatus.Busy,
            };
        }

        await using var acquiredLock = locker;
        var existing = await _store.FindByDeliveryAsync(
            providerName,
            delivery.DeliveryId,
            cancellationToken);

        if (existing is not null)
        {
            return new ProviderWebhookInboxAcceptanceResult
            {
                Status = ProviderWebhookInboxAcceptanceStatus.Duplicate,
                MessageId = existing.ItemId,
            };
        }

        var now = _clock.UtcNow;
        var message = new ProviderWebhookInboxMessage
        {
            ItemId = IdGenerator.GenerateId(),
            ProviderName = providerName,
            DeliveryId = delivery.DeliveryId,
            HandlerName = delivery.HandlerName,
            Payload = delivery.Payload,
            Status = ProviderWebhookInboxStatus.Pending,
            NextAttemptUtc = now,
            CreatedUtc = now,
            ModifiedUtc = now,
        };

        await _queue.EnqueueAsync(message, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        return new ProviderWebhookInboxAcceptanceResult
        {
            Status = ProviderWebhookInboxAcceptanceStatus.Accepted,
            MessageId = message.ItemId,
        };
    }

    /// <inheritdoc/>
    public async Task<bool> DispatchAsync(string messageId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(messageId);

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            GetDispatchLockKey(messageId),
            _dispatchLockTimeout,
            _dispatchLockExpiration);

        if (!locked)
        {
            return false;
        }

        await using var acquiredLock = locker;
        // A delivery that does not exist or is not due is left as it is.
        var message = await _queue.TryClaimAsync(messageId, _claimLease, cancellationToken);

        if (message is null)
        {
            return false;
        }

        var ownerToken = message.OwnerToken;
        var fenceToken = message.FenceToken;

        var handler = _handlers.FirstOrDefault(candidate =>
            string.Equals(candidate.TechnicalName, message.HandlerName, StringComparison.Ordinal));

        if (handler is null)
        {
            // Waiting for a handler that is not enabled yet does not use up an attempt.
            await _queue.RetryAsync(message, "HandlerUnavailable", _ => _missingHandlerDelay, countAttempt: false, cancellationToken: cancellationToken);

            _logger.LogError(
                "No provider webhook inbox handler named '{HandlerName}' is registered for message '{MessageId}'.",
                message.HandlerName,
                message.ItemId.SanitizeLogValue());

            return false;
        }

        try
        {
            await _scopeExecutor.ExecuteAsync(inbox =>
                inbox.DispatchHandlerAsync(message.HandlerName, message.Payload, cancellationToken));

            return await SettleInFreshScopeAsync(
                message.ItemId,
                ownerToken,
                fenceToken,
                true,
                null,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ConcurrencyException)
        {
            // Another worker committed a conflicting change to an aggregate this delivery touched (for
            // example a concurrent provider event for the same call). The YesSql session is now canceled and
            // must never be reused, so do not schedule a retry here. The durable claim expires and the message
            // is reclaimed in a fresh scope by the next eligible dispatch pass.
            throw;
        }
        catch (Exception exception)
        {
            await SettleInFreshScopeAsync(
                message.ItemId,
                ownerToken,
                fenceToken,
                false,
                exception.GetType().FullName,
                cancellationToken);

            _logger.LogError(
                exception,
                "Provider webhook inbox handler '{HandlerName}' failed for message '{MessageId}'.",
                message.HandlerName,
                message.ItemId.SanitizeLogValue());

            return false;
        }
    }

    /// <inheritdoc/>
    public async Task DispatchHandlerAsync(
        string handlerName,
        string payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(handlerName);
        ArgumentException.ThrowIfNullOrEmpty(payload);

        var handler = _handlers.FirstOrDefault(candidate =>
            string.Equals(candidate.TechnicalName, handlerName, StringComparison.Ordinal));

        if (handler is null)
        {
            throw new InvalidOperationException(
                $"The provider webhook inbox handler '{handlerName}' is not registered in the isolated dispatch scope.");
        }

        await handler.HandleAsync(payload, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> SettleClaimAsync(
        string messageId,
        string ownerToken,
        long fenceToken,
        bool succeeded,
        string errorType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(messageId);
        ArgumentException.ThrowIfNullOrEmpty(ownerToken);

        // The queue settles only while this worker still holds the claim; a lost claim surfaces as the concurrency
        // failure the caller's isolated scope already expects.
        var claim = new ProviderWebhookInboxMessage
        {
            ItemId = messageId,
            OwnerToken = ownerToken,
            FenceToken = fenceToken,
        };

        if (!succeeded)
        {
            await ScheduleRetryAsync(claim, errorType, cancellationToken);

            return false;
        }

        // The settled delivery stays as an idempotency tombstone without its payload, aged by when it settled.
        var completed = await _queue.CompleteAsync(claim, message =>
        {
            message.Payload = null;
            message.ProcessedUtc = message.NextAttemptUtc;
        }, cancellationToken);

        if (!completed)
        {
            throw new ConcurrencyException(new Document());
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<int> DispatchDueAsync(CancellationToken cancellationToken = default)
    {
        await PurgeExpiredTombstonesAsync(cancellationToken);

        var due = await _store.GetDueAsync(_clock.UtcNow, MaxBatchSize, cancellationToken);
        var completed = 0;

        foreach (var message in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var messageId = message.ItemId;

            try
            {
                var processed = false;

                // Isolate every due message in its own fresh Orchard child scope and YesSql session so a
                // concurrency loss or a poison delivery can never poison the remaining batch.
                await _scopeExecutor.ExecuteAsync(async inbox =>
                {
                    if (await inbox.DispatchAsync(messageId, cancellationToken))
                    {
                        processed = true;
                    }
                });

                if (processed)
                {
                    completed++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // The message's isolated scope failed (for example a concurrent worker won ownership of an
                // aggregate this delivery touched). The message stays pending and is reprocessed by the next
                // pass in a fresh scope, so continue draining the rest of the batch.
                _logger.LogWarning(
                    "Isolated dispatch of provider webhook inbox message '{MessageId}' failed with {ExceptionType}; the message stays pending for the next pass.",
                    messageId.SanitizeLogValue(),
                    exception.GetType().Name);
            }
        }

        return completed;
    }

    private async Task<int> PurgeExpiredTombstonesAsync(CancellationToken cancellationToken)
    {
        // This sweep and the retention policy both delete settled inbox rows, so the shorter of the two would
        // decide the table's real window. Taking the longer keeps the configured retention window meaningful
        // while never letting the duplicate-detection horizon be shortened below what the inbox guarantees.
        var retentionDays = Math.Max(TombstoneRetentionDays, _options.RetentionDays);
        var cutoff = _clock.UtcNow.Subtract(TimeSpan.FromDays(retentionDays));
        var tombstones = await _store.GetProcessedBeforeAsync(
            cutoff,
            MaxTombstoneCleanupBatchSize,
            cancellationToken);
        var count = 0;

        foreach (var tombstone in tombstones)
        {
            await _store.DeleteAsync(tombstone, cancellationToken);
            count++;
        }

        if (count > 0)
        {
            await _session.SaveChangesAsync(cancellationToken);
        }

        return count;
    }

    private async Task<bool> SettleInFreshScopeAsync(
        string messageId,
        string ownerToken,
        long fenceToken,
        bool succeeded,
        string errorType,
        CancellationToken cancellationToken)
    {
        var completed = false;

        await _scopeExecutor.ExecuteAsync(async inbox =>
        {
            completed = await inbox.SettleClaimAsync(
                messageId,
                ownerToken,
                fenceToken,
                succeeded,
                errorType,
                cancellationToken);
        });

        return completed;
    }

    private async Task ScheduleRetryAsync(
        ProviderWebhookInboxMessage claim,
        string errorType,
        CancellationToken cancellationToken)
    {
        var status = await _queue.RetryAsync(claim, errorType, _backoff, update: message =>
        {
            if (message.Status == ProviderWebhookInboxStatus.DeadLettered)
            {
                // This is the age settled deliveries are purged by. Without it a dead letter is never selected.
                message.ProcessedUtc = _clock.UtcNow;
            }
        }, cancellationToken: cancellationToken);

        if (status is null)
        {
            throw new ConcurrencyException(new Document());
        }
    }

    private static string GetDeliveryLockKey(string providerName, string deliveryId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{providerName}\n{deliveryId}"));

        return $"ContactCenterProviderWebhookInbox:Accept:{Convert.ToHexString(bytes)}";
    }

    private static string GetDispatchLockKey(string messageId)
    {
        return $"ContactCenterProviderWebhookInbox:Dispatch:{messageId}";
    }
}
