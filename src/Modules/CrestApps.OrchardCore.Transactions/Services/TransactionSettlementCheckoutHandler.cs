using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Bridges the checkout framework and the transaction ledger so an outstanding transaction can be settled
/// online. When a checkout session references a transaction, the handler contributes the outstanding
/// balance as a one-time billing item and, once the checkout completes, settles the transaction against
/// the amount the payment provider actually confirmed rather than the amount the checkout requested.
/// A promotion code used on the checkout is recorded on the transaction as a discount adjustment of exactly
/// what the invoice took off, and the code is redeemed only once the transaction is settled.
/// A checkout can also settle several transactions of one customer, in one currency, with one payment
/// (<see cref="CheckoutSession.References"/>): each is a line of its own, no promotion code is offered, and the confirmed
/// payment is split across them, each recording its share as a payment of its own.
/// </summary>
public sealed class TransactionSettlementCheckoutHandler : CheckoutHandlerBase
{
    private const string StepKey = TransactionsConstants.SettlementStepKey;

    private readonly ITransactionManager _transactionManager;
    private readonly IPaymentAttemptStore _paymentAttemptStore;
    private readonly ICheckoutPaymentProviderResolver _providerResolver;
    private readonly ITransactionSettlementService _settlementService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionSettlementCheckoutHandler"/> class.
    /// </summary>
    /// <param name="transactionManager">The transaction manager.</param>
    /// <param name="paymentAttemptStore">The durable payment-attempt ledger used to read confirmed amounts.</param>
    /// <param name="settlementService">The service that applies confirmed attempts to the transaction.</param>
    /// <param name="providerResolver">Resolves the provider of each attempt, so a promise to pay later is never counted as money.</param>
    /// <param name="serviceProvider">The service provider the transaction commands and the discount providers are resolved
    /// from when a promotion code was used. They are resolved then rather than injected, because they depend on services
    /// the checkout itself is built from.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionSettlementCheckoutHandler(
        ITransactionManager transactionManager,
        IPaymentAttemptStore paymentAttemptStore,
        ITransactionSettlementService settlementService,
        ICheckoutPaymentProviderResolver providerResolver,
        IServiceProvider serviceProvider,
        ILogger<TransactionSettlementCheckoutHandler> logger,
        IStringLocalizer<TransactionSettlementCheckoutHandler> stringLocalizer)
    {
        _transactionManager = transactionManager;
        _paymentAttemptStore = paymentAttemptStore;
        _settlementService = settlementService;
        _providerResolver = providerResolver;
        _serviceProvider = serviceProvider;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializingAsync(CheckoutFlowInitializingContext context)
    {
        // Concealment is decided per request rather than stored, so the balance step is hidden again every time
        // the session is loaded.
        foreach (var step in context.Flow.Session.Steps)
        {
            if (string.Equals(step.Key, StepKey, StringComparison.Ordinal))
            {
                step.Conceal = true;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task ActivatingAsync(CheckoutFlowActivatingContext context)
    {
        var session = context.Session;

        if (!IsTransactionReference(session.ReferenceType))
        {
            return;
        }

        if (session.References.Count > 0)
        {
            await ActivateSeveralAsync(context);

            return;
        }

        if (string.IsNullOrEmpty(session.ReferenceId))
        {
            return;
        }

        var transaction = await _transactionManager.FindByIdAsync(session.ReferenceId);

        if (transaction is null || transaction.OutstandingAmount <= 0m)
        {
            return;
        }

        if (string.IsNullOrEmpty(session.Currency))
        {
            session.Currency = transaction.Currency;
        }

        // The balance already exists, so it must be paid now: a provider that only records a promise to pay later
        // can't settle it, and is neither offered nor accepted.
        session.SettlesExistingBalance = true;

        // A promotion code may be used: what it takes off the invoice is recorded on the transaction as a discount
        // when the checkout completes, so the balance is settled rather than left owing the discount.
        session.AcceptsDiscountsOnBalance = true;

        // What the payment is for, so a coupon limited to a source, a kind of charge or one customer can be checked.
        session.Put(new CheckoutDiscountScope
        {
            Source = transaction.Source,
            ReferenceType = transaction.ReferenceType,
            CustomerId = transaction.OwnerId,
            CustomerEmail = transaction.GuestContactEmail,
        });

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = StepKey,
            Title = S["Outstanding payment"],
            Description = transaction.Title,
            Order = 0,

            // The balance is what the customer came to pay, so the step asks them nothing; it exists only because
            // billing items belong to steps. Concealed, the checkout opens straight on the payment.
            CollectData = false,
            Conceal = true,
            BillingItems =
            [
                new BillingItem
                {
                    ItemId = transaction.ItemId,
                    Description = string.IsNullOrEmpty(transaction.Title)
                        ? S["Outstanding payment"].Value
                        : transaction.Title,
                    Amount = transaction.OutstandingAmount,
                    Plan = null,

                    // The transaction already carries its own tax, decided when it was raised. Settling it is
                    // paying that total, not a new sale, so it must not be taxed a second time.
                    ExcludeFromTax = true,
                },
            ],
        });
    }

    /// <inheritdoc/>
    public override async Task CompletedAsync(CheckoutFlowCompletedContext context)
    {
        if (context.Flow.Session is not CheckoutSession session)
        {
            return;
        }

        if (!IsTransactionReference(session.ReferenceType))
        {
            return;
        }

        if (session.References.Count > 0)
        {
            await CompleteSeveralAsync(session);

            return;
        }

        if (string.IsNullOrEmpty(session.ReferenceId))
        {
            return;
        }

        var transaction = await _transactionManager.FindByIdAsync(session.ReferenceId);

        if (transaction is null || transaction.Status == TransactionStatus.Paid)
        {
            return;
        }

        // A promise to pay later collects nothing. Counting it would mark the balance paid with no money received,
        // so only attempts from providers that collect money settle the transaction.
        var attempts = await GetCollectingAttemptsAsync(session);

        var paid = attempts.Any(attempt => attempt.State == PaymentAttemptState.Succeeded);
        var discounts = GetDiscounts(session);
        var discountTotal = Money.Round(discounts.Sum(discount => discount.Amount), transaction.Currency);

        // A code that covers the whole balance leaves nothing to collect, so the checkout completes with no payment.
        // Without a payment, a smaller code would leave the rest owing, so neither is recorded.
        if (!paid && (discountTotal <= 0m || discountTotal < transaction.OutstandingAmount))
        {
            _logger.LogWarning("Checkout session '{SessionId}' completed for transaction '{TransactionId}' but no confirmed payment attempt was found; the transaction is left unsettled.", session.SessionId, transaction.ItemId);

            return;
        }

        if (discountTotal > 0m)
        {
            transaction = await RecordDiscountsAsync(transaction, discounts, session);
        }

        if (paid && await _settlementService.ApplyAsync(transaction, attempts, session.SessionId) && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Transaction '{TransactionId}' was settled online through checkout session '{SessionId}'.", transaction.ItemId, session.SessionId);
        }

        // A code is used up only by a checkout that settled the transaction. One that left part of it owing (the
        // gateway confirmed less than was asked) keeps the code available.
        if (discounts.Length > 0 && transaction.OutstandingAmount <= 0m)
        {
            await RedeemAsync(context.Flow, discounts);
        }
    }

    // A checkout that settles several transactions with one payment: each is a line of its own, at what is still owed on
    // it. They are paid together only when one customer owes them all in one currency and each still has a balance.
    private async Task ActivateSeveralAsync(CheckoutFlowActivatingContext context)
    {
        var session = context.Session;
        var transactions = new List<Transaction>();

        foreach (var reference in session.References)
        {
            var transaction = await _transactionManager.FindByIdAsync(reference.ReferenceId);

            if (transaction is null)
            {
                context.Reject(S["A transaction to pay could not be found."]);

                continue;
            }

            if (transaction.OutstandingAmount <= 0m)
            {
                context.Reject(S["Nothing is left to pay on {0}.", GetTitle(transaction)]);

                continue;
            }

            if (!TransactionStateMachine.CanRecordPayment(transaction))
            {
                context.Reject(S["{0} can no longer be paid.", GetTitle(transaction)]);

                continue;
            }

            transactions.Add(transaction);
        }

        if (context.Errors.Count > 0)
        {
            return;
        }

        var first = transactions[0];

        if (transactions.Any(transaction => !string.Equals(GetOwnerKey(transaction), GetOwnerKey(first), StringComparison.Ordinal)))
        {
            context.Reject(S["Transactions of different customers can't be paid together."]);
        }

        var currency = string.IsNullOrEmpty(session.Currency) ? first.Currency : session.Currency;

        if (transactions.Any(transaction => !string.Equals(transaction.Currency, currency, StringComparison.OrdinalIgnoreCase)))
        {
            context.Reject(S["Transactions in different currencies can't be paid together."]);
        }

        if (context.Errors.Count > 0)
        {
            return;
        }

        session.Currency = currency;

        // The balances already exist, so they must be paid now: a provider that only records a promise to pay later
        // can't settle them, and is neither offered nor accepted.
        session.SettlesExistingBalance = true;

        // No promotion code is offered: a code would have to be split across the transactions, so, as for any balance
        // whose module doesn't take discounts, the checkout offers and applies none.
        session.AcceptsDiscountsOnBalance = false;

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = StepKey,
            Title = S["Outstanding payments"],
            Description = S["{0} payments", transactions.Count],
            Order = 0,
            CollectData = false,
            Conceal = true,
            BillingItems = [.. transactions.Select(transaction => new BillingItem
            {
                ItemId = transaction.ItemId,
                Description = GetTitle(transaction),
                Amount = transaction.OutstandingAmount,
                Plan = null,

                // Each transaction already carries its own tax, decided when it was raised.
                ExcludeFromTax = true,
            })],
        });
    }

    // Splits what the gateway confirmed across the transactions, in the order the checkout listed them, each up to what
    // the checkout asked for it and what it still owes. Each share is recorded on its transaction as a payment naming the
    // one attempt, so its status, receipt, refunds and lifecycle events work as for a transaction paid on its own.
    private async Task CompleteSeveralAsync(CheckoutSession session)
    {
        var attempts = (await GetCollectingAttemptsAsync(session))
            .Where(attempt => attempt.State == PaymentAttemptState.Succeeded)
            .OrderBy(attempt => attempt.CreatedUtc)
            .ToArray();

        if (attempts.Length == 0)
        {
            _logger.LogWarning("Checkout session '{SessionId}' completed for {Count} transactions but no confirmed payment attempt was found; they are left unsettled.", session.SessionId, session.References.Count);

            return;
        }

        var transactions = new List<Transaction>();

        foreach (var reference in session.References)
        {
            var transaction = await _transactionManager.FindByIdAsync(reference.ReferenceId);

            if (transaction is null)
            {
                _logger.LogWarning("Transaction '{TransactionId}' settled by checkout session '{SessionId}' could not be found; its share of the payment is not applied.", reference.ReferenceId, session.SessionId);

                continue;
            }

            transactions.Add(transaction);
        }

        // What is left of each attempt to share out. A share already applied by an earlier run of the completion stays
        // where it is, so running it again never applies the same money twice.
        var left = attempts.ToDictionary(attempt => attempt.ItemId, attempt => attempt.ConfirmedAmount + attempt.ConfirmedTaxAmount, StringComparer.Ordinal);

        foreach (var payment in transactions.SelectMany(transaction => transaction.Events)
            .Where(evt => evt.Type == TransactionEventType.PaymentRecorded && evt.PaymentAttemptId is not null && left.ContainsKey(evt.PaymentAttemptId)))
        {
            left[payment.PaymentAttemptId] -= payment.Amount ?? 0m;
        }

        var asked = GetAskedAmounts(session);

        foreach (var transaction in transactions)
        {
            if (transaction.Events.Any(evt => evt.Type == TransactionEventType.PaymentRecorded && evt.PaymentAttemptId is not null && left.ContainsKey(evt.PaymentAttemptId)))
            {
                continue;
            }

            var due = asked.TryGetValue(transaction.ItemId, out var amount)
                ? Math.Min(amount, transaction.OutstandingAmount)
                : transaction.OutstandingAmount;

            foreach (var attempt in attempts)
            {
                var share = Money.Round(Math.Min(due, left[attempt.ItemId]), transaction.Currency);

                if (due <= 0m || share <= 0m)
                {
                    continue;
                }

                if (await _settlementService.ApplyShareAsync(transaction, attempt, share, session.SessionId))
                {
                    left[attempt.ItemId] -= share;
                    due -= share;
                }
            }
        }

        var unapplied = left.Values.Where(amount => amount > 0m).Sum();

        if (unapplied > 0m)
        {
            _logger.LogWarning("Checkout session '{SessionId}' collected {Amount} more than its transactions still owed; it is not applied to any transaction and may need to be refunded.", session.SessionId, unapplied);
        }
    }

    // The attempts that may settle a transaction: a promise to pay later collects nothing, so counting it would mark the
    // balance paid with no money received.
    private async Task<PaymentAttempt[]> GetCollectingAttemptsAsync(CheckoutSession session)
        => (await _paymentAttemptStore.GetBySessionAsync(session.SessionId))
            .Where(attempt => string.IsNullOrEmpty(attempt.ProviderKey) || _providerResolver.GetProvider(attempt.ProviderKey)?.Capabilities.DefersPayment != true)
            .ToArray();

    // What the checkout asked for each transaction, from its line on the balance step.
    private static Dictionary<string, decimal> GetAskedAmounts(CheckoutSession session)
    {
        var amounts = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var item in session.Steps
            .Where(step => string.Equals(step.Key, StepKey, StringComparison.Ordinal))
            .SelectMany(step => step.BillingItems ?? [])
            .Where(item => !string.IsNullOrEmpty(item.ItemId)))
        {
            amounts[item.ItemId] = item.Amount;
        }

        return amounts;
    }

    // Who owes the transaction: an account, or a guest known by their email when they have no id.
    private static string GetOwnerKey(Transaction transaction)
        => string.IsNullOrEmpty(transaction.OwnerId)
            ? $"{transaction.OwnerKind}|email|{transaction.GuestContactEmail?.Trim().ToUpperInvariant()}"
            : $"{transaction.OwnerKind}|{transaction.OwnerId}";

    private string GetTitle(Transaction transaction)
        => string.IsNullOrEmpty(transaction.Title)
            ? S["Outstanding payment"].Value
            : transaction.Title;

    // The discounts the invoice applied to the balance: what each one actually took off, after the checkout clamped them
    // to the amount due.
    private static DiscountLine[] GetDiscounts(CheckoutSession session)
        => session.AcceptsDiscountsOnBalance && session.TryGet<CheckoutInvoice>(out var invoice) && invoice.Discounts is { Count: > 0 }
            ? invoice.Discounts.Where(discount => discount is not null && discount.Amount > 0m && discount.Target == DiscountTarget.OneTime).ToArray()
            : [];

    // Each code becomes a discount adjustment of exactly what it took off, never more than is still owed, through the
    // same command the console uses, so the timeline, the invoice and the lifecycle events show it like any other.
    private async Task<Transaction> RecordDiscountsAsync(Transaction transaction, DiscountLine[] discounts, CheckoutSession session)
    {
        var sessionId = session.SessionId;
        var commands = _serviceProvider.GetService<ITransactionCommandService>();

        if (commands is null)
        {
            _logger.LogWarning("Checkout session '{SessionId}' applied a promotion code to transaction '{TransactionId}', but no transaction command service is registered to record it.", sessionId, transaction.ItemId);

            return transaction;
        }

        foreach (var discount in discounts)
        {
            var amount = Money.Round(Math.Min(discount.Amount, transaction.OutstandingAmount), transaction.Currency);

            if (amount <= 0m)
            {
                continue;
            }

            var reason = string.IsNullOrEmpty(discount.Code)
                ? S["Promotion: {0}", discount.Description].Value
                : S["Promotion code {0}", discount.Code].Value;

            // The same code already recorded since this checkout started was recorded by this checkout: completing it
            // again must not take the discount off twice.
            if (transaction.Events.Any(evt => evt.Type == TransactionEventType.Adjusted && string.Equals(evt.Reason, reason, StringComparison.Ordinal) && evt.CreatedUtc >= session.CreatedUtc))
            {
                continue;
            }

            var result = await commands.AdjustAsync(transaction.ItemId, amount, TransactionAdjustmentKind.Discount, reason);

            if (!result.Succeeded)
            {
                _logger.LogWarning("The promotion code '{Code}' used in checkout session '{SessionId}' could not be recorded on transaction '{TransactionId}': {Errors}", discount.Code, sessionId, transaction.ItemId, string.Join(" ", result.Errors));
            }

            transaction = result.Transaction ?? transaction;
        }

        return transaction;
    }

    private async Task RedeemAsync(CheckoutFlow flow, DiscountLine[] discounts)
    {
        var redemption = new CheckoutDiscountRedemptionContext(flow, discounts);

        foreach (var provider in _serviceProvider.GetServices<ICheckoutDiscountProvider>())
        {
            try
            {
                await provider.RedeemAsync(redemption);
            }
            catch (Exception exception)
            {
                // The balance is settled. A redemption counter that failed to move is a bookkeeping problem to
                // reconcile, not a reason to undo the settlement.
                _logger.LogError(exception, "The discount provider '{ProviderKey}' failed to record its redemptions.", provider.Key);
            }
        }
    }

    private static bool IsTransactionReference(string referenceType)
        => string.Equals(referenceType, TransactionsConstants.ReferenceTypes.Transaction, StringComparison.OrdinalIgnoreCase);
}
