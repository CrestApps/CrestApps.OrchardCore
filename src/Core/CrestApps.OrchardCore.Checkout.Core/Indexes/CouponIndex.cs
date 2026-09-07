using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Checkout.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Checkout.Core.Indexes;

/// <summary>
/// The queryable projection of a <see cref="Coupon"/>.
/// </summary>
public sealed class CouponIndex : CatalogItemIndex
{
    /// <summary>
    /// The coupon code, normalized to upper case so a lookup is not at the mercy of how the customer typed
    /// it or how the database collates.
    /// </summary>
    public string NormalizedCode { get; set; }

    /// <summary>
    /// Whether the coupon can be used at all.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// The UTC time the coupon stops being valid.
    /// </summary>
    public DateTime? EndsUtc { get; set; }

    /// <summary>
    /// The UTC time the coupon was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// Maps <see cref="Coupon"/> documents to <see cref="CouponIndex"/> rows.
/// </summary>
public sealed class CouponIndexProvider : IndexProvider<Coupon>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CouponIndexProvider"/> class.
    /// </summary>
    public CouponIndexProvider()
    {
        CollectionName = CheckoutConstants.CouponCollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<Coupon> context)
    {
        context.For<CouponIndex>()
            .Map(coupon => new CouponIndex
            {
                ItemId = coupon.ItemId,
                NormalizedCode = coupon.Code?.ToUpperInvariant(),
                IsEnabled = coupon.IsEnabled,
                EndsUtc = coupon.EndsUtc,
                CreatedUtc = coupon.CreatedUtc,
            });
    }
}
