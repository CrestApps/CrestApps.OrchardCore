namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// Well-known identifiers used throughout the checkout framework.
/// </summary>
public static class CheckoutConstants
{
    /// <summary>
    /// The reserved step key for the payment step that every paid checkout flow contains.
    /// </summary>
    public const string PaymentStepKey = "Payment";

    /// <summary>
    /// The feature identifiers exposed by the checkout module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The core checkout feature that provides the provider-agnostic checkout and payment framework.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.Checkout";
    }

    /// <summary>
    /// Rate-limit group names attached to the sensitive, anonymous-facing checkout routes. They are
    /// consumed by the optional Orchard Core <c>OrchardCore.RateLimits</c> module.
    /// </summary>
    public static class RateLimitGroups
    {
        /// <summary>
        /// The public checkout requests (the checkout form and each flow step).
        /// </summary>
        public const string Checkout = "checkout";

        /// <summary>
        /// The anonymous payment endpoints that talk to a payment provider.
        /// </summary>
        public const string Payment = "checkout-payment";
    }
}
