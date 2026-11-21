namespace CrestApps.OrchardCore.Payments;

/// <summary>
/// Well-known identifiers shared by the payment and shipping provider modules.
/// </summary>
public static class PaymentConstants
{
    /// <summary>
    /// The request groups of Orchard Core's Rate Limits the provider modules tag their routes with.
    /// </summary>
    public static class RateLimitGroups
    {
        /// <summary>
        /// The webhooks a payment or shipping provider calls: 600 a minute per network.
        /// </summary>
        public const string Webhook = "payment-webhook";

        /// <summary>
        /// The default limit of <see cref="Webhook"/>, per minute.
        /// </summary>
        public const int WebhookRequestsPerMinute = 600;
    }
}
