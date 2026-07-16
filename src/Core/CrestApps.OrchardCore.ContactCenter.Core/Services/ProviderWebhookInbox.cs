using System.Security.Cryptography;
using System.Text;
using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides durable, idempotent provider webhook acceptance and retryable payload dispatch.
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
    private static readonly TimeSpan _tombstoneRetention = TimeSpan.FromDays(7);

    private const int BaseBackoffSeconds = 15;
    private const int MaxBackoffSeconds = 1800;

    private readonly IReadOnlyList<IProviderWebhookInboxHandler> _handlers;
    private readonly IProviderWebhookInboxStore _store;
    private readonly ISession _session;
    private readonly IDistributedLock _distributedLock;
    private readonly IProviderIdentityResolver _providerIdentityResolver;
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderWebhookInbox"/> class.
    /// </summary>
    /// <param name="handlers">The feature-scoped normalized payload handlers.</param>
    /// <param name="store">The durable inbox message store.</param>
    /// <param name="session">The tenant YesSql session used to commit acceptance and processing state.</param>
    /// <param name="distributedLock">The distributed lock used for idempotent acceptance and single-message dispatch.</param>
    /// <param name="providerIdentityResolver">The resolver used to canonicalize provider aliases before keying deliveries.</param>
    /// <param name="scopeExecutor">The executor used to isolate each due message in a fresh child scope.</param>
    /// <param name="clock">The clock used to stamp acceptance and retry times.</param>
    /// <param name="logger">The logger instance.</param>
    public ProviderWebhookInbox(
        IEnumerable<IProviderWebhookInboxHandler> handlers,
        IProviderWebhookInboxStore store,
        ISession session,
        IDistributedLock distributedLock,
        IProviderIdentityResolver providerIdentityResolver,
        IContactCenterScopeExecutor scopeExecutor,
        IClock clock,
        ILogger<ProviderWebhookInbox> logger)
    {
        _handlers = ValidateHandlers(handlers);
        _store = store;
        _session = session;
        _distributedLock = distributedLock;
        _providerIdentityResolver = providerIdentityResolver;
        _scopeExecutor = scopeExecutor;
        _clock = clock;
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

            if (!Enum.IsDefined(handler.ReplaySafety) || handler.ReplaySafety == ContactCenterHandlerReplaySafety.Unspecified)
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
        var providerName = _providerIdentityResolver.Canonicalize(delivery.ProviderName);

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

        await _store.CreateAsync(message, cancellationToken);
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
        var message = await _store.FindByIdAsync(messageId, cancellationToken);

        if (message is null)
        {
            return false;
        }

        var now = _clock.UtcNow;

        if ((message.Status != ProviderWebhookInboxStatus.Pending &&
                message.Status != ProviderWebhookInboxStatus.Claimed) ||
            message.NextAttemptUtc > now)
        {
            return false;
        }

        message.Status = ProviderWebhookInboxStatus.Claimed;
        message.OwnerToken = Guid.NewGuid().ToString("N");
        message.FenceToken++;
        message.NextAttemptUtc = now.Add(_claimLease);
        message.ModifiedUtc = now;
        await _store.UpdateAsync(message, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
        var ownerToken = message.OwnerToken;
        var fenceToken = message.FenceToken;

        var handler = _handlers.FirstOrDefault(candidate =>
            string.Equals(candidate.TechnicalName, message.HandlerName, StringComparison.Ordinal));

        if (handler is null)
        {
            message.Status = ProviderWebhookInboxStatus.Pending;
            message.OwnerToken = null;
            message.LastError = "HandlerUnavailable";
            message.NextAttemptUtc = _clock.UtcNow.Add(_missingHandlerDelay);
            message.ModifiedUtc = _clock.UtcNow;
            await _store.UpdateAsync(message, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            _logger.LogError(
                "No provider webhook inbox handler named '{HandlerName}' is registered for message '{MessageId}'.",
                message.HandlerName,
                OperationalLogRedactor.Pseudonymize(message.ItemId, OperationalLogIdentifierCategory.Event));

            return false;
        }

        try
        {
            await _scopeExecutor.ExecuteAsync<IProviderWebhookInbox>(inbox =>
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
                OperationalLogRedactor.RedactException(exception),
                "Provider webhook inbox handler '{HandlerName}' failed for message '{MessageId}'.",
                message.HandlerName,
                OperationalLogRedactor.Pseudonymize(message.ItemId, OperationalLogIdentifierCategory.Event));

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

        var message = await _store.FindByIdAsync(messageId, cancellationToken);

        if (message is null ||
            message.Status != ProviderWebhookInboxStatus.Claimed ||
            !string.Equals(message.OwnerToken, ownerToken, StringComparison.Ordinal) ||
            message.FenceToken != fenceToken)
        {
            throw new ConcurrencyException(new Document());
        }

        if (!succeeded)
        {
            await ScheduleRetryAsync(message, errorType, cancellationToken);

            return false;
        }

        message.Status = ProviderWebhookInboxStatus.Completed;
        message.OwnerToken = null;
        message.Payload = null;
        message.LastError = null;
        message.ProcessedUtc = _clock.UtcNow;
        message.NextAttemptUtc = message.ProcessedUtc.Value;
        message.ModifiedUtc = message.ProcessedUtc;
        await _store.UpdateAsync(message, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <inheritdoc/>
    public async Task<int> DispatchDueAsync(CancellationToken cancellationToken = default)
    {
        await PurgeExpiredTombstonesAsync(cancellationToken);

        var due = await _store.ListDueAsync(_clock.UtcNow, MaxBatchSize, cancellationToken);
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
                await _scopeExecutor.ExecuteAsync<IProviderWebhookInbox>(async inbox =>
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
                    OperationalLogRedactor.Pseudonymize(messageId, OperationalLogIdentifierCategory.Event),
                    exception.GetType().Name);
            }
        }

        return completed;
    }

    private async Task<int> PurgeExpiredTombstonesAsync(CancellationToken cancellationToken)
    {
        var cutoff = _clock.UtcNow.Subtract(_tombstoneRetention);
        var tombstones = await _store.ListProcessedBeforeAsync(
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

        await _scopeExecutor.ExecuteAsync<IProviderWebhookInbox>(async inbox =>
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
        ProviderWebhookInboxMessage message,
        string errorType,
        CancellationToken cancellationToken)
    {
        message.AttemptCount++;
        message.LastError = errorType;
        message.ModifiedUtc = _clock.UtcNow;
        message.OwnerToken = null;

        if (message.AttemptCount >= MaxAttempts)
        {
            message.Status = ProviderWebhookInboxStatus.DeadLettered;
        }
        else
        {
            message.Status = ProviderWebhookInboxStatus.Pending;
            message.NextAttemptUtc = _clock.UtcNow.Add(GetBackoff(message.AttemptCount));
        }

        await _store.UpdateAsync(message, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
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

    private static TimeSpan GetBackoff(int attempt)
    {
        var exponent = Math.Min(attempt - 1, 30);
        var seconds = Math.Min(BaseBackoffSeconds * Math.Pow(2, exponent), MaxBackoffSeconds);

        return TimeSpan.FromSeconds(seconds);
    }
}
