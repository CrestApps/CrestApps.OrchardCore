using CrestApps.OrchardCore.Transactions.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Transactions.Core.Indexes;

/// <summary>
/// Maps the <see cref="Transaction"/> documents that carry a parent reference to
/// <see cref="TransactionParentReferenceIndex"/> rows.
/// </summary>
public sealed class TransactionParentReferenceIndexProvider : IndexProvider<Transaction>
{
    /// <summary>
    /// The longest value either column holds.
    /// </summary>
    public const int MaxLength = 255;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionParentReferenceIndexProvider"/> class.
    /// </summary>
    public TransactionParentReferenceIndexProvider()
    {
        CollectionName = TransactionsConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<Transaction> context)
    {
        context.For<TransactionParentReferenceIndex>()
            .Map(transaction =>
            {
                if (string.IsNullOrEmpty(transaction.ParentReferenceType) || string.IsNullOrEmpty(transaction.ParentReferenceId))
                {
                    return null;
                }

                return new TransactionParentReferenceIndex
                {
                    ItemId = transaction.ItemId,
                    ParentReferenceType = Truncate(transaction.ParentReferenceType),
                    ParentReferenceId = Truncate(transaction.ParentReferenceId),
                };
            });
    }

    // The columns are bounded, and a longer value must not stop the transaction from being saved. A query truncates
    // the value it looks for the same way, so it still finds the row.
    internal static string Truncate(string value)
        => value is null || value.Length <= MaxLength ? value : value.Substring(0, MaxLength);
}
