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

    private static decimal Round(decimal amount)
        => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
