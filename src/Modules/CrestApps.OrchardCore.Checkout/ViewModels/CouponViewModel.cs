namespace CrestApps.OrchardCore.Checkout.ViewModels;

/// <summary>
/// The coupon box on the payment step.
/// </summary>
public sealed class CouponViewModel
{
    /// <summary>
    /// Gets or sets the code the customer entered.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the amount the invoice was actually reduced by, which is what the customer is shown
    /// rather than what the coupon claims to be worth.
    /// </summary>
    public decimal AppliedAmount { get; set; }

    /// <summary>
    /// Gets or sets the invoice currency.
    /// </summary>
    public string Currency { get; set; }
}
