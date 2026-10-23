namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// Thrown when a checkout cannot be started because a handler refused what it was asked to pay for, for example a set
/// of transactions that belong to different customers (see <see cref="CheckoutFlowActivatingContext.Reject(string)"/>).
/// Nothing is saved when it is thrown.
/// </summary>
public sealed class CheckoutRejectedException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutRejectedException"/> class.
    /// </summary>
    public CheckoutRejectedException()
        : this(errors: [])
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutRejectedException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public CheckoutRejectedException(string message)
        : this(errors: string.IsNullOrEmpty(message) ? [] : [message])
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutRejectedException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public CheckoutRejectedException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        Errors = string.IsNullOrEmpty(message) ? [] : [message];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutRejectedException"/> class.
    /// </summary>
    /// <param name="errors">Why the checkout was refused, one entry per reason, in words a customer can read.</param>
    public CheckoutRejectedException(IReadOnlyList<string> errors)
        : base(errors is { Count: > 0 } ? string.Join(" ", errors) : "The checkout could not be started.")
    {
        Errors = errors ?? [];
    }

    /// <summary>
    /// Gets why the checkout was refused, one entry per reason, in words a customer can read.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }
}
