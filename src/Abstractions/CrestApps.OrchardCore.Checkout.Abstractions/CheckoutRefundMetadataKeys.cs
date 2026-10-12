namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// The well-known metadata keys a checkout stamps on a refund it creates at a payment gateway, so a later
/// gateway notification (for example a webhook) can be correlated back to the originating local
/// <see cref="Models.PaymentRefund"/> even when the provider does not echo the idempotency key on its own
/// dedicated field. A provider adapter forwards gateway metadata verbatim; only the checkout interprets
/// these keys.
/// </summary>
public static class CheckoutRefundMetadataKeys
{
    /// <summary>
    /// The metadata key that carries the idempotency key of the originating local refund request.
    /// </summary>
    public const string IdempotencyKey = "checkout_refund_idempotency_key";
}
