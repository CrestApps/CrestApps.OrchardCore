using CrestApps.OrchardCore.Payments;

namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// A single priced line on a checkout invoice.
/// </summary>
public sealed class CheckoutLineItem
{
    /// <summary>
    /// A unique identifier for the line item.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// A short description of the line item.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// The quantity being purchased.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// The price of each unit, expressed in major currency units.
    /// </summary>
    public double UnitPrice { get; set; }

    /// <summary>
    /// The recurring plan for this line item, or <see langword="null"/> for a one-time charge.
    /// </summary>
    public RecurringPlan Plan { get; set; }

    /// <summary>
    /// Returns the line total rounded to the precision of the supplied currency. Rounding at the currency's
    /// own scale keeps zero-decimal (for example JPY) and three-decimal (for example KWD) currencies exact.
    /// </summary>
    /// <param name="currency">The ISO-4217 currency code used to determine rounding precision.</param>
    public double GetLineTotal(string currency = null)
        => Money.Round(Quantity * UnitPrice, currency);
}
