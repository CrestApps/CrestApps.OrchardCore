using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Records a finished refund on the transaction it gives money back for: a line on its timeline, the running total
/// refunded, and the <see cref="TransactionStatus.Refunded"/> status once everything paid has gone back. Without it
/// the transaction kept saying <see cref="TransactionStatus.Paid"/> after the customer had their money back.
/// </summary>
public sealed class TransactionRefundHandler : IPaymentRefundHandler
{
    private readonly ITransactionManager _transactionManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionRefundHandler"/> class.
    /// </summary>
    /// <param name="transactionManager">The transaction manager.</param>
    /// <param name="serviceProvider">The service provider the optional payment attempt store is resolved from.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionRefundHandler(
        ITransactionManager transactionManager,
        IServiceProvider serviceProvider,
        IClock clock,
        IStringLocalizer<TransactionRefundHandler> stringLocalizer)
    {
        _transactionManager = transactionManager;
        _serviceProvider = serviceProvider;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task RefundSucceededAsync(PaymentRefund refund, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refund);

        // A refund started from one of the transactions a shared payment settled is recorded on that one only.
        if (string.Equals(refund.ReferenceType, TransactionsConstants.ReferenceTypes.Transaction, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(refund.ReferenceId))
        {
            await RecordAsync(await _transactionManager.FindByIdAsync(refund.ReferenceId, cancellationToken), refund, refund.RefundGrossAmount, cancellationToken);

            return;
        }

        var attempt = !string.IsNullOrEmpty(refund.OriginalAttemptId) && _serviceProvider.GetService<IPaymentAttemptStore>() is { } attemptStore
            ? await attemptStore.FindByIdAsync(refund.OriginalAttemptId, cancellationToken)
            : null;

        if (SharedPaymentAttempts.PaysSeveralTransactions(attempt))
        {
            await SplitAsync(refund, attempt, cancellationToken);

            return;
        }

        await RecordAsync(await FindTransactionAsync(refund, attempt, cancellationToken), refund, refund.RefundGrossAmount, cancellationToken);
    }

    // A refund of a payment that settled several transactions, made for none of them in particular (from the payments
    // page or the gateway's dashboard): it is split back over them in the order the checkout listed them, each up to
    // what it still has of the payment.
    private async Task SplitAsync(PaymentRefund refund, PaymentAttempt attempt, CancellationToken cancellationToken)
    {
        var left = refund.RefundGrossAmount;

        foreach (var transactionId in await SharedPaymentAttempts.GetTransactionIdsAsync(_serviceProvider, attempt, cancellationToken))
        {
            if (left <= 0m)
            {
                break;
            }

            var transaction = await _transactionManager.FindByIdAsync(transactionId, cancellationToken);

            if (transaction is null)
            {
                continue;
            }

            // Already recorded on this one: what it took is not shared out again.
            var recorded = transaction.Events.FirstOrDefault(evt => evt.Type == TransactionEventType.Refunded && string.Equals(evt.Id, refund.ItemId, StringComparison.Ordinal));

            if (recorded is not null)
            {
                left -= recorded.Amount ?? 0m;

                continue;
            }

            var available = SharedPaymentAttempts.GetAppliedAmount(transaction, attempt.ItemId) - SharedPaymentAttempts.GetRecordedRefunds(transaction, attempt.ItemId);
            var portion = CurrencyScale.Round(Math.Min(left, available), transaction.Currency);

            if (portion <= 0m)
            {
                continue;
            }

            await RecordAsync(transaction, refund, portion, cancellationToken);

            left -= portion;
        }
    }

    private async Task RecordAsync(Transaction transaction, PaymentRefund refund, decimal grossAmount, CancellationToken cancellationToken)
    {
        // A refund is reported once per status change, but a reconciliation can report one the service already
        // reported. The refund's own identifier on the timeline keeps it from counting twice.
        if (transaction is null ||
            transaction.Events.Any(evt => evt.Type == TransactionEventType.Refunded && string.Equals(evt.Id, refund.ItemId, StringComparison.Ordinal)))
        {
            return;
        }

        var now = _clock.UtcNow;
        var amount = CurrencyScale.Round(grossAmount, transaction.Currency);
        var formatted = $"{CurrencyScale.Format(amount, transaction.Currency)} {transaction.Currency}";

        transaction.AmountRefunded = CurrencyScale.Round(transaction.AmountRefunded + amount, transaction.Currency);
        transaction.UpdatedUtc = now;
        transaction.Events.Add(new TransactionEvent
        {
            Id = refund.ItemId,
            CreatedUtc = now,
            Type = TransactionEventType.Refunded,
            Amount = amount,
            Method = refund.ProviderKey,
            PaymentAttemptId = refund.OriginalAttemptId,
            Message = string.IsNullOrWhiteSpace(refund.Reason)
                ? S["A refund of {0} was given back to the customer.", formatted].Value
                : S["A refund of {0} was given back to the customer. Reason: {1}", formatted, refund.Reason].Value,
        });

        // Only a full refund changes the status: after a partial one the customer still has part of what they
        // paid for, so the transaction stays paid.
        if (transaction.AmountPaid > 0m && transaction.AmountRefunded >= transaction.AmountPaid)
        {
            transaction.Status = TransactionStatus.Refunded;
            transaction.Events.Add(new TransactionEvent
            {
                CreatedUtc = now,
                Type = TransactionEventType.StatusChanged,
                Message = S["Everything paid was refunded."].Value,
            });
        }

        await _transactionManager.UpdateAsync(transaction, data: null, cancellationToken);
    }

    // A pay-link checkout names the transaction on its payment; a purchase checkout created the transaction for
    // the obligation the refund returns.
    private async Task<Transaction> FindTransactionAsync(PaymentRefund refund, PaymentAttempt attempt, CancellationToken cancellationToken)
    {
        if (attempt is not null &&
            string.Equals(attempt.ReferenceType, TransactionsConstants.ReferenceTypes.Transaction, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(attempt.ReferenceId))
        {
            return await _transactionManager.FindByIdAsync(attempt.ReferenceId, cancellationToken);
        }

        if (!string.IsNullOrEmpty(refund.SessionId) && !string.IsNullOrEmpty(refund.ObligationId))
        {
            return await _transactionManager.GetByObligationAsync(refund.SessionId, refund.ObligationId, cancellationToken);
        }

        return null;
    }
}
