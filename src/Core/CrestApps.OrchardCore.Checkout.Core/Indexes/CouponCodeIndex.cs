using CrestApps.OrchardCore.Checkout.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Checkout.Core.Indexes;

/// <summary>
/// The queryable projection of a <see cref="CouponCode"/>.
/// </summary>
public sealed class CouponCodeIndex : MapIndex
{
    /// <summary>
    /// The code's identifier.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// The identifier of the coupon it was generated for.
    /// </summary>
    public string CouponId { get; set; }

    /// <summary>
    /// The code, normalized to upper case, as <see cref="CouponIndex.NormalizedCode"/>.
    /// </summary>
    public string NormalizedCode { get; set; }

    /// <summary>
    /// How many times it has been used.
    /// </summary>
    public int RedemptionCount { get; set; }

    /// <summary>
    /// The UTC time it was generated.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// Maps <see cref="CouponCode"/> documents to <see cref="CouponCodeIndex"/> rows.
/// </summary>
public sealed class CouponCodeIndexProvider : IndexProvider<CouponCode>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CouponCodeIndexProvider"/> class.
    /// </summary>
    public CouponCodeIndexProvider()
    {
        CollectionName = CheckoutConstants.CouponCollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<CouponCode> context)
    {
        context.For<CouponCodeIndex>()
            .Map(code => new CouponCodeIndex
            {
                ItemId = code.ItemId,
                CouponId = code.CouponId,
                NormalizedCode = code.Code?.ToUpperInvariant(),
                RedemptionCount = code.RedemptionCount,
                CreatedUtc = code.CreatedUtc,
            });
    }
}
