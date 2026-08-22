using CrestApps.Core.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Bridges the checkout framework and the transaction ledger so an outstanding transaction can be settled
/// online. When a checkout session references a transaction, the handler contributes the outstanding
/// balance as a one-time billing item and, once the checkout completes, settles the transaction against
/// the amount the payment provider actually confirmed rather than the amount the checkout requested.
/// </summary>
public sealed class TransactionSettlementCheckoutHandler : CheckoutHandlerBase
{
    private readonly ITransactionManager _transactionManager;
    private readonly IPaymentAttemptStore _paymentAttemptStore;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionSettlementCheckoutHandler"/> class.
    /// </summary>
    /// <param name="transactionManager">The transaction manager.</param>
    /// <param name="paymentAttemptStore">The durable payment-attempt ledger used to read confirmed amounts.</param>
    /// <param name="clock">The clock used for settlement timestamps.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionSettlementCheckoutHandler(
        ITransactionManager transactionManager,
        IPaymentAttemptStore paymentAttemptStore,
        IClock clock,
        ILogger<TransactionSettlementCheckoutHandler> logger,
        IStringLocalizer<TransactionSettlementCheckoutHandler> stringLocalizer)
    {
        _transactionManager = transactionManager;
        _paymentAttemptStore = paymentAttemptStore;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override async Task ActivatingAsync(CheckoutFlowActivatingContext context)
    {
        var session = context.Session;

        if (!IsTransactionReference(session.ReferenceType) || string.IsNullOrEmpty(session.ReferenceId))
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

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = "TransactionSettlement",
            Title = S["Outstanding payment"],
            Description = transaction.Title,
            Order = 0,
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

        if (!IsTransactionReference(session.ReferenceType) || string.IsNullOrEmpty(session.ReferenceId))
        {
            return;
        }

        var transaction = await _transactionManager.FindByIdAsync(session.ReferenceId);

        if (transaction is null || transaction.Status == TransactionStatus.Paid)
        {
            return;
        }

        var attempts = await _paymentAttemptStore.GetBySessionAsync(session.SessionId);
        var confirmed = attempts.Where(attempt => attempt.State == PaymentAttemptState.Succeeded).ToArray();

        if (confirmed.Length == 0)
        {
            _logger.LogWarning("Checkout session '{SessionId}' completed for transaction '{TransactionId}' but no confirmed payment attempt was found; the transaction is left unsettled.", session.SessionId, transaction.ItemId);

            return;
        }

        // Every confirmed attempt must be in the transaction currency. An empty attempt currency does not
        // match a typed transaction currency, so the check fails closed and no implicit conversion is ever
        // applied.
        foreach (var attempt in confirmed)
        {
            if (!string.Equals(attempt.Currency, transaction.Currency, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Refusing to settle transaction '{TransactionId}' ({TransactionCurrency}) from payment attempt '{AttemptId}' ({AttemptCurrency}): the currencies differ and no conversion is applied.", transaction.ItemId, transaction.Currency, attempt.ItemId, attempt.Currency);

                return;
            }
        }

        // Idempotency is per confirmed attempt, recorded on the durable timeline. An attempt already applied
        // is never counted again, even when a later partial settlement from a different session overwrote
        // the scalar settlement reference or a webhook is replayed out of order.
        var appliedAttemptIds = transaction.Events
            .Where(payment => payment.Type == TransactionEventType.PaymentRecorded && !string.IsNullOrEmpty(payment.PaymentAttemptId))
            .Select(payment => payment.PaymentAttemptId)
            .ToHashSet(StringComparer.Ordinal);

        var newAttempts = confirmed.Where(attempt => !appliedAttemptIds.Contains(attempt.ItemId)).ToArray();

        if (newAttempts.Length == 0)
        {
            return;
        }

        var confirmedTotal = newAttempts.Sum(attempt => attempt.ConfirmedAmount + attempt.ConfirmedTaxAmount);

        if (confirmedTotal <= 0m)
        {
            _logger.LogWarning("Checkout session '{SessionId}' completed for transaction '{TransactionId}' but the confirmed payment amount was not positive; the transaction is left unsettled.", session.SessionId, transaction.ItemId);

            return;
        }

        var now = _clock.UtcNow;

        transaction.AmountPaid = CurrencyScale.Round(transaction.AmountPaid + confirmedTotal, transaction.Currency);

        var fullyPaid = transaction.AmountPaid >= transaction.TotalAmount;

        transaction.PaymentAttemptId = newAttempts.Last().ItemId;
        transaction.Status = fullyPaid ? TransactionStatus.Paid : TransactionStatus.PartiallyPaid;
        transaction.SettlementMethod = TransactionsConstants.SettlementMethods.Online;
        transaction.SettlementReference = session.SessionId;
        transaction.UpdatedUtc = now;

        if (fullyPaid)
        {
            transaction.SettledUtc = now;
        }

        foreach (var attempt in newAttempts)
        {
            transaction.Events.Add(new TransactionEvent
            {
                CreatedUtc = now,
                Type = TransactionEventType.PaymentRecorded,
                PaymentAttemptId = attempt.ItemId,
                Message = S["Recorded a payment of {0} {1} against the transaction from checkout session '{2}' (payment attempt '{3}').", CurrencyScale.Format(attempt.ConfirmedAmount + attempt.ConfirmedTaxAmount, transaction.Currency), transaction.Currency, session.SessionId, attempt.ItemId].Value,
            });
        }

        try
        {
            await _transactionManager.UpdateAsync(transaction);
        }
        catch (ConcurrencyException)
        {
            _logger.LogWarning("A concurrency conflict prevented settling transaction '{TransactionId}' from checkout session '{SessionId}'; another writer updated it first.", transaction.ItemId, session.SessionId);

            return;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Transaction '{TransactionId}' was settled online through checkout session '{SessionId}'.", transaction.ItemId, session.SessionId);
        }
    }

    private static bool IsTransactionReference(string referenceType)
        => string.Equals(referenceType, TransactionsConstants.ReferenceTypes.Transaction, StringComparison.OrdinalIgnoreCase);
}
