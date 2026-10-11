namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// Thrown when a checkout cannot be completed because its payment obligations are not fully settled. It is
/// raised at completion, after the checkout has reconciled its durable payment attempts against the
/// providers' authoritative APIs, so a checkout is never marked complete while money is still outstanding
/// or a charge was reported as failed.
/// </summary>
public sealed class CheckoutPaymentException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutPaymentException"/> class.
    /// </summary>
    public CheckoutPaymentException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutPaymentException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public CheckoutPaymentException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutPaymentException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public CheckoutPaymentException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}
