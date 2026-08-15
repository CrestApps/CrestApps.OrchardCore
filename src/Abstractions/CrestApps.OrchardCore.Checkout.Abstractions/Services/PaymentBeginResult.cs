namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// The result of <see cref="ICheckoutPaymentProvider.BeginAsync"/>. It tells the checkout how to hand the
/// customer off to the provider and, critically, returns the provider's authoritative reference so it can
/// be persisted on the attempt immediately.
/// </summary>
public sealed class PaymentBeginResult
{
    /// <summary>
    /// Whether the provider accepted the request to begin payment.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// The provider's authoritative reference for the interaction (for example a PaymentIntent id). Always
    /// set when <see cref="Succeeded"/> is <c>true</c> so it can be recorded on the attempt right away.
    /// </summary>
    public string ProviderReference { get; set; }

    /// <summary>
    /// For hosted-checkout providers, the absolute URL the customer must be redirected to.
    /// </summary>
    public string RedirectUrl { get; set; }

    /// <summary>
    /// For embedded-element providers, the client secret or token the front end needs to confirm payment.
    /// </summary>
    public string ClientSecret { get; set; }

    /// <summary>
    /// Whether the customer must complete an additional action (for example 3-D Secure) before the payment
    /// can be confirmed.
    /// </summary>
    public bool RequiresAction { get; set; }

    /// <summary>
    /// The error message when <see cref="Succeeded"/> is <c>false</c>.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="providerReference">The provider's authoritative reference.</param>
    public static PaymentBeginResult Success(string providerReference)
        => new() { Succeeded = true, ProviderReference = providerReference };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    public static PaymentBeginResult Failure(string errorMessage)
        => new() { Succeeded = false, ErrorMessage = errorMessage };
}
