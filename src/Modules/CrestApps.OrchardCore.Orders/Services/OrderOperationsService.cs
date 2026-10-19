using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// What the store does to an order from its page: refund it, cancel it, resend its confirmation, and write notes.
/// </summary>
/// <remarks>
/// An order is paid one of two ways. Paid online at checkout, its money is the checkout's confirmed payments, refunded
/// through the checkout refund service. Paid later, its money is a transaction for the order, refunded or canceled
/// through the transaction commands. Either way the refund comes back through <see cref="OrderRefundRecorder"/>, which
/// records it on the order with who gave it.
/// </remarks>
public sealed class OrderOperationsService
{
    private const string StoreCreditRefundPrefix = "store-credit:";

    private readonly IOrderStore _orderStore;
    private readonly IOrderService _orderService;
    private readonly OrderOperationScope _scope;
    private readonly OrderConfirmationSender _confirmationSender;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public OrderOperationsService(
        IOrderStore orderStore,
        IOrderService orderService,
        OrderOperationScope scope,
        OrderConfirmationSender confirmationSender,
        IServiceProvider serviceProvider,
        ILogger<OrderOperationsService> logger,
        IStringLocalizer<OrderOperationsService> stringLocalizer)
    {
        _orderStore = orderStore;
        _orderService = orderService;
        _scope = scope;
        _confirmationSender = confirmationSender;
        _serviceProvider = serviceProvider;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Returns whether money is still owed on a placed order, such as one paid by bank transfer before the money
    /// arrives.
    /// </summary>
    public async Task<bool> IsOwedAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        // Only a placed order can be owed: one paid offline, such as by bank transfer, before the money arrives.
        return order.Status == OrderStatus.Paid && (await GetTransactionsAsync(order, cancellationToken)).Any(IsOutstanding);
    }

    /// <summary>
    /// Returns what can still be refunded for the order, and what is still owed for it, such as through Pay Later
    /// or a bank transfer.
    /// </summary>
    public async Task<OrderMoney> GetMoneyAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var money = new OrderMoney { Currency = order.Currency };

        foreach (var (_, remaining) in await GetPaymentsAsync(order, cancellationToken))
        {
            money.Refundable += remaining;
        }

        // What a gift card or store credit paid can be put back on it.
        if (!string.IsNullOrEmpty(order.CheckoutSessionId) && _serviceProvider.GetService<ICheckoutTenderLedger>() is { } ledger)
        {
            money.Refundable += (await ledger.GetRedeemedAsync(order.CheckoutSessionId, cancellationToken)).Sum(hold => hold.Amount - hold.ReturnedAmount);
        }

        foreach (var transaction in await GetTransactionsAsync(order, cancellationToken))
        {
            money.Refundable += Math.Max(0m, transaction.AmountPaid - transaction.AmountRefunded);

            if (IsOutstanding(transaction))
            {
                money.Outstanding += transaction.OutstandingAmount;
            }
        }

        // What was given back as store credit came out of what's refundable, though no payment was refunded.
        money.Refundable -= (order.Refunds ?? []).Where(refund => refund.RefundId?.StartsWith(StoreCreditRefundPrefix, StringComparison.Ordinal) == true).Sum(refund => refund.Amount);
        money.Refundable = Money.Round(Math.Max(0m, money.Refundable), order.Currency);
        money.CanRefundAsStoreCredit = money.Refundable > 0m &&
            _serviceProvider.GetService<IOrderStoreCreditRefunder>() is { } refunder &&
            await refunder.CanRefundAsync(order, cancellationToken);
        money.Outstanding = Money.Round(money.Outstanding, order.Currency);

        return money;
    }

    /// <summary>
    /// Gives back <paramref name="amount"/> of what was paid for the order, or everything still refundable, newest
    /// payment first. With <paramref name="restock"/>, what the order sold goes back into stock once everything is
    /// refunded.
    /// </summary>
    public Task<OrderOperationResult> RefundAsync(Order order, decimal? amount, string reason, bool restock, string actorName, CancellationToken cancellationToken = default)
        => RefundAsync(order, amount, reason, restock, actorName, asStoreCredit: false, cancellationToken);

    /// <summary>
    /// Gives back <paramref name="amount"/> of what was paid for the order, or everything still refundable: newest
    /// payment first, or, with <paramref name="asStoreCredit"/>, as store credit for the buyer to spend later.
    /// </summary>
    public async Task<OrderOperationResult> RefundAsync(Order order, decimal? amount, string reason, bool restock, string actorName, bool asStoreCredit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var result = new OrderOperationResult();
        var money = await GetMoneyAsync(order, cancellationToken);

        if (money.Refundable <= 0m)
        {
            result.Errors.Add(S["Nothing paid for this order can be refunded."]);

            return result;
        }

        if (amount is <= 0m)
        {
            result.Errors.Add(S["Enter an amount greater than zero."]);

            return result;
        }

        var left = Money.Round(amount ?? money.Refundable, order.Currency);

        if (left > money.Refundable)
        {
            result.Errors.Add(S["Only {0} can still be refunded for this order.", ProductPriceFormatter.Format(money.Refundable, order.Currency)]);

            return result;
        }

        _scope.ActorName = actorName;
        _scope.Restock = restock;
        reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        if (asStoreCredit)
        {
            if (_serviceProvider.GetService<IOrderStoreCreditRefunder>() is not { } refunder || !await refunder.RefundAsync(order, left, reason, actorName, cancellationToken))
            {
                result.Errors.Add(S["The refund couldn't be given as store credit. The buyer needs an account, and their credit may be in use at a checkout right now."]);

                return result;
            }

            // Recorded as one of the order's refunds, so what was refunded adds up to its total.
            await _orderService.RecordRefundAsync(order, new OrderRefund
            {
                RefundId = StoreCreditRefundPrefix + global::OrchardCore.IdGenerator.GenerateId(),
                Amount = left,
                Currency = order.Currency,
                Reason = reason,
                CreatedUtc = _serviceProvider.GetRequiredService<global::OrchardCore.Modules.IClock>().UtcNow,
            }, restock, cancellationToken);

            await _orderService.AddNoteAsync(
                order,
                reason is null
                    ? S["Gave {0} back as store credit.", ProductPriceFormatter.Format(left, order.Currency)]
                    : S["Gave {0} back as store credit: {1}", ProductPriceFormatter.Format(left, order.Currency), reason],
                OrderNoteKind.Event,
                actorName,
                cancellationToken);

            result.Refunded = left;

            return result;
        }

        left = await RefundPaymentsAsync(order, left, reason, result, cancellationToken);
        left = await RefundTransactionsAsync(order, left, reason, result, cancellationToken);

        // The card is refunded first, up to what it paid; what a gift card or store credit paid goes back on it last.
        left = await RefundTendersAsync(order, left, reason, cancellationToken);

        result.Refunded = Money.Round((amount ?? money.Refundable) - left, order.Currency);

        return result;
    }

    /// <summary>
    /// Cancels the order. A paid one can have everything refunded with it, and what it sold put back into stock;
    /// what is still owed through Pay Later is canceled, and an unpaid checkout is let go.
    /// </summary>
    public async Task<OrderOperationResult> CancelAsync(Order order, string note, bool refund, bool restock, string actorName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var result = new OrderOperationResult();

        if (order.Status == OrderStatus.Canceled)
        {
            result.Errors.Add(S["This order is canceled already."]);

            return result;
        }

        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        _scope.ActorName = actorName;

        // Money is given back before the order is canceled, so a refund that fails leaves the order as it was.
        if (refund && (await GetMoneyAsync(order, cancellationToken)).Refundable > 0m)
        {
            var refunded = await RefundAsync(order, amount: null, note ?? S["The order was canceled."].Value, restock: false, actorName, cancellationToken);

            result.Refunded = refunded.Refunded;
            result.Messages.AddRange(refunded.Messages);

            if (!refunded.Succeeded)
            {
                result.Errors.AddRange(refunded.Errors);

                return result;
            }
        }

        // What Pay Later still has to collect is canceled, so the customer isn't reminded to pay for a canceled order.
        if (_serviceProvider.GetService<ITransactionCommandService>() is { } commands)
        {
            foreach (var transaction in (await GetTransactionsAsync(order, cancellationToken)).Where(IsOutstanding))
            {
                var canceled = await commands.CancelAsync(transaction.ItemId, note ?? S["The order was canceled."].Value, cancellationToken);

                if (!canceled.Succeeded)
                {
                    result.Errors.AddRange(canceled.Errors);

                    return result;
                }
            }
        }

        order = await _orderStore.FindByIdAsync(order.ItemId, cancellationToken) ?? order;

        var wasPaid = order.Status == OrderStatus.Paid;
        var sessionId = order.CheckoutSessionId;

        order.CancelNote = note;

        if (!await _orderService.CancelAsync(order, OrderCancelReason.CanceledByStore, restock && wasPaid, cancellationToken))
        {
            result.Errors.Add(S["This order can't be canceled."]);

            return result;
        }

        // An unpaid order's checkout is let go too, so what it holds, such as a promotion code's use, goes back. The
        // order is canceled first, so the checkout's own cancellation finds nothing left to do to it.
        if (!wasPaid && !string.IsNullOrEmpty(sessionId) && _serviceProvider.GetService<ICheckoutEngine>() is { } engine)
        {
            await engine.CancelAsync(sessionId, S["The store canceled the order."], cancellationToken);
        }

        await _orderService.AddNoteAsync(
            order,
            note is null ? S["Canceled the order."] : S["Canceled the order: {0}", note],
            OrderNoteKind.Event,
            actorName,
            cancellationToken);

        return result;
    }

    /// <summary>
    /// Emails the order's confirmation to the customer again.
    /// </summary>
    public async Task<bool> ResendConfirmationAsync(Order order, string actorName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (!await _confirmationSender.SendAsync(order, cancellationToken))
        {
            return false;
        }

        await _orderService.AddNoteAsync(order, S["Sent the confirmation again to {0}.", order.CustomerEmail], OrderNoteKind.Event, actorName, cancellationToken);

        return true;
    }

    private async Task<decimal> RefundPaymentsAsync(Order order, decimal left, string reason, OrderOperationResult result, CancellationToken cancellationToken)
    {
        if (left <= 0m || _serviceProvider.GetService<ICheckoutRefundService>() is not { } refundService)
        {
            return left;
        }

        var refundStore = _serviceProvider.GetRequiredService<IPaymentRefundStore>();

        foreach (var (attempt, remaining) in await GetPaymentsAsync(order, cancellationToken))
        {
            if (left <= 0m)
            {
                break;
            }

            var refunds = await refundStore.GetByOriginalTransactionAsync(attempt.TransactionId, cancellationToken);

            if (refunds.Any(existing => existing.Status == RefundStatus.PendingManualReview))
            {
                result.Errors.Add(S["A refund of this order's payment is waiting to be settled by hand. Mark it settled on the Refunds page before starting another."]);

                continue;
            }

            var portion = Math.Min(left, remaining);
            PaymentRefund refund;

            try
            {
                refund = await refundService.RequestRefundAsync(new RequestPaymentRefundContext
                {
                    SessionId = attempt.SessionId,
                    OriginalTransactionId = attempt.TransactionId,
                    Amount = portion,
                    Reason = reason,
                }, cancellationToken);
            }
            catch (InvalidOperationException exception)
            {
                _logger.LogWarning(exception, "The refund of order '{OrderNumber}' was refused.", order.OrderNumber);

                result.Errors.Add(S["The refund couldn't be started. Another refund for this payment may still be in progress. Try again in a moment."]);

                continue;
            }

            switch (refund.Status)
            {
                case RefundStatus.Failed:
                case RefundStatus.Canceled:
                    result.Errors.Add(S["The refund of {0} failed: {1}", ProductPriceFormatter.Format(portion, order.Currency), refund.FailureReason ?? S["The provider did not say why."].Value]);
                    continue;

                case RefundStatus.PendingManualReview:
                    result.Messages.Add(S["This payment method can't refund automatically. The refund of {0} was recorded for you to settle by hand.", ProductPriceFormatter.Format(portion, order.Currency)]);
                    break;

                case RefundStatus.Succeeded:
                    break;

                default:
                    result.Messages.Add(S["The refund of {0} was submitted and is still being processed.", ProductPriceFormatter.Format(portion, order.Currency)]);
                    break;
            }

            left = Money.Round(left - portion, order.Currency);
        }

        return left;
    }

    private async Task<decimal> RefundTendersAsync(Order order, decimal left, string reason, CancellationToken cancellationToken)
    {
        if (left <= 0m || string.IsNullOrEmpty(order.CheckoutSessionId) || _serviceProvider.GetService<ICheckoutTenderLedger>() is not { } ledger)
        {
            return left;
        }

        var balances = string.Join(", ", (await ledger.GetRedeemedAsync(order.CheckoutSessionId, cancellationToken)).Select(hold => hold.Description).Distinct());
        var returned = await ledger.ReturnAsync(order.CheckoutSessionId, left, reason ?? S["A refund of order {0}.", order.OrderNumber].Value, cancellationToken);

        if (returned <= 0m)
        {
            return left;
        }

        // Recorded as one of the order's refunds, like a card refund, so what was refunded adds up to its total.
        await _orderService.RecordRefundAsync(order, new OrderRefund
        {
            RefundId = "tender:" + global::OrchardCore.IdGenerator.GenerateId(),
            Amount = returned,
            Currency = order.Currency,
            Reason = reason,
            CreatedUtc = _serviceProvider.GetRequiredService<global::OrchardCore.Modules.IClock>().UtcNow,
        }, _scope.Restock, cancellationToken);

        await _orderService.AddNoteAsync(
            order,
            S["Put {0} back on {1}.", ProductPriceFormatter.Format(returned, order.Currency), balances],
            OrderNoteKind.Event,
            _scope.ActorName,
            cancellationToken);

        return Money.Round(left - returned, order.Currency);
    }

    private async Task<decimal> RefundTransactionsAsync(Order order, decimal left, string reason, OrderOperationResult result, CancellationToken cancellationToken)
    {
        if (left <= 0m || _serviceProvider.GetService<ITransactionCommandService>() is not { } commands)
        {
            return left;
        }

        foreach (var transaction in await GetTransactionsAsync(order, cancellationToken))
        {
            var remaining = Math.Max(0m, transaction.AmountPaid - transaction.AmountRefunded);

            if (left <= 0m || remaining <= 0m)
            {
                continue;
            }

            var portion = Math.Min(left, remaining);
            var refunded = await commands.RefundAsync(transaction.ItemId, portion, reason, cancellationToken);

            if (!refunded.Succeeded)
            {
                result.Errors.AddRange(refunded.Errors);

                continue;
            }

            left = Money.Round(left - portion, order.Currency);
        }

        return left;
    }

    // The checkout's confirmed payments that moved money, newest first, with what each can still give back. A payment
    // promised for later (Pay Later) moved nothing; its money is on the order's transaction.
    private async Task<List<(PaymentAttempt Attempt, decimal Remaining)>> GetPaymentsAsync(Order order, CancellationToken cancellationToken)
    {
        var payments = new List<(PaymentAttempt, decimal)>();

        if (string.IsNullOrEmpty(order.CheckoutSessionId) ||
            _serviceProvider.GetService<IPaymentAttemptStore>() is not { } attemptStore ||
            _serviceProvider.GetService<IPaymentRefundStore>() is not { } refundStore)
        {
            return payments;
        }

        var providers = _serviceProvider.GetService<ICheckoutPaymentProviderResolver>();

        foreach (var attempt in (await attemptStore.GetBySessionAsync(order.CheckoutSessionId, cancellationToken)).OrderByDescending(attempt => attempt.CreatedUtc))
        {
            if (attempt.State != PaymentAttemptState.Succeeded ||
                string.IsNullOrEmpty(attempt.TransactionId) ||
                providers?.GetProvider(attempt.ProviderKey)?.Capabilities?.DefersPayment == true)
            {
                continue;
            }

            var refunded = (await refundStore.GetByOriginalTransactionAsync(attempt.TransactionId, cancellationToken))
                .Where(refund => refund.Status is not (RefundStatus.Failed or RefundStatus.Canceled))
                .Sum(refund => refund.RefundGrossAmount);

            var remaining = Money.Round(attempt.ConfirmedAmount + attempt.ConfirmedTaxAmount - refunded, attempt.Currency);

            if (remaining > 0m)
            {
                payments.Add((attempt, remaining));
            }
        }

        return payments;
    }

    private async Task<IReadOnlyList<Transaction>> GetTransactionsAsync(Order order, CancellationToken cancellationToken)
    {
        if (_serviceProvider.GetService<ITransactionStore>() is not { } transactions)
        {
            return [];
        }

        var page = await transactions.PageAsync(1, 50, new TransactionQuery
        {
            ReferenceType = CheckoutReferenceTypes.Order,
            ReferenceId = order.ItemId,
        }, cancellationToken);

        return [.. page.Entries];
    }

    private static bool IsOutstanding(Transaction transaction)
        => transaction.OutstandingAmount > 0m && transaction.Status is TransactionStatus.Pending or TransactionStatus.Outstanding or TransactionStatus.PartiallyPaid;
}

/// <summary>
/// What can still be refunded for an order, and what is still owed for it.
/// </summary>
public sealed class OrderMoney
{
    /// <summary>
    /// Gets or sets whether the refund can be given to the buyer as store credit.
    /// </summary>
    public bool CanRefundAsStoreCredit { get; set; }

    public string Currency { get; set; }

    public decimal Refundable { get; set; }

    public decimal Outstanding { get; set; }
}

/// <summary>
/// The outcome of an operation on an order.
/// </summary>
public sealed class OrderOperationResult
{
    public bool Succeeded
        => Errors.Count == 0;

    public decimal Refunded { get; set; }

    public List<string> Errors { get; } = [];

    public List<string> Messages { get; } = [];
}
