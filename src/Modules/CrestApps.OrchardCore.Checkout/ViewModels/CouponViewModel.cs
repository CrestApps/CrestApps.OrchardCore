namespace CrestApps.OrchardCore.Checkout.ViewModels;

/// <summary>
/// The coupon box on the payment step.
/// </summary>
public class CouponViewModel
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

    /// <summary>
    /// Gets or sets the codes the checkout carries, with what each took off.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public IReadOnlyList<AppliedCouponViewModel> Applied { get; set; } = [];

    /// <summary>
    /// Gets or sets whether another code can be entered.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public bool CanAddMore { get; set; } = true;
}

/// <summary>
/// A code on the checkout: what it took off, or why it took nothing off.
/// </summary>
public class AppliedCouponViewModel
{
    public string Code { get; set; }

    public string Description { get; set; }

    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the discount it can't be used with and gave way to, or <see langword="null"/>.
    /// </summary>
    public string GaveWayTo { get; set; }
}
