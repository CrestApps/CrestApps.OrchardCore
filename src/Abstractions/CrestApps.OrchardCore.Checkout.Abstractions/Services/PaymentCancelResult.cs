namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// The result of <see cref="ICheckoutPaymentProvider.CancelAsync"/>. Cancellation (voiding or refunding a
/// remote resource) is itself a money-moving operation, so it is never assumed to have happened: the
/// caller must only treat an attempt as compensated when the provider confirms it here.
/// </summary>
public sealed class PaymentCancelResult
{
    /// <summary>
    /// Whether the provider confirmed the remote resource was voided or refunded.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Whether the cancellation is accepted but not yet final (for example an asynchronous refund) and
    /// must be reconciled again before the attempt is considered compensated.
    /// </summary>
    public bool IsPending { get; set; }

    /// <summary>
    /// The error message when the provider could not cancel or compensate the attempt.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Creates a confirmed cancellation result.
    /// </summary>
    public static PaymentCancelResult Success()
        => new() { Succeeded = true };

    /// <summary>
    /// Creates a pending cancellation result that must be reconciled again before it is final.
    /// </summary>
    public static PaymentCancelResult Pending()
        => new() { IsPending = true };

    /// <summary>
    /// Creates a failed cancellation result.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    public static PaymentCancelResult Failure(string errorMessage)
        => new() { Succeeded = false, ErrorMessage = errorMessage };
}
