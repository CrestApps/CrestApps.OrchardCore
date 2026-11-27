namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// What became of the payments begun for a checkout that is about to be edited.
/// </summary>
public enum CheckoutPaymentReleaseResult
{
    /// <summary>
    /// No payment is live any more: the edit may go ahead.
    /// </summary>
    Released,

    /// <summary>
    /// A payment was taken already, or the provider would not cancel it: the edit must wait.
    /// </summary>
    PaymentInProgress,
}
