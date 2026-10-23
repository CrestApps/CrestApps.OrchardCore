using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Reads a payment that settled several transactions at once: one checkout, one attempt, and a share of it recorded on
/// each transaction as a payment naming the attempt.
/// </summary>
internal static class SharedPaymentAttempts
{
    /// <summary>
    /// Gets whether the attempt paid for several transactions. Such an attempt names the transaction reference type but
    /// no single transaction; its checkout lists them.
    /// </summary>
    /// <param name="attempt">The payment attempt.</param>
    public static bool PaysSeveralTransactions(PaymentAttempt attempt)
        => attempt is not null &&
            string.Equals(attempt.ReferenceType, TransactionsConstants.ReferenceTypes.Transaction, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrEmpty(attempt.ReferenceId);

    /// <summary>
    /// Gets the transactions the attempt's checkout settled, in the order it listed them.
    /// </summary>
    /// <param name="serviceProvider">The service provider the checkout session store is resolved from. It is resolved when
    /// needed, because the store runs the checkout handlers, which depend on the transaction services.</param>
    /// <param name="attempt">The payment attempt.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public static async Task<IReadOnlyList<string>> GetTransactionIdsAsync(IServiceProvider serviceProvider, PaymentAttempt attempt, CancellationToken cancellationToken)
    {
        if (!PaysSeveralTransactions(attempt) ||
            string.IsNullOrEmpty(attempt.SessionId) ||
            serviceProvider.GetService<ICheckoutSessionStore>() is not { } sessionStore)
        {
            return [];
        }

        var sessions = await sessionStore.GetManyAsync([attempt.SessionId], cancellationToken);

        if (sessions.Count == 0)
        {
            return [];
        }

        return [.. sessions[0].References
            .Where(reference => string.Equals(reference.ReferenceType, TransactionsConstants.ReferenceTypes.Transaction, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(reference.ReferenceId))
            .Select(reference => reference.ReferenceId)];
    }

    /// <summary>
    /// Gets the share of the attempt recorded on the transaction as paid.
    /// </summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="attemptId">The payment attempt identifier.</param>
    public static decimal GetAppliedAmount(Transaction transaction, string attemptId)
        => CurrencyScale.Round(
            transaction.Events
                .Where(evt => evt.Type == TransactionEventType.PaymentRecorded && string.Equals(evt.PaymentAttemptId, attemptId, StringComparison.Ordinal))
                .Sum(evt => evt.Amount ?? 0m),
            transaction.Currency);

    /// <summary>
    /// Gets what was given back from the attempt's share of the transaction, as recorded on the transaction's timeline.
    /// </summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="attemptId">The payment attempt identifier.</param>
    /// <param name="excludedRefundIds">Refunds not to count, because they are counted elsewhere.</param>
    public static decimal GetRecordedRefunds(Transaction transaction, string attemptId, ISet<string> excludedRefundIds = null)
        => CurrencyScale.Round(
            transaction.Events
                .Where(evt => evt.Type == TransactionEventType.Refunded &&
                    string.Equals(evt.PaymentAttemptId, attemptId, StringComparison.Ordinal) &&
                    (excludedRefundIds is null || evt.Id is null || !excludedRefundIds.Contains(evt.Id)))
                .Sum(evt => evt.Amount ?? 0m),
            transaction.Currency);
}
