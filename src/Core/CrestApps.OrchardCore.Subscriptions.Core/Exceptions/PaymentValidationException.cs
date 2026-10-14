namespace CrestApps.OrchardCore.Subscriptions.Core.Exceptions;

/// <summary>
/// Represents a failure caused by payment data that does not match the expected subscription invoice.
/// </summary>
public class PaymentValidationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentValidationException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the payment validation failure.</param>
    public PaymentValidationException(string message)
        : base(message) { }
}
