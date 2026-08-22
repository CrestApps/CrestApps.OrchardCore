namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// Represents the outcome of a Stripe Connect operation such as linking or unlinking an account.
/// </summary>
public sealed class StripeConnectionResult
{
    /// <summary>
    /// Gets a value indicating whether the operation completed successfully.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets the human-readable message describing the outcome of the operation.
    /// </summary>
    public string Message { get; init; }

    /// <summary>
    /// Gets the identifier of the Stripe account affected by the operation, when available.
    /// </summary>
    public string AccountId { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="message">The message describing the success.</param>
    /// <param name="accountId">The identifier of the affected Stripe account.</param>
    /// <returns>A successful <see cref="StripeConnectionResult"/>.</returns>
    public static StripeConnectionResult Success(string message, string accountId = null)
        => new() { Succeeded = true, Message = message, AccountId = accountId };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="message">The message describing the failure.</param>
    /// <returns>A failed <see cref="StripeConnectionResult"/>.</returns>
    public static StripeConnectionResult Failure(string message)
        => new() { Succeeded = false, Message = message };
}
