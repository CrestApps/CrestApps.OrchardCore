namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// Well-known identifiers used throughout the checkout framework.
/// </summary>
public static class CheckoutConstants
{
    /// <summary>
    /// The YesSql collection the coupon catalog is stored in.
    /// </summary>
    public const string CouponCollectionName = "Coupon";

    /// <summary>
    /// The name of the cookie that carries a guest's checkout ownership tokens.
    /// </summary>
    public const string GuestTokenCookieName = "checkout_owner";

    /// <summary>
    /// The data-protection purpose for the guest ownership cookie. It is unique to checkout so the cookie
    /// cannot be replayed against another feature.
    /// </summary>
    public const string GuestTokenProtectorPurpose = "CrestApps.OrchardCore.Checkout.GuestOwnership.v1";

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
    /// The named routes of the public checkout experience.
    /// </summary>
    public static class RouteNames
    {
        /// <summary>
        /// The route that displays a step of a pending checkout.
        /// </summary>
        public const string Step = "CheckoutStep";

        /// <summary>
        /// The route that displays the confirmation of a completed checkout.
        /// </summary>
        public const string Confirmation = "CheckoutConfirmation";

        /// <summary>
        /// The endpoint that begins payment with the selected provider.
        /// </summary>
        public const string BeginPayment = "CheckoutBeginPayment";

        /// <summary>
        /// The endpoint the client polls while a provider settles a payment.
        /// </summary>
        public const string PaymentStatus = "CheckoutPaymentStatus";
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
