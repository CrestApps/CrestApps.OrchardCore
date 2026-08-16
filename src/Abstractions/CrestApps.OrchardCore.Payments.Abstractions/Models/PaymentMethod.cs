namespace CrestApps.OrchardCore.Payments.Models;

/// <summary>
/// Describes a payment method that can be presented to users or handled by a processor.
/// </summary>
public class PaymentMethod
{
    /// <summary>
    /// Gets or sets the display title of the payment method.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a processor is registered for this payment method.
    /// </summary>
    public bool HasProcessor { get; set; }
}
