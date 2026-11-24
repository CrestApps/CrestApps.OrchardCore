namespace CrestApps.OrchardCore.PayLater.ViewModels;

/// <summary>
/// What the Pay Later panel of the checkout's payment step tells the buyer.
/// </summary>
public class PayLaterCheckoutPaymentMethodViewModel
{
    /// <summary>
    /// Gets or sets the day the payment falls due, in the site's time zone, or <see langword="null"/> when it has no
    /// due date.
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether nothing is owed until a free trial or a delayed start ends.
    /// </summary>
    public bool StartsLater { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the purchase renews, so each renewal is billed the same way.
    /// </summary>
    public bool Recurring { get; set; }
}
