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
    /// The longest parent reference type or id the index holds.
    /// </summary>
    public const int MaxParentReferenceLength = 255;

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
                ParentReferenceType = TruncateParentReference(transaction.ParentReferenceType),
                ParentReferenceId = TruncateParentReference(transaction.ParentReferenceId),
                CheckoutSessionId = transaction.CheckoutSessionId,
                ObligationId = transaction.ObligationId,
                Currency = transaction.Currency,
                TotalAmount = transaction.TotalAmount,
                AmountPaid = transaction.AmountPaid,
                OutstandingAmount = CurrencyScale.Round(transaction.OutstandingAmount, transaction.Currency),
                Status = transaction.Status,
                DueUtc = transaction.DueUtc,
                NextCycleUtc = transaction.Recurrence?.GetNextCycleUtc(),
                CreatedUtc = transaction.CreatedUtc,
                UpdatedUtc = transaction.UpdatedUtc,
            });
    }

    // The parent reference columns are bounded, and a longer value must not stop the transaction from being saved. A
    // query truncates the value it looks for the same way, so it still finds the row.
    internal static string TruncateParentReference(string value)
        => string.IsNullOrEmpty(value)
            ? null
            : value.Length <= MaxParentReferenceLength ? value : value.Substring(0, MaxParentReferenceLength);
}
