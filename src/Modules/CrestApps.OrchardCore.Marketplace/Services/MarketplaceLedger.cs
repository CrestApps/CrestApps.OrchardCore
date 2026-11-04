using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CrestApps.OrchardCore.Marketplace.Services;

/// <summary>
/// The marketplace's money: what a line paid after its share of the order's discounts, the store's commission on it,
/// and what a refund takes back of each vendor's share.
/// </summary>
public static class MarketplaceLedger
{
    /// <summary>
    /// Returns what a line paid: its total less its share of the order's discounts.
    /// </summary>
    public static decimal GetLineGross(decimal lineTotal, decimal orderSubtotal, decimal orderDiscount)
    {
        if (lineTotal <= 0m)
        {
            return 0m;
        }

        var share = orderSubtotal > 0m ? Math.Min(orderDiscount, orderSubtotal) * lineTotal / orderSubtotal : 0m;

        return Round(lineTotal - share);
    }

    /// <summary>
    /// Returns the store's commission on an amount and what's left for the vendor.
    /// </summary>
    public static (decimal Commission, decimal Net) Split(decimal gross, decimal commissionPercent)
    {
        var commission = Round(gross * Math.Clamp(commissionPercent, 0m, 100m) / 100m);

        return (commission, Round(gross - commission));
    }

    /// <summary>
    /// Returns what a refund of <paramref name="refunded"/> out of an order's <paramref name="paid"/> takes back of a
    /// sale of <paramref name="gross"/>: the same share.
    /// </summary>
    public static decimal GetRefundShare(decimal gross, decimal refunded, decimal paid)
        => paid <= 0m || refunded <= 0m ? 0m : Round(gross * Math.Min(1m, refunded / paid));

    /// <summary>
    /// Returns the idempotency key of a payout: the same for the same vendor, order and earnings, so a payout retried
    /// after its save failed reuses the key of the transfer that went through and is not paid twice. A new
    /// <paramref name="refusals"/> count makes a new key, so a retry after the provider refused the transfer is a new
    /// request rather than the provider's recorded refusal.
    /// </summary>
    /// <param name="vendorId">The vendor paid.</param>
    /// <param name="orderId">The order the earnings come from.</param>
    /// <param name="earningIds">The earnings the payout pays.</param>
    /// <param name="refusals">How many payouts of these earnings the provider refused before.</param>
    public static string GetPayoutKey(string vendorId, string orderId, IEnumerable<string> earningIds, int refusals)
    {
        ArgumentException.ThrowIfNullOrEmpty(vendorId);
        ArgumentNullException.ThrowIfNull(earningIds);

        var source = string.Join('|', [vendorId, orderId ?? string.Empty, .. earningIds.Order(StringComparer.Ordinal), refusals.ToString(CultureInfo.InvariantCulture)]);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }

    private static decimal Round(decimal amount)
        => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
