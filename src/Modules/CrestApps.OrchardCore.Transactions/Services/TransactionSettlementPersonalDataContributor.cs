using CrestApps.OrchardCore.Customers.PersonalData;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Adds the checkouts that paid a person's transactions to what identifies them, so the payments, refunds and contact
/// details of those checkouts are exported and anonymized with the transactions. A transaction can be paid down over
/// several checkouts, and only the last one is recorded on the transaction itself.
/// </summary>
public sealed class TransactionSettlementPersonalDataContributor : ICommercePersonalDataContributor
{
    private readonly ITransactionManager _transactionManager;
    private readonly IPaymentAttemptStore _paymentAttemptStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionSettlementPersonalDataContributor"/> class.
    /// </summary>
    /// <param name="transactionManager">The transaction ledger.</param>
    /// <param name="paymentAttemptStore">The durable payment ledger.</param>
    public TransactionSettlementPersonalDataContributor(
        ITransactionManager transactionManager,
        IPaymentAttemptStore paymentAttemptStore)
    {
        _transactionManager = transactionManager;
        _paymentAttemptStore = paymentAttemptStore;
    }

    /// <inheritdoc/>
    public async Task IdentifyAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        foreach (var transaction in await TransactionPersonalDataContributor.GetTransactionsAsync(_transactionManager, subject, cancellationToken))
        {
            var attempts = await _paymentAttemptStore.GetByReferenceAsync(TransactionsConstants.ReferenceTypes.Transaction, transaction.ItemId, cancellationToken);

            foreach (var attempt in attempts)
            {
                subject.AddCheckoutSession(attempt.SessionId);
            }

            // A payment that settled several transactions at once names none of them, so its checkout is found through
            // the payments recorded on the transaction.
            foreach (var attemptId in transaction.Events
                .Where(evt => evt.Type == TransactionEventType.PaymentRecorded && !string.IsNullOrEmpty(evt.PaymentAttemptId))
                .Select(evt => evt.PaymentAttemptId)
                .Distinct(StringComparer.Ordinal))
            {
                var attempt = await _paymentAttemptStore.FindByIdAsync(attemptId, cancellationToken);

                if (attempt is not null)
                {
                    subject.AddCheckoutSession(attempt.SessionId);
                }
            }
        }
    }

    /// <inheritdoc/>
    public Task ExportAsync(CommercePersonalDataExportContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task AnonymizeAsync(CommercePersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
