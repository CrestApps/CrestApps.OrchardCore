namespace CrestApps.OrchardCore.Payments.Models;

/// <summary>
/// Contains the configured payment methods and the method selected as the default.
/// </summary>
public sealed class PaymentMethodOptions
{
    /// <summary>
    /// Gets or sets the key of the payment method used by default.
    /// </summary>
    public string DefaultPaymentMethod { get; set; }

    /// <summary>
    /// Gets the configured payment methods, keyed by payment method name.
    /// </summary>
    public Dictionary<string, PaymentMethod> PaymentMethods { get; } = [];
}
