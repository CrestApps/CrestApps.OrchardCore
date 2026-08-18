using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Transactions.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Transactions.Core.Indexes;

/// <summary>
/// Maps <see cref="Transaction"/> documents to <see cref="TransactionIndex"/> rows.
/// </summary>
public sealed class TransactionIndexProvider : IndexProvider<Transaction>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionIndexProvider"/> class.
    /// </summary>
    public TransactionIndexProvider()
    {
        CollectionName = TransactionsConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<Transaction> context)
    {
        context.For<TransactionIndex>()
            .Map(transaction => new TransactionIndex
            {
                ItemId = transaction.ItemId,
                Title = transaction.Title,
                Source = transaction.Source,
                OwnerId = transaction.OwnerId,
                OwnerKind = transaction.OwnerKind,
                ReferenceType = transaction.ReferenceType,
                ReferenceId = transaction.ReferenceId,
                CheckoutSessionId = transaction.CheckoutSessionId,
                ObligationId = transaction.ObligationId,
                Currency = transaction.Currency,
                TotalAmount = transaction.TotalAmount,
                AmountPaid = transaction.AmountPaid,
                OutstandingAmount = CurrencyScale.Round(Math.Max(0m, transaction.TotalAmount - transaction.AmountPaid), transaction.Currency),
                Status = transaction.Status,
                DueUtc = transaction.DueUtc,
                CreatedUtc = transaction.CreatedUtc,
                UpdatedUtc = transaction.UpdatedUtc,
            });
    }
}
