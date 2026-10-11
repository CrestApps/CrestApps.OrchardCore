namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// The settled state of a single payment transaction recorded against a checkout.
/// </summary>
public enum PaymentStatus
{
    /// <summary>
    /// The outcome of the payment is not yet known.
    /// </summary>
    Unknown,

    /// <summary>
    /// The payment was confirmed by the provider.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The payment failed at the provider.
    /// </summary>
    Failed,
}
