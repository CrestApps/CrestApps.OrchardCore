using CrestApps.OrchardCore.Transactions.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Transactions.Core.Indexes;

/// <summary>
/// Maps a payment term to its <see cref="PaymentTermIndex"/>.
/// </summary>
public sealed class PaymentTermIndexProvider : IndexProvider<PaymentTerm>
{
    /// <summary>
    /// The length of the name column.
    /// </summary>
    public const int MaxNameLength = 255;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentTermIndexProvider"/> class.
    /// </summary>
    public PaymentTermIndexProvider()
    {
        CollectionName = TransactionsConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<PaymentTerm> context)
    {
        context
            .For<PaymentTermIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(term => new PaymentTermIndex
            {
                ItemId = term.ItemId,
                Name = term.Name?.Length > MaxNameLength ? term.Name.Substring(0, MaxNameLength) : term.Name,
                Enabled = term.Enabled,
            });
    }
}
