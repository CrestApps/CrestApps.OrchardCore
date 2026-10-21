using CrestApps.OrchardCore.Checkout.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Checkout.Core.Indexes;

/// <summary>
/// Finds the uses of a coupon, of a checkout, and of a customer.
/// </summary>
public sealed class CouponRedemptionIndex : MapIndex
{
    public string ItemId { get; set; }

    public string NormalizedCode { get; set; }

    public string SessionId { get; set; }

    public string CustomerId { get; set; }

    public string NormalizedEmail { get; set; }

    public string Status { get; set; }

    public bool IsCounted { get; set; }

    public DateTime? ExpiresUtc { get; set; }

    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// Maps a <see cref="CouponRedemption"/> to its <see cref="CouponRedemptionIndex"/>.
/// </summary>
public sealed class CouponRedemptionIndexProvider : IndexProvider<CouponRedemption>
{
    public CouponRedemptionIndexProvider()
    {
        CollectionName = CheckoutConstants.CouponCollectionName;
    }

    public override void Describe(DescribeContext<CouponRedemption> context)
    {
        context.For<CouponRedemptionIndex>()
            .Map(redemption => new CouponRedemptionIndex
            {
                ItemId = redemption.ItemId,
                NormalizedCode = Normalize(redemption.Code),
                SessionId = redemption.SessionId,
                CustomerId = redemption.CustomerId,
                NormalizedEmail = Normalize(redemption.CustomerEmail),
                Status = redemption.Status.ToString(),
                IsCounted = redemption.IsCounted,
                ExpiresUtc = redemption.ExpiresUtc,
                CreatedUtc = redemption.CreatedUtc,
            });
    }

    internal static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
