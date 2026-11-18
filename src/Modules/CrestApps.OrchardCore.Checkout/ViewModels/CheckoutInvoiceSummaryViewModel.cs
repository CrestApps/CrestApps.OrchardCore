namespace CrestApps.OrchardCore.Checkout.ViewModels;

/// <summary>
/// The order summary shown beside every checkout step and on its confirmation.
/// </summary>
public class CheckoutInvoiceSummaryViewModel
{
    /// <summary>
    /// Gets or sets what the checkout charges.
    /// </summary>
    public CheckoutInvoice Invoice { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the checkout was completed with a method that collects nothing now (Pay
    /// Later, bank transfer), so the total reads as due later rather than due now.
    /// </summary>
    public bool PaymentDeferred { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the checkout took the money, so its confirmation shows the total as paid.
    /// </summary>
    public bool Paid { get; set; }
}
