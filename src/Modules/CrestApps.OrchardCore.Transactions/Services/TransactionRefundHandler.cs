using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionRefundHandler"/> class.
    /// </summary>
    /// <param name="transactionManager">The transaction manager.</param>
    /// <param name="serviceProvider">The service provider the optional payment attempt store and the transaction event handlers are resolved from.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger for event handlers that fail.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionRefundHandler(
        ITransactionManager transactionManager,
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<TransactionRefundHandler> logger,
        IStringLocalizer<TransactionRefundHandler> stringLocalizer)
    {
        _transactionManager = transactionManager;
        _serviceProvider = serviceProvider;
        _clock = clock;
        _logger = logger;
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
            await SplitAsync(refund, attempt, await SharedPaymentAttempts.GetTransactionIdsAsync(_serviceProvider, attempt, cancellationToken), cancellationToken);

            return;
        }

        var transaction = await FindTransactionAsync(refund, attempt, cancellationToken);

        // The checkout's payment also paid other debts of the same checkout (an invoice's debit note for the shipping its
        // order's checkout added): the refund is split over them, this one first, each up to its share. Without this the
        // whole refund went on this one, more than it was paid, and the others kept saying paid.
        if (await GetSharedWithAsync(transaction, refund, attempt, cancellationToken) is { Count: > 1 } shared)
        {
            await SplitAsync(refund, attempt, shared, cancellationToken);

            return;
        }

        await RecordAsync(transaction, refund, refund.RefundGrossAmount, cancellationToken);
    }

    // The transaction first, then the other transactions of the checkout that recorded a share of the same payment.
    private async Task<IReadOnlyList<string>> GetSharedWithAsync(Transaction transaction, PaymentRefund refund, PaymentAttempt attempt, CancellationToken cancellationToken)
    {
        if (transaction is null ||
            attempt is null ||
            string.IsNullOrEmpty(refund.SessionId) ||
            SharedPaymentAttempts.GetAppliedAmount(transaction, attempt.ItemId) <= 0m ||
            _serviceProvider.GetService<ITransactionStore>() is not { } store)
        {
            return [];
        }

        var others = await store.GetByCheckoutSessionAsync(refund.SessionId, cancellationToken);

        return [transaction.ItemId, .. others
            .Where(other => !string.Equals(other.ItemId, transaction.ItemId, StringComparison.Ordinal) && SharedPaymentAttempts.GetAppliedAmount(other, attempt.ItemId) > 0m)
            .Select(other => other.ItemId)];
    }

    // A refund of a payment that settled several transactions: it is split back over them in the order given (the order
    // the checkout listed them, or the one the refund was made for first), each up to what it still has of the payment.
    private async Task SplitAsync(PaymentRefund refund, PaymentAttempt attempt, IReadOnlyList<string> transactionIds, CancellationToken cancellationToken)
    {
        var left = refund.RefundGrossAmount;

        foreach (var transactionId in transactionIds)
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

        var entry = new TransactionEvent
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
        };

        transaction.Events.Add(entry);

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
        await _serviceProvider.GetServices<ITransactionEventHandler>().RefundedAsync(transaction, entry, _logger, cancellationToken);
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
