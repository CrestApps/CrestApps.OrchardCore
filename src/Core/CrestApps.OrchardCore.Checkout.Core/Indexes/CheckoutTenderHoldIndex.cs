using CrestApps.OrchardCore.Checkout.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Checkout.Core.Indexes;

/// <summary>
/// The queryable projection of a <see cref="CheckoutTenderHold"/>.
/// </summary>
public sealed class CheckoutTenderHoldIndex : MapIndex
{
    public string ItemId { get; set; }

    public string SessionId { get; set; }

    public string ProviderKey { get; set; }

    public string Reference { get; set; }

    public string Status { get; set; }
}

/// <summary>
/// Maps <see cref="CheckoutTenderHold"/> documents to <see cref="CheckoutTenderHoldIndex"/> rows.
/// </summary>
public sealed class CheckoutTenderHoldIndexProvider : IndexProvider<CheckoutTenderHold>
{
    public override void Describe(DescribeContext<CheckoutTenderHold> context)
    {
        context.For<CheckoutTenderHoldIndex>()
            .Map(hold => new CheckoutTenderHoldIndex
            {
                ItemId = hold.ItemId,
                SessionId = hold.SessionId,
                ProviderKey = hold.ProviderKey,
                Reference = hold.Reference,
                Status = hold.Status.ToString(),
            });
    }
}
