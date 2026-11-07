using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Payments;

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
    /// <param name="lineTotal">The line's total.</param>
    /// <param name="orderSubtotal">The order's subtotal.</param>
    /// <param name="orderDiscount">The order's discounts.</param>
    /// <param name="currency">The order's currency, or <see langword="null"/> for two decimals.</param>
    public static decimal GetLineGross(decimal lineTotal, decimal orderSubtotal, decimal orderDiscount, string currency = null)
    {
        if (lineTotal <= 0m)
        {
            return 0m;
        }

        var share = orderSubtotal > 0m ? Math.Min(orderDiscount, orderSubtotal) * lineTotal / orderSubtotal : 0m;

        return Money.Round(lineTotal - share, currency);
    }

    /// <summary>
    /// Returns the store's commission on an amount and what's left for the vendor.
    /// </summary>
    /// <param name="gross">What the line paid.</param>
    /// <param name="commissionPercent">The store's commission, in percent.</param>
    /// <param name="currency">The amount's currency, or <see langword="null"/> for two decimals.</param>
    public static (decimal Commission, decimal Net) Split(decimal gross, decimal commissionPercent, string currency = null)
    {
        var commission = Money.Round(gross * Math.Clamp(commissionPercent, 0m, 100m) / 100m, currency);

        return (commission, Money.Round(gross - commission, currency));
    }

    /// <summary>
    /// Returns what a refund of <paramref name="refunded"/> out of an order's <paramref name="paid"/> takes back of a
    /// sale of <paramref name="gross"/>: the same share.
    /// </summary>
    /// <param name="gross">What the sale paid.</param>
    /// <param name="refunded">What the refund gives back.</param>
    /// <param name="paid">What the order paid.</param>
    /// <param name="currency">The order's currency, or <see langword="null"/> for two decimals.</param>
    public static decimal GetRefundShare(decimal gross, decimal refunded, decimal paid, string currency = null)
        => paid <= 0m || refunded <= 0m ? 0m : Money.Round(gross * Math.Min(1m, refunded / paid), currency);

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
}
