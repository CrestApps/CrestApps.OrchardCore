using CrestApps.OrchardCore.Plaid.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Plaid.Indexes;

/// <summary>
/// Finds a kept Plaid item by its Plaid id, for webhooks, and by its bank account, when the account changes.
/// </summary>
public sealed class PlaidItemIndex : MapIndex
{
    /// <summary>
    /// Gets or sets Plaid's id of the item.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the id of the bank account.
    /// </summary>
    public string BankAccountId { get; set; }
}

/// <summary>
/// Maps kept Plaid items to <see cref="PlaidItemIndex"/>.
/// </summary>
public sealed class PlaidItemIndexProvider : IndexProvider<PlaidItem>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlaidItemIndexProvider"/> class.
    /// </summary>
    public PlaidItemIndexProvider()
    {
        CollectionName = PlaidConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<PlaidItem> context)
    {
        context
            .For<PlaidItemIndex>()
            .Map(item => new PlaidItemIndex
            {
                ItemId = item.ItemId,
                BankAccountId = item.BankAccountId,
            });
    }
}
