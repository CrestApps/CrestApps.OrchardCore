using System.Globalization;
using CrestApps.OrchardCore.Fulfillment.Core.Models;
using CrestApps.OrchardCore.Fulfillment.Models;
using CrestApps.OrchardCore.Fulfillment.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Fulfillment.Core.Services;

/// <summary>
/// Records paid orders for the fulfillment partners and sends them, once. The lines going to a partner are reserved in
/// a pending Shipping shipment, saved together with the request's <see cref="FulfillmentRequestStatus.Sending"/> status
/// before the partner is called; any outcome that is not clear goes to a person, and the shipment stays reserved.
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

    /// <summary>
    /// How long the lock of an order is held at most: longer than <see cref="FulfillmentConstants.SubmitTimeout"/>.
    /// </summary>
    internal static readonly TimeSpan LockExpiration = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan _lockWait = TimeSpan.FromSeconds(10);

    private readonly FulfillmentRequestStore _store;
    private readonly IOrderStore _orders;
    private readonly IEnumerable<IFulfillmentProvider> _providers;
    private readonly FulfillmentOrderBuilder _builder;
    private readonly FulfillmentLineAssigner _assigner;
    private readonly IShipmentService _shipments;
    private readonly IShipmentStore _shipmentStore;
    private readonly IEnumerable<IFulfillmentRequestHandler> _handlers;
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
    /// <param name="builder">Checks orders and works out what is left to ship.</param>
    /// <param name="assigner">Assigns lines to partners.</param>
    /// <param name="shipments">The shipment service, which reserves the lines a partner makes.</param>
    /// <param name="shipmentStore">The shipment store, to read and note a request's shipment.</param>
    /// <param name="handlers">The handlers told when a request needs a person.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="distributedLock">The distributed lock, so one sender at a time handles an order.</param>
    /// <param name="logger">The logger.</param>
    public FulfillmentSender(
        FulfillmentRequestStore store,
        IOrderStore orders,
        IEnumerable<IFulfillmentProvider> providers,
        FulfillmentOrderBuilder builder,
        FulfillmentLineAssigner assigner,
        IShipmentService shipments,
        IShipmentStore shipmentStore,
        IEnumerable<IFulfillmentRequestHandler> handlers,
        IClock clock,
        IDistributedLock distributedLock,
        ILogger<FulfillmentSender> logger)
    {
        _store = store;
        _orders = orders;
        _providers = providers;
        _builder = builder;
        _assigner = assigner;
        _shipments = shipments;
        _shipmentStore = shipmentStore;
        _handlers = handlers;
        _clock = clock;
        _distributedLock = distributedLock;
        _logger = logger;
    }

    /// <summary>
    /// Gets or sets how long a partner may take to answer; shorter in tests.
    /// </summary>
    internal TimeSpan SubmitTimeout { get; set; } = FulfillmentConstants.SubmitTimeout;

    /// <summary>
    /// Records a pending request for each connected partner that has none for the order yet. It reads saved settings
    /// only and never calls a partner, so it is safe while an order is being paid.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <param name="lockAndCommit">
    /// Whether to hold the order's lock and save the new requests before returning, so two people sending the same order
    /// at once reuse one request. The paid-order handler passes <see langword="false"/>: nobody else sees the order as
    /// paid before the payment is saved.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>All the order's requests.</returns>
    public async Task<IReadOnlyList<FulfillmentRequest>> RecordAsync(Order order, bool lockAndCommit = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (lockAndCommit)
        {
            return await TryRecordForSendAsync(order, cancellationToken)
                ?? await _store.ListByOrderAsync(order.ItemId, cancellationToken);
        }

        return await RecordCoreAsync(order, cancellationToken);
    }

    /// <summary>
    /// Takes the order's lock and records requests for a manual send.
    /// </summary>
    /// <param name="order">The order to send.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The order's requests, or <see langword="null"/> when another worker holds its lock.</returns>
    public async Task<IReadOnlyList<FulfillmentRequest>> TryRecordForSendAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockKey(order.ItemId), _lockWait, LockExpiration);

        if (!locked)
        {
            return null;
        }

        await using (locker)
        {
            var requests = await RecordCoreAsync(order, cancellationToken);

            await _store.CommitAsync(cancellationToken);

            return requests;
        }
    }

    /// <summary>
    /// Sends one request now: assigns its lines, reserves them in a pending shipment, then calls the partner. A request
    /// already sent, or being sent, is left as it is.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests, until the partner is called.</param>
    /// <returns>The request as stored after sending, or that another worker had it.</returns>
    public async Task<FulfillmentSendResult> SendAsync(FulfillmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_sessionUnusable)
        {
            return new FulfillmentSendResult(request, TakenByAnotherWorker: true);
        }

        // One sender at a time per order: line assignment, the reserving shipment and the partner call never overlap.
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockKey(request.OrderId), _lockWait, LockExpiration);

        if (!locked)
        {
            return new FulfillmentSendResult(request, TakenByAnotherWorker: true);
        }

        await using (locker)
        {
            // Decide on the stored state: another worker may have sent or settled it since it was loaded.
            var current = await _store.FindCommittedAsync(request, cancellationToken);

            if (current is null || current.Status is FulfillmentRequestStatus.Sent or FulfillmentRequestStatus.Sending)
            {
                return new FulfillmentSendResult(current ?? request);
            }

            return await SendLockedAsync(current, cancellationToken);
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
    /// site stops while a partner is being called. The reserving shipment is kept.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many requests were flagged.</returns>
    public async Task<int> FlagStuckAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = _clock.UtcNow - StuckAfter;
        var stuck = await _store.ListStuckAsync(cutoff, 50, cancellationToken);
        var flagged = 0;

        foreach (var request in stuck)
        {
            if (_sessionUnusable)
            {
                break;
            }

            // A sender holding the order's lock is still working on it.
            var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockKey(request.OrderId), TimeSpan.Zero, LockExpiration);

            if (!locked)
            {
                continue;
            }

            await using (locker)
            {
                var current = await _store.FindCommittedAsync(request, cancellationToken);

                if (current is null || current.Status != FulfillmentRequestStatus.Sending || current.ModifiedUtc >= cutoff)
                {
                    continue;
                }

                if (await SettleAsync(current, FulfillmentRequestStatus.NeedsAttention, FulfillmentReasons.Interrupted, null, [], CancellationToken.None) is not null)
                {
                    flagged++;
                }
            }
        }

        return flagged;
    }

    private async Task<IReadOnlyList<FulfillmentRequest>> RecordCoreAsync(Order order, CancellationToken cancellationToken)
    {
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
                Reason = FulfillmentReasons.Waiting,
                NextAttemptUtc = now,
                CreatedUtc = now,
                ModifiedUtc = now,
            };

            await _store.CreateAsync(request, cancellationToken);
            requests.Add(request);
        }

        return requests;
    }

    private async Task<FulfillmentSendResult> SendLockedAsync(FulfillmentRequest request, CancellationToken cancellationToken)
    {
        var provider = _providers.FirstOrDefault(item => string.Equals(item.Name, request.ProviderName, StringComparison.Ordinal));

        if (provider is null)
        {
            return await SettledAsync(request, FulfillmentRequestStatus.NeedsAttention, FulfillmentReasons.ProviderDisabled, null, [request.ProviderName], cancellationToken);
        }

        var siblings = await _store.ListByOrderAsync(request.OrderId, cancellationToken);

        if (siblings.Any(item => item.ItemId != request.ItemId
            && string.Equals(item.ProviderName, request.ProviderName, StringComparison.Ordinal)
            && item.Status is FulfillmentRequestStatus.Sent or FulfillmentRequestStatus.Sending))
        {
            return await SettledAsync(request, FulfillmentRequestStatus.NothingToSend, FulfillmentReasons.AlreadySent, null, [], cancellationToken);
        }

        var order = await _orders.FindByIdAsync(request.OrderId, cancellationToken);
        var blocked = FulfillmentOrderBuilder.CheckOrder(order);

        var shipment = await LoadReservedShipmentAsync(request, cancellationToken);

        if (blocked is not null)
        {
            if (shipment?.Status == ShipmentStatus.Pending && request.Reason == FulfillmentReasons.RetryLater)
            {
                // The partner asked to wait, so it certainly does not have the order: free the units it held.
                await ReleaseAsync(request, shipment);
            }

            return await SettledAsync(request, blocked.NothingToSend ? FulfillmentRequestStatus.NothingToSend : FulfillmentRequestStatus.NeedsAttention, blocked.Reason, null, blocked.Arguments, cancellationToken);
        }

        if (shipment?.Status is ShipmentStatus.Shipped or ShipmentStatus.Delivered)
        {
            // Someone confirmed with the partner and shipped it by hand: nothing to send.
            return await SettledAsync(request, FulfillmentRequestStatus.NothingToSend, FulfillmentReasons.ShippedByHand, null, [], cancellationToken);
        }

        if (shipment is null)
        {
            var reserved = await ReserveAsync(request, provider, order, cancellationToken);

            if (reserved.Settled is not null)
            {
                return reserved.Settled;
            }

            shipment = reserved.Shipment;
        }

        // The shipment and the Sending status are saved in one commit, before the partner is called: if the site stops
        // now, the request stays in Sending, the units stay reserved and a person is asked.
        request.ShipmentId = shipment.ItemId;
        request.Lines = [.. shipment.Lines.Select(line => new FulfillmentRequestLine { LineId = line.LineId, Title = line.Title, Sku = line.Sku, Quantity = line.Quantity })];
        request.Status = FulfillmentRequestStatus.Sending;
        request.Reason = null;
        request.ReasonArguments = [];
        request.Message = null;
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

            return new FulfillmentSendResult(request, TakenByAnotherWorker: true);
        }

        var lines = shipment.Lines.ToDictionary(line => line.LineId, line => line.Quantity, StringComparer.Ordinal);
        var result = await SubmitAsync(provider, FulfillmentOrderBuilder.ForLines(order, lines), request);

        return new FulfillmentSendResult(await RecordResultAsync(request, shipment, provider, result) ?? request);
    }

    private async Task<Shipment> LoadReservedShipmentAsync(FulfillmentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.ShipmentId))
        {
            return null;
        }

        var shipment = await _shipmentStore.FindByIdAsync(request.ShipmentId, cancellationToken);

        // A shipment deleted or returned since no longer reserves the units: assign the lines again.
        return shipment?.Status is ShipmentStatus.Pending or ShipmentStatus.Shipped or ShipmentStatus.Delivered
            ? shipment
            : null;
    }

    private async Task<(Shipment Shipment, FulfillmentSendResult Settled)> ReserveAsync(FulfillmentRequest request, IFulfillmentProvider provider, Order order, CancellationToken cancellationToken)
    {
        request.ShipmentId = null;

        var check = await _builder.CheckAsync(order, cancellationToken);

        if (!check.CanSend)
        {
            return (null, await SettledAsync(request, check.NothingToSend ? FulfillmentRequestStatus.NothingToSend : FulfillmentRequestStatus.NeedsAttention, check.Reason, null, check.Arguments, cancellationToken));
        }

        FulfillmentAssignment assignment;

        try
        {
            assignment = await _assigner.AssignAsync(provider, check.Description, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Nothing is reserved or sent yet, so a person can simply send it again.
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(exception, "{Provider} could not say which lines of order '{OrderNumber}' it makes.", request.ProviderName, request.OrderNumber);
            }

            return (null, await SettledAsync(request, FulfillmentRequestStatus.NeedsAttention, FulfillmentReasons.ClaimFailed, null, [], cancellationToken));
        }

        if (assignment.HasConflict)
        {
            return (null, await SettledAsync(request, FulfillmentRequestStatus.NeedsAttention, FulfillmentReasons.ConflictingClaims, null, [assignment.ConflictingLine, string.Join(", ", assignment.ConflictingPartners)], cancellationToken));
        }

        if (assignment.Lines.Count == 0)
        {
            return (null, await SettledAsync(request, FulfillmentRequestStatus.NothingToSend, FulfillmentReasons.NotClaimed, null, [], cancellationToken));
        }

        var created = await _shipments.CreateAsync(
            order,
            assignment.Lines.Select(line => new ShipmentLineRequest(line.Key, line.Value)),
            new ShipmentDetails(Carrier: null, TrackingNumber: null, Note: $"Sent to {provider.DisplayName.Value}"),
            ship: false,
            cancellationToken);

        if (!created.Succeeded)
        {
            return (null, created.Error switch
            {
                ShipmentError.NothingToShip => await SettledAsync(request, FulfillmentRequestStatus.NothingToSend, FulfillmentReasons.NothingLeft, null, [], cancellationToken),
                ShipmentError.OrderNotPaid => await SettledAsync(request, FulfillmentRequestStatus.NeedsAttention, FulfillmentReasons.NotPaid, null, [order.Status.ToString()], cancellationToken),
                _ => await SettledAsync(request, FulfillmentRequestStatus.NeedsAttention, FulfillmentReasons.TooMany, null, [], cancellationToken),
            });
        }

        return (created.Shipment, null);
    }

    private async Task<FulfillmentSubmitResult> SubmitAsync(IFulfillmentProvider provider, FulfillmentOrder order, FulfillmentRequest request)
    {
        // Not the caller's token: once the partner may have the order, the outcome must be recorded. A bounded wait
        // instead, which ends well before the order's lock does.
        using var timeout = new CancellationTokenSource(SubmitTimeout);

        try
        {
            return await provider.SubmitAsync(order, timeout.Token);
        }
        catch (Exception exception)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(exception, "Sending order '{OrderNumber}' to {Provider} failed.", request.OrderNumber, request.ProviderName);
            }

            return FulfillmentSubmitResult.NeedsAttention(null);
        }
    }

    private async Task<FulfillmentRequest> RecordResultAsync(FulfillmentRequest request, Shipment shipment, IFulfillmentProvider provider, FulfillmentSubmitResult result)
    {
        var token = CancellationToken.None;

        switch (result.Status)
        {
            case FulfillmentSubmitStatus.Sent:
                request.ExternalOrderId = result.ExternalOrderId;
                request.SentUtc = _clock.UtcNow;
                shipment.Note = string.IsNullOrEmpty(result.ExternalOrderId)
                    ? $"Sent to {provider.DisplayName.Value}"
                    : $"Sent to {provider.DisplayName.Value}, order {result.ExternalOrderId}";
                shipment.ModifiedUtc = _clock.UtcNow;
                await _shipmentStore.UpdateAsync(shipment, token);

                return await SettleAsync(request, FulfillmentRequestStatus.Sent, FulfillmentReasons.Sent, result.Message, [result.ExternalOrderId ?? string.Empty], token);

            case FulfillmentSubmitStatus.NothingToSend:
                await ReleaseAsync(request, shipment);

                return await SettleAsync(request, FulfillmentRequestStatus.NothingToSend, FulfillmentReasons.PartnerNothingToSend, result.Message, [], token);

            case FulfillmentSubmitStatus.RetryLater when request.Attempts < MaxAttempts:
                // The shipment stays reserved and is reused on the next attempt.
                request.NextAttemptUtc = _clock.UtcNow + RetryWait(request.Attempts);

                return await SettleAsync(request, FulfillmentRequestStatus.Pending, FulfillmentReasons.RetryLater, result.Message, [], token);

            case FulfillmentSubmitStatus.RetryLater:
                // The partner certainly did not create the order: release the units so they can be shipped another way.
                await ReleaseAsync(request, shipment);

                return await SettleAsync(request, FulfillmentRequestStatus.NeedsAttention, FulfillmentReasons.GaveUp, result.Message, [request.Attempts.ToString(CultureInfo.InvariantCulture)], token);

            case FulfillmentSubmitStatus.Refused:
                await ReleaseAsync(request, shipment);

                return await SettleAsync(request, FulfillmentRequestStatus.NeedsAttention, FulfillmentReasons.Refused, result.Message, [], token);

            default:
                // Unclear: the units stay reserved, so nobody ships them by hand until a person decides.
                return await SettleAsync(request, FulfillmentRequestStatus.NeedsAttention, FulfillmentReasons.Unclear, result.Message, [], token);
        }
    }

    private async Task ReleaseAsync(FulfillmentRequest request, Shipment shipment)
    {
        var deleted = await _shipments.DeleteAsync(shipment, CancellationToken.None);

        if (deleted.Succeeded)
        {
            request.ShipmentId = null;
        }
        else if (_logger.IsEnabled(LogLevel.Warning))
        {
            // Shipped by hand meanwhile: it stays, and so does the link to it.
            _logger.LogWarning("The shipment '{ShipmentId}' of order '{OrderNumber}' could not be released: {Error}.", shipment.ItemId, request.OrderNumber, deleted.Error);
        }
    }

    private async Task<FulfillmentSendResult> SettledAsync(FulfillmentRequest request, FulfillmentRequestStatus status, string reason, string message, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        => new(await SettleAsync(request, status, reason, message, arguments, cancellationToken) ?? request, TakenByAnotherWorker: _sessionUnusable);

    private async Task<FulfillmentRequest> SettleAsync(FulfillmentRequest request, FulfillmentRequestStatus status, string reason, string message, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var previous = request.Status;

        request.Status = status;
        request.Reason = reason;
        request.ReasonArguments = [.. arguments ?? []];
        request.Message = message;
        request.ModifiedUtc = _clock.UtcNow;

        try
        {
            await _store.UpdateAsync(request, cancellationToken);
            await _store.CommitAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            _sessionUnusable = true;

            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("The fulfillment request '{RequestId}' of order '{OrderNumber}' was changed by another worker; its outcome {Status} ({ExternalOrderId}) was not saved.", request.ItemId, request.OrderNumber, status, request.ExternalOrderId);
            }

            return null;
        }

        if (status == FulfillmentRequestStatus.NeedsAttention && previous != FulfillmentRequestStatus.NeedsAttention)
        {
            await NotifyAsync(request);
        }

        return request;
    }

    private async Task NotifyAsync(FulfillmentRequest request)
    {
        foreach (var handler in _handlers)
        {
            try
            {
                await handler.NeedsAttentionAsync(request, CancellationToken.None);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(exception, "Telling people that fulfillment request '{RequestId}' needs attention failed.", request.ItemId);
                }
            }
        }

        try
        {
            // Notices are saved with this session: save them now, so a later conflict in the same batch can't drop them.
            await _store.CommitAsync(CancellationToken.None);
        }
        catch (ConcurrencyException)
        {
            _sessionUnusable = true;
        }
    }

    private static string LockKey(string orderId)
        => $"fulfillment-order-{orderId}";

    internal static TimeSpan RetryWait(int attempts)
        => TimeSpan.FromMinutes(Math.Min(60, Math.Pow(2, Math.Max(0, attempts - 1))));
}
