using CrestApps.OrchardCore.Fulfillment.Core.Models;
using CrestApps.OrchardCore.Fulfillment.Models;
using CrestApps.OrchardCore.Fulfillment.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Fulfillment.Core.Services;

/// <summary>
/// Records paid orders for the fulfillment partners and sends them, once. A request is stored as being sent before the
/// partner is called, and any outcome that is not clear goes to a person instead of being sent again.
/// </summary>
public sealed class FulfillmentSender
{
    /// <summary>
    /// The most times a request the partner asked to wait on is tried before a person is asked.
    /// </summary>
    internal const int MaxAttempts = 10;

    /// <summary>
    /// How long a request may stay in <see cref="FulfillmentRequestStatus.Sending"/> before a person is asked.
    /// </summary>
    internal static readonly TimeSpan StuckAfter = TimeSpan.FromMinutes(15);

    private readonly FulfillmentRequestStore _store;
    private readonly IOrderStore _orders;
    private readonly IEnumerable<IFulfillmentProvider> _providers;
    private readonly IClock _clock;
    private readonly IDistributedLock _distributedLock;
    private readonly ILogger _logger;

    private bool _sessionUnusable;

    /// <summary>
    /// Initializes a new instance of the <see cref="FulfillmentSender"/> class.
    /// </summary>
    /// <param name="store">The request store.</param>
    /// <param name="orders">The order store.</param>
    /// <param name="providers">The fulfillment partners whose features are on.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="distributedLock">The distributed lock, so one sender at a time handles an order and partner.</param>
    /// <param name="logger">The logger.</param>
    public FulfillmentSender(
        FulfillmentRequestStore store,
        IOrderStore orders,
        IEnumerable<IFulfillmentProvider> providers,
        IClock clock,
        IDistributedLock distributedLock,
        ILogger<FulfillmentSender> logger)
    {
        _store = store;
        _orders = orders;
        _providers = providers;
        _clock = clock;
        _distributedLock = distributedLock;
        _logger = logger;
    }

    /// <summary>
    /// Records a pending request for each connected partner that has none for the order yet. It reads saved settings
    /// only and never calls a partner, so it is safe while an order is being paid.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>All the order's requests.</returns>
    public async Task<IReadOnlyList<FulfillmentRequest>> RecordAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var requests = (await _store.ListByOrderAsync(order.ItemId, cancellationToken)).ToList();
        var now = _clock.UtcNow;

        foreach (var provider in _providers)
        {
            if (requests.Any(request => string.Equals(request.ProviderName, provider.Name, StringComparison.Ordinal)) || !await provider.IsConnectedAsync())
            {
                continue;
            }

            var request = new FulfillmentRequest
            {
                ItemId = IdGenerator.GenerateId(),
                OrderId = order.ItemId,
                OrderNumber = order.OrderNumber,
                ProviderName = provider.Name,
                Status = FulfillmentRequestStatus.Pending,
                NextAttemptUtc = now,
                CreatedUtc = now,
                ModifiedUtc = now,
            };

            await _store.CreateAsync(request, cancellationToken);
            requests.Add(request);
        }

        return requests;
    }

    /// <summary>
    /// Sends one request now. A request already sent, or being sent, is left as it is.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The request as it ended.</returns>
    public async Task<FulfillmentRequest> SendAsync(FulfillmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Status is FulfillmentRequestStatus.Sent or FulfillmentRequestStatus.Sending)
        {
            return request;
        }

        if (_sessionUnusable)
        {
            return request;
        }

        var provider = _providers.FirstOrDefault(item => string.Equals(item.Name, request.ProviderName, StringComparison.Ordinal));

        if (provider is null)
        {
            return await SettleAsync(request, FulfillmentRequestStatus.NeedsAttention, $"The {request.ProviderName} feature is turned off.", cancellationToken);
        }

        // One sender at a time per order and partner, so two records of the same order (a double click, two people)
        // can never both be sent.
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockKey(request), TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5));

        if (!locked)
        {
            return request;
        }

        await using (locker)
        {
            var siblings = await _store.ListByOrderAsync(request.OrderId, cancellationToken);

            if (siblings.Any(item => item.ItemId != request.ItemId
                && string.Equals(item.ProviderName, request.ProviderName, StringComparison.Ordinal)
                && item.Status is FulfillmentRequestStatus.Sent or FulfillmentRequestStatus.Sending))
            {
                return await SettleAsync(request, FulfillmentRequestStatus.NothingToSend, $"Not sent: this order is already being sent, or was sent, to {provider.DisplayName}.", cancellationToken);
            }

            var check = FulfillmentOrderFactory.Check(await _orders.FindByIdAsync(request.OrderId, cancellationToken));

            if (!check.CanSend)
            {
                return await SettleAsync(request, check.NothingToSend ? FulfillmentRequestStatus.NothingToSend : FulfillmentRequestStatus.NeedsAttention, "Not sent: " + check.Reason, cancellationToken);
            }

            // Stored before the partner is called: if the site stops now, the request stays in Sending and a person is asked.
            request.Status = FulfillmentRequestStatus.Sending;
            request.Attempts++;
            request.ModifiedUtc = _clock.UtcNow;

            try
            {
                await _store.UpdateAsync(request, cancellationToken);
                await _store.CommitAsync(cancellationToken);
            }
            catch (ConcurrencyException)
            {
                // YesSql cancels a session that lost a race, so nothing more is saved with it.
                _sessionUnusable = true;

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("The fulfillment request '{RequestId}' was taken by another worker.", request.ItemId);
                }

                return request;
            }

            FulfillmentSubmitResult result;

            try
            {
                // Not cancelled from here: once the partner may have the order, the outcome must be recorded.
                result = await provider.SubmitAsync(check.Order, CancellationToken.None);
            }
            catch (Exception exception)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(exception, "Sending order '{OrderNumber}' to {Provider} failed.", request.OrderNumber, request.ProviderName);
                }

                result = FulfillmentSubmitResult.NeedsAttention($"Unclear: sending failed unexpectedly, so it is not known whether {provider.DisplayName} created the order. Check there before you retry.");
            }

            return await RecordResultAsync(request, result, CancellationToken.None);
        }
    }

    /// <summary>
    /// Sends the pending requests that are due.
    /// </summary>
    /// <param name="count">The most requests to send.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many requests were looked at.</returns>
    public async Task<int> ProcessDueAsync(int count, CancellationToken cancellationToken = default)
    {
        var due = await _store.ListDueAsync(_clock.UtcNow, count, cancellationToken);

        foreach (var request in due)
        {
            if (_sessionUnusable)
            {
                break;
            }

            await SendAsync(request, cancellationToken);
        }

        return due.Count;
    }

    /// <summary>
    /// Asks a person about requests left in <see cref="FulfillmentRequestStatus.Sending"/>, which happens when the
    /// site stops while a partner is being called.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many requests were flagged.</returns>
    public async Task<int> FlagStuckAsync(CancellationToken cancellationToken = default)
    {
        var stuck = await _store.ListStuckAsync(_clock.UtcNow - StuckAfter, 50, cancellationToken);

        foreach (var request in stuck)
        {
            var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockKey(request), TimeSpan.Zero, TimeSpan.FromMinutes(1));

            if (!locked)
            {
                continue;
            }

            await using var held = locker;

            await SettleAsync(request, FulfillmentRequestStatus.NeedsAttention, $"Unclear: sending was interrupted, so it is not known whether {request.ProviderName} created the order. Look for order {request.OrderNumber} there before you retry.", cancellationToken);
        }

        return stuck.Count;
    }

    private Task<FulfillmentRequest> RecordResultAsync(FulfillmentRequest request, FulfillmentSubmitResult result, CancellationToken cancellationToken)
    {
        switch (result.Status)
        {
            case FulfillmentSubmitStatus.Sent:
                request.ExternalOrderId = result.ExternalOrderId;
                request.SentUtc = _clock.UtcNow;

                return SettleAsync(request, FulfillmentRequestStatus.Sent, result.Message, cancellationToken);

            case FulfillmentSubmitStatus.NothingToSend:
                return SettleAsync(request, FulfillmentRequestStatus.NothingToSend, result.Message, cancellationToken);

            case FulfillmentSubmitStatus.RetryLater when request.Attempts < MaxAttempts:
                request.NextAttemptUtc = _clock.UtcNow + RetryWait(request.Attempts);

                return SettleAsync(request, FulfillmentRequestStatus.Pending, result.Message, cancellationToken);

            default:
                return SettleAsync(request, FulfillmentRequestStatus.NeedsAttention, result.Message, cancellationToken);
        }
    }

    private async Task<FulfillmentRequest> SettleAsync(FulfillmentRequest request, FulfillmentRequestStatus status, string message, CancellationToken cancellationToken)
    {
        request.Status = status;
        request.Message = message;
        request.ModifiedUtc = _clock.UtcNow;

        await _store.UpdateAsync(request, cancellationToken);
        await _store.CommitAsync(cancellationToken);

        return request;
    }

    private static string LockKey(FulfillmentRequest request)
        => $"fulfillment-send-{request.OrderId}-{request.ProviderName}";

    internal static TimeSpan RetryWait(int attempts)
        => TimeSpan.FromMinutes(Math.Min(60, Math.Pow(2, Math.Max(0, attempts - 1))));
}
