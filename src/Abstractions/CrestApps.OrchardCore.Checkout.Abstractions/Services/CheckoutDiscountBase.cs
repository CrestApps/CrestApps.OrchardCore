using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// What a discount can take off: for a one-time discount, what is bought now, but not shipping; for a first-cycle
/// discount, the recurring charges collected now. Shipping is discounted only by a discount aimed at it.
/// </summary>
public static class CheckoutDiscountBase
{
    /// <summary>
    /// Gets whether a discount aimed at <paramref name="target"/> can take something off the line.
    /// </summary>
    public static bool IsDiscountable(CheckoutLineItem line, DiscountTarget target)
    {
        ArgumentNullException.ThrowIfNull(line);

        return target == DiscountTarget.FirstCycle
            ? line.Plan is not null && CheckoutObligations.GetDeferralDays([line]) == 0
            : line.Plan is null && line.TaxableKind != TaxableItemKind.Shipping;
    }

    /// <summary>
    /// Gets the amount a discount aimed at <paramref name="target"/> applies to: the lines it can discount. An
    /// invoice without lines (one built by hand) falls back to the whole bucket.
    /// </summary>
    public static decimal GetAmount(CheckoutInvoice invoice, DiscountTarget target)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        if (invoice.LineItems is not { Length: > 0 } lines)
        {
            return target == DiscountTarget.FirstCycle
                ? invoice.FirstRecurringPaymentAmount ?? 0m
                : invoice.InitialPaymentAmount ?? 0m;
        }

        return Money.Round(lines.Where(line => IsDiscountable(line, target)).Sum(line => line.GetLineTotal(invoice.Currency)), invoice.Currency);
    }
}
