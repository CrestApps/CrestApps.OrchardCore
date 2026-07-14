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
    /// The maximum provider-scoped delivery identifier length supported by the durable index.
    /// </summary>
    public const int MaxDeliveryIdLength = 256;

    private static readonly TimeSpan _acceptanceLockTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _acceptanceLockExpiration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _dispatchLockTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _dispatchLockExpiration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _missingHandlerDelay = TimeSpan.FromMinutes(5);

    private const int BaseBackoffSeconds = 15;
    private const int MaxBackoffSeconds = 1800;

    private readonly IEnumerable<IProviderWebhookInboxHandler> _handlers;
    private readonly IProviderWebhookInboxStore _store;
    private readonly ISession _session;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderWebhookInbox"/> class.
    /// </summary>
    /// <param name="handlers">The feature-scoped normalized payload handlers.</param>
    /// <param name="store">The durable inbox message store.</param>
    /// <param name="session">The tenant YesSql session used to commit acceptance and processing state.</param>
    /// <param name="distributedLock">The distributed lock used for idempotent acceptance and single-message dispatch.</param>
    /// <param name="clock">The clock used to stamp acceptance and retry times.</param>
    /// <param name="logger">The logger instance.</param>
    public ProviderWebhookInbox(
        IEnumerable<IProviderWebhookInboxHandler> handlers,
        IProviderWebhookInboxStore store,
        ISession session,
        IDistributedLock distributedLock,
        IClock clock,
        ILogger<ProviderWebhookInbox> logger)
    {
        _handlers = handlers;
        _store = store;
        _session = session;
        _distributedLock = distributedLock;
        _clock = clock;
        _logger = logger;
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

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            GetDeliveryLockKey(delivery.ProviderName, delivery.DeliveryId),
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
            delivery.ProviderName,
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
            ProviderName = delivery.ProviderName,
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

        if (message is null || message.Status != ProviderWebhookInboxStatus.Pending)
        {
            return false;
        }

        var handler = _handlers.FirstOrDefault(candidate =>
            string.Equals(candidate.TechnicalName, message.HandlerName, StringComparison.Ordinal));

        if (handler is null)
        {
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
            await handler.HandleAsync(message.Payload, cancellationToken);
            await _store.DeleteAsync(message, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await ScheduleRetryAsync(message, exception.GetType(), cancellationToken);

            _logger.LogError(
                OperationalLogRedactor.RedactException(exception),
                "Provider webhook inbox handler '{HandlerName}' failed for message '{MessageId}'.",
                message.HandlerName,
                OperationalLogRedactor.Pseudonymize(message.ItemId, OperationalLogIdentifierCategory.Event));

            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<int> DispatchDueAsync(CancellationToken cancellationToken = default)
    {
        var due = await _store.ListDueAsync(_clock.UtcNow, MaxBatchSize, cancellationToken);
        var completed = 0;

        foreach (var message in due)
        {
            if (await DispatchAsync(message.ItemId, cancellationToken))
            {
                completed++;
            }
        }

        return completed;
    }

    private async Task ScheduleRetryAsync(
        ProviderWebhookInboxMessage message,
        Type exceptionType,
        CancellationToken cancellationToken)
    {
        message.AttemptCount++;
        message.LastError = exceptionType.FullName;
        message.ModifiedUtc = _clock.UtcNow;

        if (message.AttemptCount >= MaxAttempts)
        {
            message.Status = ProviderWebhookInboxStatus.DeadLettered;
        }
        else
        {
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
