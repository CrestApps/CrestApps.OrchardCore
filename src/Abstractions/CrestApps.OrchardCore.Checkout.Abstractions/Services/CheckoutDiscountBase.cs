using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// What a discount can take off: for a one-time discount, what is bought now, but not shipping; for a first-cycle
/// discount, the recurring charges collected now; for a shipping discount, the shipping charged now and nothing else.
/// </summary>
public static class CheckoutDiscountBase
{
    /// <summary>
    /// Gets whether a discount aimed at <paramref name="target"/> can take something off the line.
    /// </summary>
    public static bool IsDiscountable(CheckoutLineItem line, DiscountTarget target)
    {
        ArgumentNullException.ThrowIfNull(line);

        if (line.ExcludeFromDiscounts)
        {
            return false;
        }

        return target switch
        {
            DiscountTarget.FirstCycle => line.Plan is not null && CheckoutObligations.GetDeferralDays([line]) == 0,
            DiscountTarget.Shipping => line.Plan is null && line.TaxableKind == TaxableItemKind.Shipping,
            _ => line.Plan is null && line.TaxableKind != TaxableItemKind.Shipping,
        };
    }

    /// <summary>
    /// Gets the amount a discount aimed at <paramref name="target"/> applies to: the lines it can discount. An
    /// invoice without lines (one built by hand) falls back to the whole bucket, and has no shipping.
    /// </summary>
    public static decimal GetAmount(CheckoutInvoice invoice, DiscountTarget target)
        => GetAmount(invoice, target, line => true);

    /// <summary>
    /// Gets the amount a discount aimed at <paramref name="target"/> applies to, counting only the lines that
    /// <paramref name="predicate"/> accepts, such as the products a coupon is limited to.
    /// </summary>
    public static decimal GetAmount(CheckoutInvoice invoice, DiscountTarget target, Func<CheckoutLineItem, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(predicate);

        if (invoice.LineItems is not { Length: > 0 } lines)
        {
            return target switch
            {
                DiscountTarget.FirstCycle => invoice.FirstRecurringPaymentAmount ?? 0m,
                DiscountTarget.Shipping => 0m,
                _ => invoice.InitialPaymentAmount ?? 0m,
            };
        }

        return Money.Round(lines.Where(line => IsDiscountable(line, target) && predicate(line)).Sum(line => line.GetLineTotal(invoice.Currency)), invoice.Currency);
    }
}
